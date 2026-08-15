using VContainer;

namespace LOP
{
    /// <summary>FlapWang 덩어리(서버) — 캐릭터 월드와 캐릭터 룰을 쓴다.</summary>
    public class FlapWangLifetimeScope : GameLifetimeScope
    {
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.IWorld, LOPWorld>(Lifetime.Singleton);
            builder.Register<ICharacterCreator, CharacterCreator>(Lifetime.Singleton);
            // 진단 도구(DebugEnemySpawner)가 구체 타입을 주입받으므로 둘 다로 등록한다.
            builder.Register<FlapWangRuleSystem>(Lifetime.Singleton).AsSelf().As<IGameRuleSystem>();
        }
    }
}
