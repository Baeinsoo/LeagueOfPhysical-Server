using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 게임 덩어리가 게임 종류와 무관하게 공통으로 쓰는 등록(서버).
    /// 게임마다 갈리는 것(월드·플레이어 몸 생성기·룰)은 각 게임 스코프가 따로 넣는다.
    /// </summary>
    public class GameplayInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
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
            builder.Register<CharacterLoadoutProvider>(Lifetime.Singleton);
            // 마스터데이터 조회만 사이드에서 넣어 준다(클·서 패키지가 서로 다름).
            builder.Register(c => new AbilityActivator(
                c.Resolve<AbilitySystem>(),
                id => c.Resolve<AbilityDataProvider>().TryGet(id, out var data) ? data : (AbilityData?)null,
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>()), Lifetime.Singleton);
            builder.Register<MatchSeed>(Lifetime.Singleton).AsSelf().As<IMatchSeed>();

            // effect 실행 — executor가 타입별 핸들러로 디스패치. AbilitySystem이 Active 창에서 구동.
            builder.Register<AbilityEffectExecutor>(Lifetime.Singleton);
            builder.Register<IAbilityEffectHandler>(c => new StatusEffectApplyEffectHandler(
                c.Resolve<StatusEffectSystem>(),
                id => c.Resolve<StatusEffectDataProvider>().Get(id),
                c.Resolve<GameFramework.World.EntityRegistry>()), Lifetime.Singleton);
            // DamageEffectHandler = 서버 전용 등록. 클라엔 미등록이라 executor가 DamageEffect를 무시 → 데미지 서버권위.
            // 구체 타입으로 등록(.As) — Func 등록은 ImplementationType이 IAbilityEffectHandler라 다른 Func 핸들러와 충돌.
            builder.Register<DamageEffectHandler>(Lifetime.Singleton).As<IAbilityEffectHandler>();
            builder.Register<KnockbackEffectHandler>(Lifetime.Singleton).As<IAbilityEffectHandler>();
            builder.Register<GameFramework.World.IEventSink, WorldEventSink>(Lifetime.Singleton);
            builder.Register<DeathCascadeSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.Physics.IPhysicsSimulator, GameFramework.Physics.UnityPhysicsSimulator>(Lifetime.Singleton);
            builder.Register<GameFramework.Physics.ICollisionQuery, GameFramework.Physics.UnityCollisionQuery>(Lifetime.Singleton);
            builder.Register<GameFramework.Physics.IOverlapQuery, LOPOverlapQuery>(Lifetime.Singleton);
            // 클라와 동일: 캐릭터를 벽으로(sweep에 Character 포함) + 겹치면 풀 밀어내기(1.0).
            // 클·서 같은 충돌이라야 예측이 맞아 recon이 작다.
            builder.Register<KinematicMoveSystem>(c => new KinematicMoveSystem(
                c.Resolve<GameFramework.Physics.ICollisionQuery>(), LayerMask.GetMask("Default", "Character")), Lifetime.Singleton);
            builder.Register<GameFramework.World.IMotionBridge>(_ => new MotionBridge(
                LayerMask.GetMask("Default"), LayerMask.GetMask("Character"), 1f), Lifetime.Singleton);
            builder.Register<GameFramework.Rng.IRandom, GameFramework.Rng.UnityRandom>(Lifetime.Singleton);
            builder.Register<GameFramework.Runner.IMapLoader, AddressablesMapLoader>(Lifetime.Singleton);
            builder.Register<GameFramework.Netcode.INetworkTime, MirrorNetworkTime>(Lifetime.Singleton);

            // 메시지 핸들러: 컨테이너 엔트리포인트로 자기 구독 생명주기를 스스로 관리(스코프가 Initialize/Dispose 구동).
            builder.RegisterEntryPoint<GameInfoMessageHandler>();
            builder.RegisterEntryPoint<GameEntityMessageHandler>();
            builder.RegisterEntryPoint<GameInputMessageHandler>();
            builder.RegisterEntryPoint<EntityBinder>();   // 서버 뷰 스포너(EntityCreated/EntityDestroyed 반응)

            builder.Register<CombatConfigProvider>(Lifetime.Singleton);
            builder.Register<CombatConfig>(c => c.Resolve<CombatConfigProvider>().Get(), Lifetime.Singleton);
            builder.Register<LOPCombatSystem>(Lifetime.Singleton);
            builder.Register<ItemCreator>(Lifetime.Singleton);
            // PanchigiCoinCreator는 이름은 판치기 전용이지만 등록은 여기 공통 자리다 — EntitySpawner가
            // 모든 게임 스코프에서 생성자로 직접 물기 때문에(ItemCreator와 같은 방식), 판치기가 아닌
            // 게임 스코프(FlapWang·FlappyRace)에도 이게 없으면 EntitySpawner 자체를 못 만든다.
            builder.Register<PanchigiCoinCreator>(Lifetime.Singleton);
            builder.Register<EntitySpawner>(Lifetime.Singleton);
            builder.Register<ActorRegistry>(Lifetime.Singleton);
            builder.Register<IEntityCreationDataCreator, CharacterCreationDataCreator>(Lifetime.Singleton);
            builder.Register<IEntityCreationDataCreator, ItemCreationDataCreator>(Lifetime.Singleton);
            builder.Register<IEntityCreationDataCreator, CoinCreationDataCreator>(Lifetime.Singleton);
            builder.Register<IEntityCreationDataFactory, EntityCreationDataFactory>(Lifetime.Singleton);

            // Slice 5-B: LOPRunner.UpdateRunner 인라인 파이프라인 스텝 → ITickSystem 추출(god-object 해체).
            builder.Register<ServerInputSystem>(Lifetime.Singleton);
            builder.Register<PhysicsSimulationSystem>(Lifetime.Singleton);
            builder.Register<DeathResolveSystem>(Lifetime.Singleton);
            builder.Register<WorldEventDrainSystem>(Lifetime.Singleton);
            builder.Register<InputTimingFeedbackSystem>(Lifetime.Singleton);
            builder.Register<EntitySnapshotBroadcastSystem>(Lifetime.Singleton);
            builder.Register<UserEntitySnapshotSystem>(Lifetime.Singleton);
            builder.Register<DespawnFlushSystem>(Lifetime.Singleton);
        }
    }
}
