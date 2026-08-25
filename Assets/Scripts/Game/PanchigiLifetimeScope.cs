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

            //  턴 시스템을 러너의 End 페이즈에 물리는 것은 여기서 한다. 턴 시스템이 스스로
            //  IRunner를 잡으면 러너→룰→턴→러너로 고리가 생겨 컨테이너가 아예 안 만들어진다
            //  (룰은 호스트를 역참조하지 않는다 — FlapWangRuleSystem에 적힌 규칙과 같은 것).
            builder.RegisterBuildCallback(container =>
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(container.Resolve<PanchigiTurnSystem>()));
        }
    }
}
