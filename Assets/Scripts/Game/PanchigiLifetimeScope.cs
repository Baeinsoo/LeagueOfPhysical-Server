using VContainer;

namespace LOP
{
    /// <summary>판치기 덩어리(서버) — 빈 월드·아바타 없는 플레이어·턴 룰.</summary>
    public class PanchigiLifetimeScope : GameLifetimeScope
    {
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.IWorld>(c => new PanchigiWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>()), Lifetime.Singleton);
            builder.Register<ICharacterCreator, PanchigiPlayerCreator>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, PanchigiRuleSystem>(Lifetime.Singleton);
        }
    }
}
