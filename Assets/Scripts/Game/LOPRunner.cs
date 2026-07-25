using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using GameFramework;
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
        [Inject] private GameRuleSystem gameRuleSystem;
        [Inject] private INetworkTime networkTimeSource;

        // Slice 5-B: 파이프라인 스텝 — 순서대로 직접 호출(넷코드 순서 불변식이 코드에 명시).
        [Inject] private ServerInputSystem serverInputSystem;
        [Inject] private PhysicsSimulationSystem physicsSimulationSystem;
        [Inject] private DeathResolveSystem deathResolveSystem;
        [Inject] private WorldEventDrainSystem worldEventDrainSystem;
        [Inject] private InputTimingFeedbackSystem inputTimingFeedbackSystem;
        [Inject] private EntitySnapshotBroadcastSystem entitySnapshotBroadcastSystem;
        [Inject] private UserEntitySnapshotSystem userEntitySnapshotSystem;
        [Inject] private DespawnFlushSystem despawnFlushSystem;

        private const string MapId = "Assets/Art/Scenes/FlapWangMap.unity";

        private readonly Restorer restorer = new Restorer();

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
            var mapLoadTask = mapLoader.LoadAsync(MapId);

            await base.InitializeAsync();

            networkTime = networkTimeSource;
            ((LOPTickUpdater)tickUpdater).networkTime = networkTimeSource;

            await mapLoadTask;

            gameRuleSystem.Initialize();

            gameState = RunnerState.Initialized;
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
            if (initialized && tickUpdater.elapsedTime > 60 * 5)
            {
                EndMatch();
            }
        }

        /// <summary>매치 종료 진입점. 종료 판정은 서버 권위이고, 클라는 통보를 받아 같은 이름의 메서드로 들어온다.</summary>
        public void EndMatch()
        {
            gameState = RunnerState.GameOver;
        }

        public override void UpdateRunner()
        {
            RunPhase<Begin>(tickUpdater.tick, (float)tickUpdater.deltaTime);
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
