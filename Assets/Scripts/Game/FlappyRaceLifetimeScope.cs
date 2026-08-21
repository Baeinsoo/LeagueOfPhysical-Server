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
            builder.Register<FlappyBodyCollisionSystem>(Lifetime.Singleton);
            // sweep이 볼 것은 맵 지오메트리뿐이다 — 새끼리는 물리엔진이 아니라 우리 계산으로 민다.
            builder.Register<GameFramework.World.IWorld>(c => new FlappyWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<FlappyMoveSystem>(),
                c.Resolve<FlappyBodyCollisionSystem>(),
                c.Resolve<GameFramework.Physics.ICollisionQuery>(),
                c.Resolve<GameFramework.World.IMotionBridge>(),
                c.Resolve<FlappyConfig>(),
                LayerMask.GetMask("Default")), Lifetime.Singleton);
            builder.Register<ICharacterCreator, FlappyBirdCreator>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, FlappyRaceRuleSystem>(Lifetime.Singleton);
        }
    }
}
