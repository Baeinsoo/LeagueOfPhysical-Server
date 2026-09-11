using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>Archery 덩어리(서버) — 제자리에 선 사수들, 날아가는 화살.</summary>
    public class ArcheryLifetimeScope : GameLifetimeScope
    {
        // 50Hz. 시뮬이 당긴 시간을 초로 환산할 때 쓴다.
        private const float TickInterval = 0.02f;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<ArcheryAimSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.IWorld>(c => new ArcheryWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<ArcheryAimSystem>(),
                TickInterval), Lifetime.Singleton);

            builder.Register<ICharacterCreator, ArcheryPlayerCreator>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, ArcheryRuleSystem>(Lifetime.Singleton);
        }
    }
}
