using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using GameFramework;
using VContainer;
using LOP.Event.LOPRunner.Update;

namespace LOP
{
    [DIMonoBehaviour]
    public class LOPRunner : RunnerBase
    {
        [Inject]
        private ISessionManager sessionManager;

        [Inject]
        private AbilityActivator abilityActivator;

        [Inject] private GameFramework.World.WorldEventBuffer worldEventBuffer;
        [Inject] private GameFramework.World.IEventSink eventSink;
        [Inject] private DeathCascadeSystem deathCascade;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private IPhysicsSimulator physicsSimulator;
        [Inject] private GameFramework.World.IWorld world;
        [Inject] private AbilityEffectExecutor abilityEffectExecutor;
        [Inject] private InputBufferSystem inputBufferSystem;

        [Inject] private IMapLoader mapLoader;
        [Inject] private GameRuleSystem gameRuleSystem;

        private const string MapId = "Assets/Art/Scenes/FlapWangMap.unity";

        private readonly Restorer restorer = new Restorer();

        public new LOPEntityManager entityManager => base.entityManager as LOPEntityManager;

        protected override INetworkTime CreateNetworkTime() => new MirrorNetworkTime();

        public override async Task InitializeAsync()
        {
            gameState = Initializing.State;

            var oldSimulationMode = Physics.simulationMode;
            var oldAutoSyncTransforms = Physics.autoSyncTransforms;

            restorer.action += () =>
            {
                Physics.simulationMode = oldSimulationMode;
                Physics.autoSyncTransforms = oldAutoSyncTransforms;
            };

            Physics.simulationMode = SimulationMode.Script;
            Physics.autoSyncTransforms = false;
            Physics.gravity = new Vector3(0, -9.81f * 2, 0);

            // 맵 로딩과 베이스 초기화를 병렬로 — 둘 다 끝나길 기다린다.
            var mapLoadTask = mapLoader.LoadAsync(MapId);

            await base.InitializeAsync();

            await mapLoadTask;

            gameRuleSystem.Initialize();

            gameState = Initialized.State;
        }

        public override async Task DeinitializeAsync()
        {
            gameRuleSystem.Deinitialize();

            await base.DeinitializeAsync();

            restorer.Dispose();

            await mapLoader.UnloadAsync();
        }

        public override void Run(long tick, double interval, double elapsedTime)
        {
            base.Run(tick, interval, elapsedTime);

            gameState = Playing.State;
        }

        public override void Stop()
        {
            base.Stop();

            gameState = Paused.State;
        }

        private void LateUpdate()
        {
            if (initialized && tickUpdater.elapsedTime > 60 * 5)
            {
                gameState = GameOver.State;
            }
        }

        public override void UpdateRunner()
        {
            BeginUpdate();

            ProcessNetworkMessage();

            ProcessInput();

            UpdateEntity();

            UpdateAI();

            world.Tick(Runner.Time.tick, (float)tickUpdater.interval);
            DriveAbilityEffects();

            SimulatePhysics();

            ProcessDeaths();

            ProcessEvent();

            SendInputTimingFeedback();

            EndUpdate();
        }

        // world.Tick이 페이즈를 전진시킨 뒤, 진행 중 어빌리티의 Active 창 effect를 executor로 구동(대시 push 등).
        // 핸들러가 side 자원(Rigidbody)을 ctx의 entityManager로 잡도록 host가 구동 — DI 순환 회피.
        private void DriveAbilityEffects()
        {
            long tick = Runner.Time.tick;
            foreach (var entity in entityManager.GetEntities<LOPEntity>())
            {
                abilityEffectExecutor.DriveActiveEntity(entityRegistry.Get(entity.entityId), entityManager, tick);
            }
        }

        private void BeginUpdate()
        {
            DispatchEvent<Begin>();
        }

        private void ProcessNetworkMessage()
        {
        }

        private void ProcessInput()
        {
            IEnumerable<LOPEntity> LOPEntities = new List<LOPEntity>(entityManager.GetEntities<LOPEntity>());

            foreach (var entity in LOPEntities)
            {
                var buffer = entityRegistry.Get(entity.entityId).Get<InputBuffer>();
                if (buffer == null)
                {
                    continue;   // 입력 비조종(AI 등) — 버퍼 없음
                }

                // 입력을 스탬프된 틱에 제때 처리 — 클라 예측(즉시 적용)과 정렬(offset 0). 이건 하드 롤백 재조정의 전제다:
                // 서버를 늦추면(입력을 과거 틱에 소비) 클라 예측과 항상 어긋나 낙하·충돌에서 발산한다. 늦추지 말 것.
                // 지터로 입력이 늦게 도착할 여유가 더 필요하면 서버가 아니라 클라 lead(AheadMargin)를 키운다(표준).
                // command-frame 정렬 + 지각 prune → 이번 틱 커맨드 확정(Current). 소비는 LOPWorld.Tick(MovementSystem).
                long targetTick = Runner.Time.tick;
                int pruned = inputBufferSystem.PruneBefore(buffer, targetTick);
                for (int i = 0; i < pruned; i++)
                {
                    buffer.TimingTracker.RecordPrune();
                }

                long previousSequence = buffer.LastProcessedSequence;
                var input = inputBufferSystem.Consume(buffer, targetTick);

                if (input == null)
                {
                    // 미스 → 0 커맨드 확정(수평 제동). 어빌리티/시퀀스 송신은 입력 있을 때만.
                    inputBufferSystem.SetCurrent(buffer, new InputCommand());
                    continue;
                }

                long gap = input.SequenceNumber - previousSequence - 1;
                if (gap > 0)
                {
                    buffer.TimingTracker.RecordSeqGap((int)gap);
                }

                if (input.AbilityId != 0)
                {
                    // 발동 연출 cue는 AbilityActivator가 내부에서 append한다(플레이어·AI 공용).
                    abilityActivator.TryActivate(entity.entityId, input.AbilityId, Runner.Time.tick);
                }

                InputSequenceToC inputSequnceToC = new InputSequenceToC();
                inputSequnceToC.EntityId = entity.entityId;
                inputSequnceToC.InputSequence = new InputSequence
                {
                    Tick = Runner.Time.tick,
                    Sequence = input.SequenceNumber,
                };

                string userId = entityManager.GetUserIdByEntityId(entity.entityId);
                ISession session = sessionManager.GetSessionByUserId(userId);
                session.Send(inputSequnceToC);
            }
        }

