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
        private IActionManager actionManager;

        [Inject]
        private AbilityActivator abilityActivator;

        [Inject]
        private IMovementManager movementManager;

        [Inject] private GameFramework.World.WorldEventBuffer worldEventBuffer;
        [Inject] private GameFramework.World.IEventSink eventSink;
        [Inject] private DeathCascadeSystem deathCascade;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private IPhysicsSimulator physicsSimulator;
        [Inject] private GameFramework.World.IWorld world;
        [Inject] private AbilityEffectExecutor abilityEffectExecutor;

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
                var inputComponent = entity.GetEntityComponent<EntityInputComponent>();
                if (inputComponent == null)
                {
                    continue;   // 입력 비조종(AI 등) — 이동 모터 미사용
                }

                var input = inputComponent.GetInput(Runner.Time.tick);

                // 무입력 틱에도 수평 속도를 0으로 제동해야 클·서 결정론이 유지된다(미스 시 0 입력으로 movement만 실행).
                // Phase 3b: recent_inputs로 재구성된 입력은 EntityTransform이 null이라 entityTransform도 null/default가 된다.
                // LOPMovementManager.ProcessInput은 entityTransform을 사용하지 않으므로 안전.
                EntityTransform entityTransform = input != null
                    ? MapperConfig.mapper.Map<EntityTransform>(input.EntityTransform)
                    : default;
                float horizontal = input != null ? input.PlayerInput.Horizontal : 0f;
                float vertical = input != null ? input.PlayerInput.Vertical : 0f;
                bool jump = input != null ? input.PlayerInput.Jump : false;

                movementManager.ProcessInput(entity, entityTransform, horizontal, vertical, jump);

                if (input == null)
                {
                    continue;   // 미스 — 어빌리티/시퀀스 송신은 입력 있을 때만
                }

                if (input.PlayerInput.AbilityId != 0)
                {
                    // 실제 발동 시 발동 연출 이벤트 append → WorldEventSink가 AbilityActivatedToC로 브로드캐스트.
                    if (abilityActivator.TryActivate(entity.entityId, input.PlayerInput.AbilityId, Runner.Time.tick))
                    {
                        worldEventBuffer.Append(new GameFramework.World.AbilityActivatedEvent(entity.entityId, input.PlayerInput.AbilityId));
                    }
                }
                else if (string.IsNullOrEmpty(input.PlayerInput.ActionCode) == false)
                {
                    actionManager.TryStartAction(entity, input.PlayerInput.ActionCode);
                }

                InputSequenceToC inputSequnceToC = new InputSequenceToC();
                inputSequnceToC.EntityId = entity.entityId;
                inputSequnceToC.InputSequence = new InputSequence
                {
                    Tick = Runner.Time.tick,
                    Sequence = input.PlayerInput.SequenceNumber,
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
                var inputComponent = entity.GetEntityComponent<EntityInputComponent>();
                if (inputComponent == null)
                {
                    continue;
                }

                var summary = inputComponent.SummarizeTiming();
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

                foreach (var action in entity.GetComponents<Action>())
                {
                    entitySnapsToC.ActionDatas.Add(new ActionData
                    {
                        ActionCode = action.actionCode,
                        IsActive = action.isActive,
                        RemainCooldown = action.remainCooldown,
                        StartTick = action.startTick,
                    });
                }

                session.Send(entitySnapsToC);
            }

            entityManager.DestroyMarkedEntities();
        }
    }
}
