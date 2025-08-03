using GameFramework;
using LOP.Event.Entity;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        private double lastEnemySpawnTime;

        public async Task InitializeAsync()
        {
            EventBus.Default.Subscribe<EntityDeath>(EventTopic.Entity, entityDeath =>
            {
                DespawnEntity(entityDeath.victimId);

                SpawnExpMarble(entityDeath.position);
            });

            EventBus.Default.Subscribe<ItemTouch>(EventTopic.Entity, itemTouch =>
            {
                if (gameEngine.entityManager.GetEntity(itemTouch.itemId) != null)
                {
                    DespawnEntity(itemTouch.itemId);

                    LOPEntity toucher = gameEngine.entityManager.GetEntity<LOPEntity>(itemTouch.toucherId);
                    toucher.GetEntityComponent<LevelComponent>().AddExperience(10);
                }
            });

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

                int random = UnityEngine.Random.Range(0, 3);
                string visualId = "";
                string characterCode = "";
                switch (random)
                {
                    case 0:
                        visualId = "Assets/Art/Characters/Knight/Knight.prefab";
                        characterCode = "character_001";
                        break;
                    case 1:
                        visualId = "Assets/Art/Characters/Archer/Archer.prefab";
                        characterCode = "monster_002";
                        break;
                    case 2:
                        visualId = "Assets/Art/Characters/Necromancer/Necromancer.prefab";
                        characterCode = "monster_001";
                        break;
                }

                CharacterCreationData data = new CharacterCreationData
                {
                    userId = playerId,
                    entityId = gameEngine.entityManager.GenerateEntityId(),
                    visualId = visualId,
                    characterCode = characterCode,
                    position = Vector3.right * i * 5,
                    rotation = Vector3.zero,
                    velocity = Vector3.zero,
                    maxHP = 100000,
                    currentHP = 100000,
                    maxMP = 1000,
                    currentMP = 1000,
                    level = 1,
                    currentExp = 0,
                };

                LOPEntity entity = gameEngine.entityManager.CreateEntity<LOPEntity, CharacterCreationData>(data);
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
            if (gameEngine.tickUpdater.elapsedTime - lastEnemySpawnTime >= 10f)
            {
                if (gameEngine.entityManager.GetEntities().Count() < 100)
                {
                    SpawnEnemies(10);
                    lastEnemySpawnTime = gameEngine.tickUpdater.elapsedTime;
                }
            }

            if (gameEngine.tickUpdater.elapsedTime > 60 * 5)
            {
                gameState = GameOver.State;
            }
        }

        #region Spawn
        private void SpawnEnemies(int count)
        {
            for (int i = 0; i < count; i++)
            {
                SpawnEnemy(GetRandomSpawnPosition());
            }
        }

        private void SpawnEnemy(Vector3 position)
        {
            int random = UnityEngine.Random.Range(0, 2);
            string visualId = "";
            string characterCode = "";
            switch (random)
            {
                case 0:
                    visualId = "Assets/Art/Characters/Archer/Archer.prefab";
                    characterCode = "monster_002";
                    break;

                case 1:
                    visualId = "Assets/Art/Characters/Necromancer/Necromancer.prefab";
                    characterCode = "monster_001";
                    break;
            }

            CharacterCreationData data = new CharacterCreationData
            {
                userId = "",
                entityId = gameEngine.entityManager.GenerateEntityId(),
                visualId = visualId,
                characterCode = characterCode,
                position = position,
                rotation = Vector3.zero,
                velocity = Vector3.zero,
                maxHP = 100,
                currentHP = 100,
                maxMP = 100,
                currentMP = 100,
                level = 1,
                currentExp = 0,
            };

            LOPEntity entity = gameEngine.entityManager.CreateEntity<LOPEntity, CharacterCreationData>(data);

            EntitySpawnToC entitySpawnToC = new EntitySpawnToC
            {
                EntityCreationData = EntityCreationDataFactory.Create(entity),
            };

            ISessionManager sessionManager = SceneLifetimeScope.Resolve<ISessionManager>();
            foreach (var session in sessionManager.GetAllSessions().OrEmpty())
            {
                session.Send(entitySpawnToC);
            }
        }

        public void SpawnExpMarble(Vector3 position)
        {
            string visualId = "Assets/Art/Items/ExpMarble/ExpMarble.prefab";
            string itemCode = "exp_marble";

            ItemCreationData data = new ItemCreationData
            {
                entityId = gameEngine.entityManager.GenerateEntityId(),
                visualId = visualId,
                itemCode = itemCode,
                position = position,
                rotation = Vector3.zero,
                velocity = Vector3.zero,
            };

            LOPEntity entity = gameEngine.entityManager.CreateEntity<LOPEntity, ItemCreationData>(data);

            EntitySpawnToC entitySpawnToC = new EntitySpawnToC
            {
                EntityCreationData = EntityCreationDataFactory.Create(entity),
            };

            ISessionManager sessionManager = SceneLifetimeScope.Resolve<ISessionManager>();
            foreach (var session in sessionManager.GetAllSessions().OrEmpty())
            {
                session.Send(entitySpawnToC);
            }
        }

        private Vector3 GetRandomSpawnPosition()
        {
            return new Vector3(UnityEngine.Random.Range(-20f, 20f), 0, UnityEngine.Random.Range(-20f, 20f));
        }

        private void DespawnEntity(string entityId)
        {
            gameEngine.entityManager.DeleteEntityById(entityId);

            EntityDespawnToC entityDespawnToC = new EntityDespawnToC
            {
                EntityId = entityId,
            };

            ISessionManager sessionManager = SceneLifetimeScope.Resolve<ISessionManager>();
            foreach (var session in sessionManager.GetAllSessions().OrEmpty())
            {
                session.Send(entityDespawnToC);
            }
        }
        #endregion
    }
}