        // ~0.5초마다 조종 엔티티별 입력 타이밍 요약을 그 세션에 전송(Phase 4). 활동 없으면 skip.
        private const long InputTimingFeedbackIntervalTicks = 15;  // 틱레이트 기준 ~0.5초 — 필요시 조정

        private void SendInputTimingFeedback()
        {
            if (Runner.Time.tick % InputTimingFeedbackIntervalTicks != 0)
            {
                return;
            }

            foreach (var entity in new List<LOPEntity>(entityManager.GetEntities<LOPEntity>()))
            {
                var buffer = entityRegistry.Get(entity.entityId).Get<InputBuffer>();
                if (buffer == null)
                {
                    continue;
                }

                var summary = buffer.TimingTracker.Summarize();
                if (summary.HasActivity == false)
                {
                    continue;
                }

                string userId = entityManager.GetUserIdByEntityId(entity.entityId);
                ISession session = sessionManager.GetSessionByUserId(userId);
                if (session == null)
                {
                    continue;
                }

                session.Send(new InputTimingToC
                {
                    AvgD = summary.AvgD,
                    MaxD = summary.MaxD,
                    PruneCount = summary.PruneCount,
                    SeqGapCount = summary.SeqGapCount,
                    SampleCount = summary.SampleCount,
                }, reliable: false);
            }
        }

        private void UpdateEntity()
        {
            DispatchEvent<BeforeEntityUpdate>();

            entityManager.UpdateEntities();

            DispatchEvent<AfterEntityUpdate>();
        }

        private void UpdateAI()
        {
        }

        private void SimulatePhysics()
        {
            DispatchEvent<BeforePhysicsSimulation>();

            physicsSimulator.Simulate((float)tickUpdater.interval);

            DispatchEvent<AfterPhysicsSimulation>();
        }

        private void ProcessDeaths()
        {
            var snapshot = worldEventBuffer.Snapshot;
            if (snapshot.Count == 0) return;
            deathCascade.Resolve(snapshot);
        }

        private void ProcessEvent()
        {
            // --- World Core — 슬라이스 3: 이벤트 버퍼 드레인 ---
            var snapshot = worldEventBuffer.Snapshot;
            if (snapshot.Count == 0) return;

            eventSink.Emit(snapshot);
            worldEventBuffer.Clear();
            // --- end World Core slice 3 ---
        }

        private void EndUpdate()
        {
            DispatchEvent<End>();

            EntitySnap[] allEntitySnaps = entityManager.GetAllEntitySnaps();

            foreach (var session in sessionManager.GetAllSessions())
            {
                EntitySnapsToC entitySnapsToC = new EntitySnapsToC();
                entitySnapsToC.Tick = tickUpdater.tick;
                entitySnapsToC.EntitySnaps.AddRange(allEntitySnaps);

                session.Send(entitySnapsToC);
            }

            foreach (var session in sessionManager.GetAllSessions())
            {
                LOPEntity entity = entityManager.GetEntityByUserId<LOPEntity>(session.userId);

                // HP/MP/Level/Exp/StatPoints 모두 World 코어에서 읽는다.
                GameFramework.World.Entity worldEntity = entityRegistry.Get(entity.entityId);
                GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
                if (health == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Health not found for entity {entity.entityId}");
                }

                UserEntitySnapToC entitySnapsToC = new UserEntitySnapToC();
                entitySnapsToC.CurrentHP = health?.Current ?? 0;
                entitySnapsToC.MaxHP = health?.Max ?? 0;
                GameFramework.World.Mana mana = worldEntity?.Get<GameFramework.World.Mana>();
                if (mana == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Mana not found for entity {entity.entityId}");
                }
                entitySnapsToC.CurrentMP = mana?.Current ?? 0;
                entitySnapsToC.MaxMP = mana?.Max ?? 0;
                GameFramework.World.Level level = worldEntity?.Get<GameFramework.World.Level>();
                if (level == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Level not found for entity {entity.entityId}");
                }
                entitySnapsToC.CurrentExp = level?.Exp ?? 0;
                entitySnapsToC.Level = level?.Value ?? 0;
                GameFramework.World.Stats stats = worldEntity?.Get<GameFramework.World.Stats>();
                if (stats == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Stats not found for entity {entity.entityId}");
                }
                entitySnapsToC.StatPoints = stats?.UnspentPoints ?? 0;

                session.Send(entitySnapsToC);
            }

            entityManager.DestroyMarkedEntities();
        }
    }
}
