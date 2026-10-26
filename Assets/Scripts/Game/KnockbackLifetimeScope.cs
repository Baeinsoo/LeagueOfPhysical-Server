using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>Knockback 덩어리(서버) — 칸 바닥 위에서 걷다 떨어지면 탈락하는 몸들.</summary>
    public class KnockbackLifetimeScope : GameLifetimeScope
    {
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<KnockbackConfigProvider>(Lifetime.Singleton);
            builder.Register<KnockbackConfig>(c => c.Resolve<KnockbackConfigProvider>().Get(), Lifetime.Singleton);

            // 바닥은 월드와 이동 시스템이 같은 한 개를 나눠 쓴다.
            builder.Register<KnockbackFloor>(c => c.Resolve<KnockbackConfig>().CreateFloor(), Lifetime.Singleton);
            builder.Register<KnockbackMoveSystem>(c => c.Resolve<KnockbackConfig>().CreateMoveSystem(
                c.Resolve<KnockbackFloor>(),
                c.Resolve<GameFramework.Physics.ICollisionQuery>(),
                LayerMask.GetMask("Default", "Character")), Lifetime.Singleton);

            builder.Register(c => new KnockbackFireSystem(c.Resolve<KnockbackConfig>()), Lifetime.Singleton);
            builder.Register(c => new BodyCollisionSystem(
                BodySizes.CharacterRadius, BodySizes.CharacterHeight,
                c.Resolve<KnockbackConfig>().BodyRestitution, new UnityEngine.Vector3(1, 0, 1)), Lifetime.Singleton);

            builder.Register<KnockbackWorld>(c => new KnockbackWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<KnockbackMoveSystem>(),
                c.Resolve<GameFramework.World.IMotionBridge>(),
                c.Resolve<KnockbackFloor>(),
                c.Resolve<KnockbackFireSystem>(),
                c.Resolve<BodyCollisionSystem>(),
                c.Resolve<KnockbackConfig>()), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            builder.Register<ICharacterCreator, KnockbackPlayerCreator>(Lifetime.Singleton);
            builder.Register<KnockbackMatchState>(Lifetime.Singleton);
            builder.Register(c => new KnockbackFallSystem(
                c.Resolve<KnockbackMatchState>(),
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.IWorld>(),
                c.Resolve<KnockbackConfig>(),
                c.Resolve<EntitySpawner>().Despawn), Lifetime.Singleton);
            builder.RegisterInstance<System.Func<int>>(KnockbackTestBots.ReadFillTo);
            builder.Register<IGameRuleSystem, KnockbackRuleSystem>(Lifetime.Singleton);

            builder.Register(c => new KnockbackHitSystem(
                c.Resolve<KnockbackWorld>(),
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<KnockbackMatchState>(),
                c.Resolve<KnockbackConfig>(),
                c.Resolve<GameFramework.World.IEventSink>()), Lifetime.Singleton);
            builder.Register(c => new KnockbackZoneSystem(
                c.Resolve<KnockbackMatchState>(),
                c.Resolve<KnockbackConfig>(),
                c.Resolve<KnockbackWorld>(),
                new System.Random(unchecked((int)c.Resolve<IMatchSeed>().Value))), Lifetime.Singleton);
            builder.Register<KnockbackStateBroadcastSystem>(Lifetime.Singleton);
            builder.Register(c => new KnockbackBotSystem(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<KnockbackWorld>(),
                c.Resolve<KnockbackMatchState>(),
                c.Resolve<KnockbackConfig>(),
                c.Resolve<InputBufferSystem>(),
                c.Resolve<IMatchSeed>()), Lifetime.Singleton);

            // 시스템이 러너를 직접 잡으면 순환이 생겨 빌드 콜백에서 붙인다(DodgeLifetimeScope와 같다).
            builder.RegisterBuildCallback(container =>
            {
                //  봇 입력은 사람 입력이 처리되기 전(Begin)에 이번 틱 번호로 넣어야 같은 틱에 쓰인다.
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.Begin>(container.Resolve<KnockbackBotSystem>());
                //  순서가 뜻이다: 맞음 → 떨어짐/사라짐 → 원 공개 → 방송.
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(container.Resolve<KnockbackHitSystem>());
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(container.Resolve<KnockbackFallSystem>());
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(container.Resolve<KnockbackZoneSystem>());
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(container.Resolve<KnockbackStateBroadcastSystem>());
            });
        }
    }
}
