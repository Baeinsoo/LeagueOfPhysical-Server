using UnityEngine;
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

            builder.Register<FlappyMoveSystem>(Lifetime.Singleton);
            builder.Register<FlappyStunSystem>(Lifetime.Singleton);
            builder.Register<FlappyDashSystem>(Lifetime.Singleton);
            //  새는 +x로 달린다. 폴백을 주지 않는다 — 마커가 없으면 룰이 Initialize에서 터뜨린다.
            builder.Register(c => new FinishLineBounds(FinishAxis.X), Lifetime.Singleton);
            builder.Register(c => new FinishSystem(
                c.Resolve<FinishLineBounds>(), FinishAxis.X, increasing: true), Lifetime.Singleton);
            // sweep이 볼 것은 맵 지오메트리뿐이다 — 새끼리는 아예 부딪히지 않는다(서로 통과한다).
            // 새의 물리 몸은 PhysicsFollower가 만들면서 무조건 Character 레이어에 둔다. 그래서 이
            // 마스크에 Character가 없는 한 새끼리는 sweep에 걸리지 않는다.
            // (겉모습 프리팹 Bird.prefab에는 콜라이더가 없어 물리에는 아예 존재하지 않는다.)
            builder.Register<GameFramework.World.IWorld>(c => new FlappyWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<FlappyMoveSystem>(),
                c.Resolve<FlappyStunSystem>(),
                c.Resolve<FlappyDashSystem>(),
                c.Resolve<FinishSystem>(),
                c.Resolve<GameFramework.Physics.ICollisionQuery>(),
                c.Resolve<GameFramework.World.IMotionBridge>(),
                LayerMask.GetMask("Default")), Lifetime.Singleton);
            builder.Register<ICharacterCreator, FlappyBirdCreator>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, FlappyRaceRuleSystem>(Lifetime.Singleton);

            //  새는 +x로 달리므로 x가 커지는 방향이다. 폴백 좌표를 주지 않는다 — 마커가 없으면
            //  룰이 Initialize에서 이미 터뜨린다(짐작해 세우면 판이 엉뚱한 데서 끝난다).
            builder.Register(c => new FinishLineTrackingSystem(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<ActorRegistry>(),
                FinishAxis.X, increasing: true), Lifetime.Singleton);
            builder.Register<FlappyChaserSystem>(Lifetime.Singleton);

            //  도착 감시를 러너의 End 페이즈에 문다. 시스템이 스스로 IRunner를 잡으면
            //  러너→룰→도착→러너로 고리가 생겨 컨테이너가 아예 안 만들어진다.
            //  추격자는 그 뒤여야 한다 — 앞에 두면 같은 틱에 결승선을 넘은 새를 잡는다.
            builder.RegisterBuildCallback(container =>
            {
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<FinishLineTrackingSystem>());
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<FlappyChaserSystem>());
            });
        }
    }
}
