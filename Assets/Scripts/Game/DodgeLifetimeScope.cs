using VContainer;

namespace LOP
{
    /// <summary>Dodge 덩어리(서버) — 플랩왕의 캐릭터 월드, 맵 자리에 세우는 룰.</summary>
    public class DodgeLifetimeScope : GameLifetimeScope
    {
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.IWorld, LOPWorld>(Lifetime.Singleton);
            builder.Register<ICharacterCreator, CharacterCreator>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, DodgeRuleSystem>(Lifetime.Singleton);
        }
    }
}
