using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using System.Threading.Tasks;
using System;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace LOP
{
    public class LOPGame : MonoBehaviour, IGame
    {
        public event Action<IGameState> onGameStateChanged;

        [Inject]
        private IRoomDataStore roomDataStore;

        [Inject]
        public IGameEngine gameEngine { get; private set; }

        [Inject]
        private IEnumerable<IGameMessageHandler> gameMessageHandlers;

        private IGameState _gameState;
        public IGameState gameState
        {
            get => _gameState;
            private set
            {
                if (_gameState == value)
                {
                    return;
                }

                _gameState = value;
                onGameStateChanged?.Invoke(value);
            }
        }

        public bool initialized { get; private set; }

        private Restorer restorer = new Restorer();
        private AsyncOperationHandle<SceneInstance> handle;

        public async Task InitializeAsync()
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

            foreach (var gameMessageHandler in gameMessageHandlers.OrEmpty())
            {
                gameMessageHandler.Register();
            }

            handle = Addressables.LoadSceneAsync(/*roomDataStore.match.mapId*/"Assets/Art/Scenes/FlapWangMap.unity", LoadSceneMode.Additive);

            await gameEngine.InitializeAsync();

            await UniTask.WaitUntil(() => handle.IsDone);

            for (int i = 0; i < roomDataStore.match.playerList.Length; i++)
            {
                string playerId = roomDataStore.match.playerList[i];

                LOPEntityCreationData data = new LOPEntityCreationData
                {
                    userId = playerId,
                    entityId = gameEngine.entityManager.GenerateEntityId(),
                    visualId = "Assets/Art/Characters/Knight/Knight.prefab",
                    characterCode = "character_001",
                    position = Vector3.right * i * 5,
                    rotation = Vector3.zero,
                    velocity = Vector3.zero,
                };

                LOPEntity entity = gameEngine.entityManager.CreateEntity<LOPEntity, LOPEntityCreationData>(data);
            }

            gameState = Initialized.State;

            initialized = true;
        }

        public async Task DeinitializeAsync()
        {
            await gameEngine.DeinitializeAsync();

            foreach (var gameMessageHandler in gameMessageHandlers.OrEmpty())
            {
                gameMessageHandler.Unregister();
            }

            restorer.Dispose();

            await Addressables.UnloadSceneAsync(handle);

            initialized = false;
        }

        public void Run(long tick, double interval, double elapsedTime)
        {
            gameState = Playing.State;

            gameEngine.Run(tick, interval, elapsedTime);
        }

        private void LateUpdate()
        {
            if (gameEngine.tickUpdater.elapsedTime > 60 * 5)
            {
                gameState = GameOver.State;
            }
        }
    }
}
