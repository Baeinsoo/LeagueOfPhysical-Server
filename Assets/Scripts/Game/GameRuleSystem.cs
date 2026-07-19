using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using System;
using System.Linq;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 서버 게임 룰 — 초기 플레이어 생성, 적 스폰(틱 구동), 아이템 획득 시 경험치.
    /// <para>
    /// ⚠️ 임시 위치. 게임 룰(스폰/점수/승패)의 표준 집은 시뮬(World) 시스템이다
    /// (Quantum SpawnSystem/ScoreSystem, ECS systems). 서버권위·RNG·엔티티생성을
    /// sim-호환으로 다듬어 World 시스템으로 이주하는 것은 별도 슬라이스(4c 룰 / Stage④).
    /// 여기서는 Game→Runner 합치기(③) 중 룰이 호스트(Runner) 몸통에 잘못 들어가지 않도록
    /// 분리만 한다. 호스트가 Initialize/Deinitialize로 구동한다.
    /// </para>
    /// </summary>
    public class GameRuleSystem
    {
        private readonly IRoomDataStore roomDataStore;
        private readonly ISessionManager sessionManager;
        private readonly IEntityCreationDataFactory entityCreationDataFactory;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IRandom rng;
        private readonly GameFramework.World.LevelSystem levelSystem;
        private readonly GameFramework.World.StatsSystem statsSystem;
        // 룰은 호스트(IRunner)를 역참조하지 않는다(Runner↔Rule 순환 방지). sim 서비스만 주입받는다.
        private readonly IEntityManager entityManager;
        private readonly ITickUpdater tickUpdater;
        private readonly ISubscriber<ItemTouch> itemTouchSubscriber;

        private double lastEnemySpawnTime;
        private IDisposable itemTouchSubscription;

        public GameRuleSystem(
            IRoomDataStore roomDataStore,
            ISessionManager sessionManager,
            IEntityCreationDataFactory entityCreationDataFactory,
            GameFramework.World.EntityRegistry entityRegistry,
            IRandom rng,
            GameFramework.World.LevelSystem levelSystem,
            GameFramework.World.StatsSystem statsSystem,
            IEntityManager entityManager,
            ITickUpdater tickUpdater,
            ISubscriber<ItemTouch> itemTouchSubscriber)
        {
            this.roomDataStore = roomDataStore;
            this.sessionManager = sessionManager;
            this.entityCreationDataFactory = entityCreationDataFactory;
            this.entityRegistry = entityRegistry;
            this.rng = rng;
            this.levelSystem = levelSystem;
            this.statsSystem = statsSystem;
            this.entityManager = entityManager;
            this.tickUpdater = tickUpdater;
            this.itemTouchSubscriber = itemTouchSubscriber;
        }

        public void Initialize()
        {
            itemTouchSubscription = itemTouchSubscriber.Subscribe(HandleItemTouch);
            tickUpdater.onTick += OnTick;

            CreateInitialPlayers();
        }

        public void Deinitialize()
        {
            itemTouchSubscription?.Dispose();
            tickUpdater.onTick -= OnTick;
        }

        private void OnTick(long tick)
        {
            if (tickUpdater.elapsedTime - lastEnemySpawnTime >= 10f)
            {
                if (entityManager.GetEntities().Count() < 100)
                {
                    SpawnEnemies(10);
                    lastEnemySpawnTime = tickUpdater.elapsedTime;
                }
            }
        }

        private void CreateInitialPlayers()
        {
            for (int i = 0; i < roomDataStore.match.playerList.Length; i++)
            {
                string playerId = roomDataStore.match.playerList[i];

                int random = rng.Range(0, 3);
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
                    entityId = entityManager.GenerateEntityId(),
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

                LOPActor actor = entityManager.CreateEntity<LOPActor, CharacterCreationData>(data);
            }
        }

        private void HandleItemTouch(ItemTouch itemTouch)
        {
            if (entityManager.GetEntity(itemTouch.itemId) != null)
            {
                DespawnEntity(itemTouch.itemId);

                GameFramework.World.Entity toucher = entityRegistry.Get(itemTouch.toucherId);
                GameFramework.World.Level level = toucher?.Get<GameFramework.World.Level>();
                if (level == null)
                {
                    Debug.LogWarning($"[World] HandleItemTouch: Level not found for entity {itemTouch.toucherId}");
                    return;
                }

                int gained = levelSystem.AddExperience(level, 10);
                if (gained > 0)
                {
                    GameFramework.World.Stats stats = toucher?.Get<GameFramework.World.Stats>();
                    if (stats != null)
                    {
                        statsSystem.AddUnspent(stats, gained);
                    }
                }
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
            int random = rng.Range(0, 2);
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
                entityId = entityManager.GenerateEntityId(),
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

            LOPActor actor = entityManager.CreateEntity<LOPActor, CharacterCreationData>(data);

            EntitySpawnToC entitySpawnToC = new EntitySpawnToC
            {
                EntityCreationData = entityCreationDataFactory.Create(entityRegistry.Get(actor.entityId)),
            };

            foreach (var session in sessionManager.GetAllSessions().OrEmpty())
            {
                session.Send(entitySpawnToC);
            }
        }

        private Vector3 GetRandomSpawnPosition()
        {
            return new Vector3(rng.Range(-20f, 20f), 0, rng.Range(-20f, 20f));
        }

        private void DespawnEntity(string entityId)
        {
            entityManager.DeleteEntityById(entityId);
        }
        #endregion
    }
}
