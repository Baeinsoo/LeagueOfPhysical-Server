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
            // 맵 씬의 DoorVolume 마커가 맵 로드 시 여기에 자기를 넣는다.
            builder.Register<DoorField>(Lifetime.Singleton);
            builder.Register<GameFramework.World.IWorld>(c => new SkydiveWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<SkydiveMoveSystem>(),
                c.Resolve<StaminaSystem>(),
                c.Resolve<WindDriftSystem>(),
                c.Resolve<FinishSystem>(),
                c.Resolve<WindField>(),
                c.Resolve<DoorField>(),
                c.Resolve<SkydiveConfig>(),
                c.Resolve<GameFramework.Physics.ICollisionQuery>(),
                c.Resolve<GameFramework.World.IMotionBridge>(),
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

            builder.Register(c => new SkydiveDoorSystem(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<DoorField>(),
                c.Resolve<SkydiveConfig>(),
                SkydiveCourseLayout.ShelfYs,
                SkydiveCourseLayout.SpawnY,
                SkydiveCourseLayout.RespawnPoints), Lifetime.Singleton);

            builder.Register(c => new SkydiveLandingSystem(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<SkydiveConfig>(),
                SkydiveCourseLayout.ShelfYs,
                SkydiveCourseLayout.SpawnY,
                SkydiveCourseLayout.RespawnPoints), Lifetime.Singleton);

            builder.Register<FinishTrackingSystem>(Lifetime.Singleton);
            // 도착 감시를 러너의 End 페이즈에 문다. 시스템이 스스로 IRunner를 잡으면
            // 러너→룰→도착→러너로 고리가 생겨 컨테이너가 아예 안 만들어진다.
            //
            // 아래 네 시스템끼리의 등록 순서는 결승 판정에는 영향이 없다 — FinishState는
            // world.Tick(러너가 이 End 페이즈보다 먼저 부른다) 안의 SkydiveWorld.Detection이
            // 이미 확정해 두고, FinishTrackingSystem은 그 결과를 옮겨 담을 뿐 스스로 판정하지
            // 않는다(FinishSystem.cs 참고). 그래서 레이저·문·착지가 End 안에서 FinishTrackingSystem보다
            // 앞이든 뒤든 결승 통과 여부는 달라지지 않는다.
            //
            // 진짜 지켜야 하는 불변식은 "부활은 다음 틱의 world.Tick보다 먼저 끝나 있어야 한다"이다.
            // LandingImpact는 착지한 바로 그 틱에만 값이 있고, 다음 틱 이동이 자동으로 0으로 비운다
            // (SkydiveWorld.MoveBlockedByMap) — 부활이 프레임을 하나라도 건너뛰어 미뤄지면, 다음
            // world.Tick의 Detection이 이미 비어 버린 충격값과 죽은 자리(아직 안 되돌려진 위치)를
            // 보고 치명 착지를 완주로 잘못 인정한다. Update.End 안 어디에 물려 있든 이 조건은
            // 지켜진다 — 프레임을 건너 미루는 실수만 이 조건을 깬다.
            builder.RegisterBuildCallback(container =>
            {
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<SkydiveLaserSystem>());
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<SkydiveDoorSystem>());
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<SkydiveLandingSystem>());
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<FinishTrackingSystem>());
            });
        }
    }
}
