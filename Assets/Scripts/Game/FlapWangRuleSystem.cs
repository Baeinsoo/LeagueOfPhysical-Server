using GameFramework;
using GameFramework.Runner;
using GameFramework.Rng;
using LOP.Event.Entity;
using MessagePipe;
using System;
using System.Linq;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// FlapWang 룰 — 초기 플레이어 생성, 적 스폰(틱 구동), 아이템 획득 시 경험치.
    /// <para>
    /// ⚠️ 임시 위치. 게임 룰(스폰/점수/승패)의 표준 집은 시뮬(World) 시스템이다
    /// (Quantum SpawnSystem/ScoreSystem, ECS systems). 서버권위·RNG·엔티티생성을
    /// sim-호환으로 다듬어 World 시스템으로 이주하는 것은 별도 슬라이스(4c 룰 / Stage④).
    /// 여기서는 Game→Runner 합치기(③) 중 룰이 호스트(Runner) 몸통에 잘못 들어가지 않도록
    /// 분리만 한다. 호스트가 Initialize/Deinitialize로 구동한다.
    /// </para>
    /// </summary>
    public class FlapWangRuleSystem : IGameRuleSystem
    {
        private readonly IRoomDataStore roomDataStore;
        private readonly ISessionManager sessionManager;
        private readonly IEntityCreationDataFactory entityCreationDataFactory;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IRandom rng;
        private readonly GameFramework.World.LevelSystem levelSystem;
        private readonly GameFramework.World.StatsSystem statsSystem;
        // 룰은 호스트(IRunner)를 역참조하지 않는다(Runner↔Rule 순환 방지). sim 서비스만 주입받는다.
        private readonly EntitySpawner entitySpawner;
        private readonly ITickUpdater tickUpdater;
        private readonly ISubscriber<ItemTouch> itemTouchSubscriber;

        private double lastEnemySpawnTime;
        private IDisposable itemTouchSubscription;

        public FlapWangRuleSystem(
            IRoomDataStore roomDataStore,
            ISessionManager sessionManager,
            IEntityCreationDataFactory entityCreationDataFactory,
            GameFramework.World.EntityRegistry entityRegistry,
            IRandom rng,
            GameFramework.World.LevelSystem levelSystem,
            GameFramework.World.StatsSystem statsSystem,
            EntitySpawner entitySpawner,
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
            this.entitySpawner = entitySpawner;
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
                if (entityRegistry.All.Count() < 100)
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
                    entityId = entitySpawner.GenerateEntityId(),
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

                entitySpawner.Spawn(data);
            }
        }

        //  "아이템에 뭔가 닿았다"는 사실만 온다 — 그게 줍기인지는 여기서 정한다.
        //  감지기(Unity 레이어)는 도메인을 모르고, 이 판단이 게임 규칙이기 때문이다.
        private void HandleItemTouch(ItemTouch itemTouch)
        {
            if (entityRegistry.Get(itemTouch.itemId) != null)
            {
                GameFramework.World.Entity toucher = entityRegistry.Get(itemTouch.toucherId);

                //  주인이 있는 엔티티(=플레이어)만 줍는다. 몬스터나 다른 아이템이 스친 것은 아무 일도 아니다.
                if (toucher?.Has<GameFramework.World.Ownership>() != true)
                {
                    return;
                }

                DespawnEntity(itemTouch.itemId);

                GameFramework.World.Level level = toucher.Get<GameFramework.World.Level>();
                if (level == null)
                {
                    Debug.LogWarning($"[World] HandleItemTouch: Level not found for entity {itemTouch.toucherId}");
                    return;
                }

                int gained = levelSystem.AddExperience(level, 10);
                if (gained > 0)
                {
                    GameFramework.World.Stats stats = toucher.Get<GameFramework.World.Stats>();
                    if (stats != null)
                    {
                        statsSystem.AddUnspent(stats, gained);
                    }
                }
            }
        }

        #region Spawn
        // 진단(부하 실험)에서도 부른다 — 자동 스폰의 100마리 상한은 OnTick에만 있어 여기엔 안 걸린다.
        public void SpawnEnemies(int count)
        {
            for (int i = 0; i < count; i++)
            {
                SpawnEnemy(GetRandomSpawnPosition());
            }
        }

        /// <summary>진단용: 플레이어가 아닌 캐릭터를 전부 디스폰 큐에 넣는다.</summary>
        public void DespawnAllEnemies()
        {
            // Despawn은 큐에 넣기만 하고 실제 제거·클라 통보는 틱 끝의 FlushDespawns가 한다 →
            // 여기서 registry를 순회하며 불러도 순회 중 컬렉션이 바뀌지 않는다.
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }
                if (entity.Has<GameFramework.World.Ownership>())
                {
                    continue;   // Ownership이 있으면 플레이어다
                }
                entitySpawner.Despawn(entity.Id);
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
                entityId = entitySpawner.GenerateEntityId(),
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

            entitySpawner.Spawn(data);

            EntitySpawnToC entitySpawnToC = new EntitySpawnToC
            {
                EntityCreationData = entityCreationDataFactory.Create(entityRegistry.Get(data.entityId)),
            };

            foreach (var session in sessionManager.GetAllSessions())
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
            entitySpawner.Despawn(entityId);
        }
        #endregion

        //  FlapWang은 넷코드 검증용이라 순위 개념이 없다. 결과 보고 배선이 실제로 도는지
        //  확인하려고 무작위로 섞는다 — 진짜 등수는 Flappy Race가 낸다.
        public MatchOutcome ResolveOutcome()
        {
            var userIds = roomDataStore.match.playerList.ToList();

            for (int i = userIds.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (userIds[i], userIds[j]) = (userIds[j], userIds[i]);
            }

            var outcome = new MatchOutcome();
            for (int i = 0; i < userIds.Count; i++)
            {
                outcome.placements.Add(new MatchPlacement { userId = userIds[i], placement = i + 1 });
            }

            return outcome;
        }
    }
}
