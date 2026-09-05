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
            // 맵 씬의 WindVolume 마커가 맵 로드 시 여기에 자기를 넣는다.
            builder.Register<WindField>(Lifetime.Singleton);
            builder.Register<WindDriftSystem>(Lifetime.Singleton);
            //  아래로 떨어지므로 y가 작아지는 방향이다. 마커가 없는 맵을 위해 지면 높이를 폴백으로 준다.
            builder.Register(c => new FinishLineBounds(
                FinishAxis.Y, c.Resolve<SkydiveConfig>().GroundY), Lifetime.Singleton);
            builder.Register(c => new FinishSystem(
                c.Resolve<FinishLineBounds>(), FinishAxis.Y, increasing: false), Lifetime.Singleton);
            builder.Register<GameFramework.World.IWorld>(c => new SkydiveWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<SkydiveMoveSystem>(),
                c.Resolve<StaminaSystem>(),
                c.Resolve<WindDriftSystem>(),
                c.Resolve<FinishSystem>(),
                c.Resolve<WindField>(),
                c.Resolve<SkydiveConfig>(),
                c.Resolve<GameFramework.Physics.ICollisionQuery>(),
                // 클라와 같은 마스크여야 예측이 권위와 갈리지 않는다.
                UnityEngine.LayerMask.GetMask("Default")), Lifetime.Singleton);

            builder.Register<ICharacterCreator, SkydivePlayerCreator>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, SkydiveRuleSystem>(Lifetime.Singleton);

            //  맵 씬의 LaserVolume 마커가 맵 로드 시 여기에 자기를 넣는다.
            builder.Register<LaserField>(Lifetime.Singleton);
            builder.Register(c => new SkydiveLaserSystem(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<LaserField>(),
                c.Resolve<SkydiveConfig>(),
                SkydiveCourseLayout.ShelfYs,
                SkydiveCourseLayout.SpawnY,
                SkydiveCourseLayout.RespawnPoints), Lifetime.Singleton);

            builder.Register<FinishTrackingSystem>(Lifetime.Singleton);
            // 도착 감시를 러너의 End 페이즈에 문다. 시스템이 스스로 IRunner를 잡으면
            // 러너→룰→도착→러너로 고리가 생겨 컨테이너가 아예 안 만들어진다.
            // 레이저를 결승선보다 먼저 물려, 맞은 그 틱에 결승 통과로도 잡히지 않게 한다.
            builder.RegisterBuildCallback(container =>
            {
                //  레이저를 결승보다 먼저 문다 — 결승선에 닿는 그 틱에 맞았다면 완주가 아니라 피격이다.
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<SkydiveLaserSystem>());
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<FinishTrackingSystem>());
            });
        }
    }
}
