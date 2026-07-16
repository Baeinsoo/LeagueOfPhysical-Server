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
        [SerializeField, FormerlySerializedAs("gameEngine")] private LOPRunner runner;
        [SerializeField] private LOPEntityManager entityManager;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.EntityRegistry>(Lifetime.Singleton);
            builder.Register<GameFramework.World.WorldEventBuffer>(Lifetime.Singleton);
            builder.Register<GameFramework.World.HealthSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.LevelSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.StatsSystem>(Lifetime.Singleton);
            builder.Register<MovementSystem>(Lifetime.Singleton);
            builder.Register<MotionContributionSystem>(Lifetime.Singleton);
            builder.Register<InputBufferSystem>(Lifetime.Singleton);
            builder.Register<StatusEffectSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.ManaSystem>(Lifetime.Singleton);
            builder.Register<AbilitySystem>(Lifetime.Singleton);
            builder.Register<StatusEffectDataProvider>(Lifetime.Singleton);
            builder.Register<AbilityDataProvider>(Lifetime.Singleton);
            builder.Register<AbilityActivator>(Lifetime.Singleton);
            builder.Register<MatchSeed>(Lifetime.Singleton).AsSelf().As<IMatchSeed>();

            // effect 실행 — executor가 타입별 핸들러로 디스패치. AbilitySystem이 Active 창에서 구동.
            // (entity manager는 아래 RegisterComponent(entityManager).As<IEntityManager>()로 이미 등록 → 핸들러가 주입받음.)
            builder.Register<AbilityEffectExecutor>(Lifetime.Singleton);
            builder.Register<IAbilityEffectHandler>(c => new StatusEffectApplyEffectHandler(
                c.Resolve<StatusEffectSystem>(),
                id => c.Resolve<StatusEffectDataProvider>().Get(id)), Lifetime.Singleton);
            // DamageEffectHandler = 서버 전용 등록. 클라엔 미등록이라 executor가 DamageEffect를 무시 → 데미지 서버권위.
            // 구체 타입으로 등록(.As) — Func 등록은 ImplementationType이 IAbilityEffectHandler라 다른 Func 핸들러와 충돌.
            builder.Register<DamageEffectHandler>(Lifetime.Singleton).As<IAbilityEffectHandler>();
            builder.Register<KnockbackEffectHandler>(Lifetime.Singleton).As<IAbilityEffectHandler>();
            builder.Register<GameFramework.World.IEventSink, WorldEventSink>(Lifetime.Singleton);
            builder.Register<GameFramework.World.IWorld, LOPWorld>(Lifetime.Singleton);
            builder.Register<DeathCascadeSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.IPhysicsSimulator, GameFramework.UnityPhysicsSimulator>(Lifetime.Singleton);
            builder.Register<GameFramework.ICollisionQuery, GameFramework.UnityCollisionQuery>(Lifetime.Singleton);
            builder.Register<GameFramework.IOverlapQuery, LOPOverlapQuery>(Lifetime.Singleton);
            // 클라와 동일: 캐릭터를 벽으로(sweep에 Character 포함) + 겹치면 풀 밀어내기(1.0).
            // 클·서 같은 충돌이라야 예측이 맞아 recon이 작다.
            builder.Register<KinematicMoveSystem>(c => new KinematicMoveSystem(
                c.Resolve<GameFramework.ICollisionQuery>(), LayerMask.GetMask("Default", "Character")), Lifetime.Singleton);
            builder.Register<GameFramework.World.IMotionBridge>(_ => new MotionBridge(
                LayerMask.GetMask("Default"), LayerMask.GetMask("Character"), 1f), Lifetime.Singleton);
            builder.Register<GameFramework.IRandom, GameFramework.UnityRandom>(Lifetime.Singleton);
            builder.Register<GameFramework.IMapLoader, AddressablesMapLoader>(Lifetime.Singleton);

            // runner은 게임 서비스에 의존하므로 부모(Room)가 아닌 이 컨테이너에서 주입돼야 한다.
            builder.RegisterComponent(runner).As<IRunner>();
            builder.RegisterComponent(entityManager).As<IEntityManager>();
            // GameRuleSystem이 sim 서비스로 쓰는 ITickUpdater (runner의 형제 컴포넌트). 호스트 역참조를 피하기 위해 직접 등록.
            builder.Register<ITickUpdater>(_ => runner.GetComponent<ITickUpdater>(), Lifetime.Singleton);

            // 메시지 핸들러: 컨테이너 엔트리포인트로 자기 구독 생명주기를 스스로 관리(스코프가 Initialize/Dispose 구동).
            builder.RegisterEntryPoint<GameInfoMessageHandler>();
            builder.RegisterEntryPoint<GameEntityMessageHandler>();
            builder.RegisterEntryPoint<GameInputMessageHandler>();

            builder.Register<LOPCombatSystem>(Lifetime.Singleton);
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
