using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>Skydive 덩어리(서버) — 떨어지는 월드, 하늘에 세우는 룰.</summary>
    public class SkydiveLifetimeScope : GameLifetimeScope
    {
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<SkydiveConfigProvider>(Lifetime.Singleton);
            builder.Register<SkydiveConfig>(c => c.Resolve<SkydiveConfigProvider>().Get(), Lifetime.Singleton);

            builder.Register<SkydiveMoveSystem>(Lifetime.Singleton);
            builder.Register<StaminaSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.IWorld>(c => new SkydiveWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<SkydiveMoveSystem>(),
                c.Resolve<StaminaSystem>(),
                c.Resolve<SkydiveConfig>(),
                c.Resolve<GameFramework.Physics.ICollisionQuery>(),
                // 클라와 같은 마스크여야 예측이 권위와 갈리지 않는다.
                UnityEngine.LayerMask.GetMask("Default")), Lifetime.Singleton);

            builder.Register<ICharacterCreator, SkydivePlayerCreator>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, SkydiveRuleSystem>(Lifetime.Singleton);

            builder.Register<SkydiveFinishSystem>(Lifetime.Singleton);
            // 도착 감시를 러너의 End 페이즈에 문다. 시스템이 스스로 IRunner를 잡으면
            // 러너→룰→도착→러너로 고리가 생겨 컨테이너가 아예 안 만들어진다.
            builder.RegisterBuildCallback(container =>
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(container.Resolve<SkydiveFinishSystem>()));
        }
    }
}
