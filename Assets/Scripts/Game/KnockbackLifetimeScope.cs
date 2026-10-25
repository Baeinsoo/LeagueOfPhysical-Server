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

            builder.Register<KnockbackWorld>(c => new KnockbackWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<KnockbackMoveSystem>(),
                c.Resolve<GameFramework.World.IMotionBridge>(),
                c.Resolve<KnockbackFloor>()), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            builder.Register<ICharacterCreator, KnockbackPlayerCreator>(Lifetime.Singleton);
            builder.Register<KnockbackMatchState>(Lifetime.Singleton);
            builder.Register(c => new KnockbackFallSystem(
                c.Resolve<KnockbackMatchState>(),
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<KnockbackConfig>(),
                c.Resolve<EntitySpawner>().Despawn), Lifetime.Singleton);
            builder.Register<IGameRuleSystem, KnockbackRuleSystem>(Lifetime.Singleton);

            // 시스템이 러너를 직접 잡으면 순환이 생겨 빌드 콜백에서 붙인다(DodgeLifetimeScope와 같다).
            builder.RegisterBuildCallback(container =>
            {
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(container.Resolve<KnockbackFallSystem>());
            });
        }
    }
}
