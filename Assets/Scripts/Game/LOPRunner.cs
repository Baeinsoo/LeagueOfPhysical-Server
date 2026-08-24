using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using GameFramework;
using GameFramework.Runner;
using GameFramework.Physics;
using VContainer;
using LOP.Event.LOPRunner.Update;
using GameFramework.Netcode;

namespace LOP
{
    [SceneInjectMonoBehaviour]
    public class LOPRunner : RunnerBase
    {
        [Inject] private GameFramework.World.IWorld world;

        [Inject] private IMapLoader mapLoader;
        [Inject] private IGameRuleSystem gameRuleSystem;
        [Inject] private INetworkTime networkTimeSource;
        [Inject] private IRoomDataStore roomDataStore;
        [Inject] private LOP.MasterData.LOPMasterData masterData;

        // Slice 5-B: 파이프라인 스텝 — 순서대로 직접 호출(넷코드 순서 불변식이 코드에 명시).
        [Inject] private MatchStartSystem matchStartSystem;
        [Inject] private ServerInputSystem serverInputSystem;
        [Inject] private PhysicsSimulationSystem physicsSimulationSystem;
        [Inject] private DeathResolveSystem deathResolveSystem;
        [Inject] private WorldEventDrainSystem worldEventDrainSystem;
        [Inject] private InputTimingFeedbackSystem inputTimingFeedbackSystem;
        [Inject] private EntitySnapshotBroadcastSystem entitySnapshotBroadcastSystem;
        [Inject] private UserEntitySnapshotSystem userEntitySnapshotSystem;
        [Inject] private DespawnFlushSystem despawnFlushSystem;

        private readonly Restorer restorer = new Restorer();

        //  50Hz × 300초.
        private const long MatchDurationTicks = 15000;

        public override async Task InitializeAsync()
        {
            gameState = RunnerState.Initializing;

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
            var mapLoadTask = mapLoader.LoadAsync(ResolveMapScenePath());

            await base.InitializeAsync();

            networkTime = networkTimeSource;
            ((LOPTickUpdater)tickUpdater).networkTime = networkTimeSource;

            await mapLoadTask;

            gameRuleSystem.Initialize();

            gameState = RunnerState.Initialized;
        }

        /// <summary>이 판에서 로드할 맵 씬. 매치의 이번 라운드가 가리키는 맵에서 온다.</summary>
        private string ResolveMapScenePath()
        {
            var rounds = roomDataStore.match?.rounds;
            var roundIndex = MatchSceneResolver.CurrentRoundIndex(rounds?.Length ?? 0);
            var round = rounds[roundIndex];
            var map = MatchSceneResolver.RequireRow(
                "TbMap", round.mapId, masterData.Tables.TbMap.GetOrDefault(round.mapId));

            return MatchSceneResolver.RequireScenePath("TbMap", round.mapId, map.ScenePath);
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

            gameState = RunnerState.Playing;
        }

        public override void Stop()
        {
            base.Stop();

            gameState = RunnerState.Paused;
        }

        private void LateUpdate()
        {
            //  끝나는 길이 둘이다: 룰이 끝났다고 하거나(판치기 라운드 등), 제한 시간이 지나거나.
            //  제한 시간은 방이 부팅된 때가 아니라 출발한 때부터 잰다 — 부팅 기준이면 참가자를
            //  기다리는 동안 판이 시작도 못 하고 끝난다(로컬 대기 상한이 600초라 특히).
            if (initialized
                && (gameRuleSystem.IsMatchOver
                    || (matchStartSystem.Phase == MatchPhase.InProgress
                        && tickUpdater.tick - matchStartSystem.StartTick > MatchDurationTicks)))
            {
                EndMatch();
            }
        }

        /// <summary>매치 종료 진입점. 종료 판정은 서버 권위이고, 클라는 통보를 받아 같은 이름의 메서드로 들어온다.</summary>
        public void EndMatch()
        {
            //  타이머가 매 프레임 부르므로 첫 호출만 유효해야 한다. 아래 등수 산출이 그때마다
            //  다시 도는 것을 막는다(예전엔 gameState 세터의 동일값 가드에 우연히 기대고 있었다).
            if (gameState == RunnerState.GameOver)
            {
                return;
            }

            //  등수는 지금 뽑는다 — 게임이 아직 살아 있을 때만 알 수 있는 값이라(엔티티·점수),
            //  방이 닫히는 시점에는 이미 늦다. 보고는 LOPRoom이 방을 닫기 전에 한다.
            roomDataStore.outcome = gameRuleSystem.ResolveOutcome();

            matchStartSystem.Finish();

            gameState = RunnerState.GameOver;
        }

        protected override void UpdateRunner()
        {
            RunPhase<Begin>(tickUpdater.tick, (float)tickUpdater.deltaTime);
            //  이번 틱이 출발틱인지가 먼저 정해져야 월드가 그걸 보고 굴린다.
            matchStartSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
            serverInputSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
            world.Tick(tickUpdater.tick, (float)tickUpdater.interval);
            physicsSimulationSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
            deathResolveSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
            worldEventDrainSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
            inputTimingFeedbackSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
            RunPhase<End>(tickUpdater.tick, (float)tickUpdater.deltaTime);
            entitySnapshotBroadcastSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
            userEntitySnapshotSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
            despawnFlushSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
        }
    }
}
