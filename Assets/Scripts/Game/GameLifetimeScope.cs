using GameFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 게임 씬의 게임 스코프. EnqueueParent(Room)로 로드되면 Room 자식으로 빌드된다.
    /// </summary>
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private LOPGame game;
        [SerializeField, FormerlySerializedAs("gameEngine")] private LOPRunner runner;
        [SerializeField] private LOPEntityManager entityManager;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.EntityRegistry>(Lifetime.Singleton);
            builder.Register<GameFramework.World.WorldEventBuffer>(Lifetime.Singleton);
            builder.Register<GameFramework.World.HealthSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.LevelSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.StatsSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.IEventSink, WorldEventSink>(Lifetime.Singleton);
            builder.Register<GameFramework.World.IWorld, LOPWorld>(Lifetime.Singleton);
            builder.Register<DeathCascadeSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.IPhysicsSimulator, GameFramework.UnityPhysicsSimulator>(Lifetime.Singleton);
            builder.Register<GameFramework.IRandom, GameFramework.UnityRandom>(Lifetime.Singleton);
            builder.Register<GameFramework.IMapLoader, AddressablesMapLoader>(Lifetime.Singleton);

            // game/runner은 게임 서비스에 의존하므로 부모(Room)가 아닌 이 컨테이너에서 주입돼야 한다.
            builder.RegisterComponent(game).As<IGame>();
            builder.RegisterComponent(runner).As<IRunner>();
            builder.RegisterComponent(entityManager).As<IEntityManager>();

            builder.Register<IGameMessageHandler, GameInfoMessageHandler>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, GameEntityMessageHandler>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, GameInputMessageHandler>(Lifetime.Transient);
            builder.Register<IGameMessageHandler, EntityBinder>(Lifetime.Transient);

            builder.Register<IActionManager, LOPActionManager>(Lifetime.Singleton);
            builder.Register<IMovementManager, LOPMovementManager>(Lifetime.Singleton);
            builder.Register<ICombatSystem, LOPCombatSystem>(Lifetime.Singleton);
            builder.Register<IEntityCreator, CharacterCreator>(Lifetime.Singleton);
            builder.Register<IEntityCreator, ItemCreator>(Lifetime.Singleton);
            builder.Register<IEntityFactory, EntityFactory>(Lifetime.Singleton);
            builder.Register<IEntityCreationDataCreator, CharacterCreationDataCreator>(Lifetime.Singleton);
            builder.Register<IEntityCreationDataCreator, ItemCreationDataCreator>(Lifetime.Singleton);
            builder.Register<IEntityCreationDataFactory, EntityCreationDataFactory>(Lifetime.Singleton);

            // 서버 게임 룰(스폰/exp/초기플레이어). ⚠️ 임시 위치 — 목적지는 World 시스템(별도 슬라이스).
            builder.Register<GameRuleSystem>(Lifetime.Singleton);

            builder.RegisterBuildCallback(container =>
            {
                container.InjectSceneObjects(gameObject.scene);
                SceneManager.sceneLoaded += OnSceneLoaded;
            });
        }

        protected override void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            base.OnDestroy();
        }

        // Factory가 additive 로드하는 맵 씬도 이 컨테이너로 주입한다.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 자기 씬은 빌드 콜백에서 이미 주입했다. (자기 씬 Awake 중 구독해 자기 sceneLoaded도 수신됨)
            if (scene == gameObject.scene)
            {
                Debug.Log($"[GameLifetimeScope] Skip re-injecting own scene '{scene.name}'; already injected in build callback.");
                return;
            }

            Container.InjectSceneObjects(scene);
        }
    }
}
