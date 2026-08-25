using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>판치기 덩어리(서버) — 빈 월드·아바타 없는 플레이어·턴 룰.</summary>
    public class PanchigiLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private PanchigiBoard board;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.IWorld>(c => new PanchigiWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>()), Lifetime.Singleton);
            builder.Register<ICharacterCreator, PanchigiPlayerCreator>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, PanchigiRuleSystem>(Lifetime.Singleton);
            builder.Register<PanchigiTurnSystem>(Lifetime.Singleton);
            builder.RegisterComponent(board);

            // 타격 수신·검증·임펄스 — 판치기만 있는 흐름이라 공통 GameplayInstaller가 아니라 여기에 둔다.
            builder.RegisterEntryPoint<PanchigiStrikeMessageHandler>();
        }
    }
}
