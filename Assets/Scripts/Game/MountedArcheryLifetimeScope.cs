using VContainer;

namespace LOP
{
    /// <summary>MountedArchery 덩어리(서버) — 말 위에서 달리며 쏘는 기수들.</summary>
    public class MountedArcheryLifetimeScope : GameLifetimeScope
    {
        // 50Hz.
        private const float TickInterval = 0.02f;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<MountedArcheryConfigProvider>(Lifetime.Singleton);
            builder.Register<MountedArcheryConfig>(
                c => c.Resolve<MountedArcheryConfigProvider>().Get(), Lifetime.Singleton);

            builder.Register(c => new MountSystem(c.Resolve<MountedArcheryConfig>().Mount), Lifetime.Singleton);
            builder.Register(c =>
            {
                var cfg = c.Resolve<MountedArcheryConfig>();
                return new ArcheryAimSystem(cfg.ToBowConfig(), cfg.RiderEyeHeight, turnBodyToAim: false);
            }, Lifetime.Singleton);

            builder.Register<MountedArcheryWorld>(c => new MountedArcheryWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<ArcheryAimSystem>(),
                c.Resolve<MountSystem>(),
                c.Resolve<KinematicMoveSystem>(),
                c.Resolve<GameFramework.World.IMotionBridge>(),
                TickInterval), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            builder.Register<ICharacterCreator, MountedArcheryPlayerCreator>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, MountedArcheryRuleSystem>(Lifetime.Singleton);
        }
    }
}
