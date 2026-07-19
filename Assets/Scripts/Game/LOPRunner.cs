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
        [Inject] private GameFramework.World.IMotionBridge motionBridge;
        [Inject] private IPhysicsSimulator physicsSimulator;
        [Inject] private GameFramework.World.IWorld world;
        [Inject] private InputBufferSystem inputBufferSystem;

        [Inject] private IMapLoader mapLoader;
        [Inject] private GameRuleSystem gameRuleSystem;
        [Inject] private EntitySpawner entitySpawner;

        private const string MapId = "Assets/Art/Scenes/FlapWangMap.unity";

        private readonly Restorer restorer = new Restorer();

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

            SimulatePhysics();

            ProcessDeaths();

            ProcessEvent();

            SendInputTimingFeedback();

            EndUpdate();
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
            List<GameFramework.World.Entity> worldEntities = new List<GameFramework.World.Entity>(entityRegistry.All);

            foreach (var worldEntity in worldEntities)
            {
                var buffer = worldEntity.Get<InputBuffer>();
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
                    abilityActivator.TryActivate(worldEntity.Id, input.AbilityId, Runner.Time.tick);
                }

                InputSequenceToC inputSequnceToC = new InputSequenceToC();
                inputSequnceToC.EntityId = worldEntity.Id;
                inputSequnceToC.InputSequence = new InputSequence
                {
                    Tick = Runner.Time.tick,
                    Sequence = input.SequenceNumber,
                };

                string userId = worldEntity.Get<GameFramework.World.Ownership>()?.OwnerId;
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

            foreach (var worldEntity in new List<GameFramework.World.Entity>(entityRegistry.All))
            {
                var buffer = worldEntity.Get<InputBuffer>();
                if (buffer == null)
                {
                    continue;
                }

                var summary = buffer.TimingTracker.Summarize();
                if (summary.HasActivity == false)
                {
                    continue;
                }

                string userId = worldEntity.Get<GameFramework.World.Ownership>()?.OwnerId;
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

            DispatchEvent<AfterEntityUpdate>();
        }

        private void UpdateAI()
        {
        }

        private void SimulatePhysics()
        {
            DispatchEvent<BeforePhysicsSimulation>();

            // World.Transform → rb 팔로우: PhysicsBody 가진 모든 엔티티(내 캐릭=예측, 남·아이템=보간).
            // Simulated는 world.Tick서 이미 밀렸으나 idempotent. per-entity LOPEntityController 대체.
            foreach (var entity in entityRegistry.All)
            {
                motionBridge.PushMotion(entity);
            }

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

            EntitySnap[] allEntitySnaps = BuildAllEntitySnaps();

            // durable 스냅샷 → unreliable(막 배송). 유실돼도 다음 스냅이 최신 전체를 덮음.
            // Mirror unreliable은 큰 메시지 조각내기(fragment) 불가 → 배치 한도(≈1184B) 초과 시 통째 드롭.
            // 그래서 엔티티를 바이트 예산으로 나눠 여러 메시지(같은 tick)로 청킹(서브셋 청킹, Quake/Source식).
            // 각 청크 독립 → 하나 유실돼도 그 엔티티만 한 틱 놓치고 다음 틱 복구(fragment-재조립의 손실 복리 회피).
            const int MaxEntityBytesPerMessage = 1000;   // 한도(1184) 밑 여유(tick 필드·메시지 프레이밍 몫).

            List<EntitySnapsToC> chunks = new List<EntitySnapsToC>();   // 세션 무관(같은 스냅) → 한 번 만들어 모두에게.
            EntitySnapsToC chunk = new EntitySnapsToC { Tick = tickUpdater.tick };
            int chunkBytes = 0;
            foreach (var snap in allEntitySnaps)
            {
                int snapBytes = snap.CalculateSize() + 2;   // +반복 필드 태그/길이 근사
                if (chunk.EntitySnaps.Count > 0 && chunkBytes + snapBytes > MaxEntityBytesPerMessage)
                {
                    chunks.Add(chunk);
                    chunk = new EntitySnapsToC { Tick = tickUpdater.tick };
                    chunkBytes = 0;
                }
                chunk.EntitySnaps.Add(snap);
                chunkBytes += snapBytes;
            }
            if (chunk.EntitySnaps.Count > 0)
            {
                chunks.Add(chunk);
            }

            foreach (var session in sessionManager.GetAllSessions())
            {
                foreach (var entitySnapsToC in chunks)
                {
                    session.Send(entitySnapsToC, reliable: false);
                }
            }

            foreach (var session in sessionManager.GetAllSessions())
            {
                string entityId = entitySpawner.GetEntityIdByUserId(session.userId);

                // HP/MP/Level/Exp/StatPoints 모두 World 코어에서 읽는다.
                GameFramework.World.Entity worldEntity = entityRegistry.Get(entityId);
                GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
                if (health == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Health not found for entity {entityId}");
                }

                UserEntitySnapToC entitySnapsToC = new UserEntitySnapToC();
                entitySnapsToC.CurrentHP = health?.Current ?? 0;
                entitySnapsToC.MaxHP = health?.Max ?? 0;
                GameFramework.World.Mana mana = worldEntity?.Get<GameFramework.World.Mana>();
                if (mana == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Mana not found for entity {entityId}");
                }
                entitySnapsToC.CurrentMP = mana?.Current ?? 0;
                entitySnapsToC.MaxMP = mana?.Max ?? 0;
                GameFramework.World.Level level = worldEntity?.Get<GameFramework.World.Level>();
                if (level == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Level not found for entity {entityId}");
                }
                entitySnapsToC.CurrentExp = level?.Exp ?? 0;
                entitySnapsToC.Level = level?.Value ?? 0;
                GameFramework.World.Stats stats = worldEntity?.Get<GameFramework.World.Stats>();
                if (stats == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Stats not found for entity {entityId}");
                }
                entitySnapsToC.StatPoints = stats?.UnspentPoints ?? 0;

                session.Send(entitySnapsToC);
            }

            entitySpawner.FlushDespawns();
        }

        private EntitySnap[] BuildAllEntitySnaps()
        {
            var entitySnapList = new List<EntitySnap>();

            foreach (var worldEntity in entityRegistry.All)
            {
                GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
                var snap = new EntitySnap
                {
                    EntityId = worldEntity.Id,
                    Position = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity)),
                    Rotation = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity)),
                    Velocity = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetVelocity(worldEntity)),
                    MaxHP = health?.Max ?? 0,
                    CurrentHP = health?.Current ?? 0,
                };

                var contributions = worldEntity?.Get<MotionContributions>();
                if (contributions != null)
                {
                    foreach (var c in contributions.Items)
                    {
                        snap.MotionContributions.Add(new ProtoMotionContribution
                        {
                            Horizontal = new ProtoVector3 { X = c.Horizontal.X, Y = c.Horizontal.Y, Z = c.Horizontal.Z },
                            Mode = (int)c.Mode,
                            Priority = c.Priority,
                            StartTick = c.StartTick,
                            EndTick = c.EndTick,
                            DecayPerTick = c.DecayPerTick,
                        });
                    }
                }

                entitySnapList.Add(snap);
            }

            return entitySnapList.ToArray();
        }
    }
}
