using VContainer;

namespace LOP
{
    /// <summary>Flappy Race 덩어리(서버) — 새 월드·새 생성기·레이스 룰.</summary>
    public class FlappyRaceLifetimeScope : GameLifetimeScope
    {
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<FlappyConfigProvider>(Lifetime.Singleton);
            builder.Register<FlappyConfig>(c => c.Resolve<FlappyConfigProvider>().Get(), Lifetime.Singleton);

            builder.Register<GameFramework.World.IWorld, FlappyWorld>(Lifetime.Singleton);
            builder.Register<ICharacterCreator, FlappyBirdCreator>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, FlappyRaceRuleSystem>(Lifetime.Singleton);
        }
    }
}
