using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>Skydive 덩어리(서버) — 떨어지는 월드, 하늘에 세우는 룰.</summary>
    public class SkydiveLifetimeScope : GameLifetimeScope
    {
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<SkydiveMoveSystem>(Lifetime.Singleton);
            builder.Register<StaminaSystem>(Lifetime.Singleton);

            // TODO(Task 5·6): TbSkydiveConfig를 읽는 SkydiveConfigProvider로 교체한다.
            // 지금 하드코딩하는 이유는 그 테이블이 아직 없어서다 — 값은 계획서의 시작값 그대로다.
            builder.Register<SkydiveConfig>(_ => new SkydiveConfig(
                spreadFallSpeed: 25f, diveFallSpeed: 45f, glideFallSpeed: 6f,
                spreadMoveSpeed: 12f, diveMoveSpeed: 18f, glideMoveSpeed: 14f,
                spreadTurnAccel: 22f, diveTurnAccel: 6f, glideTurnAccel: 18f,
                fallApproach: 30f, postureRate: 4f,
                bodyRadius: 0.4f, bodyHeight: 1.8f, groundY: 0f,
                staminaMax: 100f, glideDrain: 20f, groundRecover: 40f,
                emergencyGlideTime: 1f), Lifetime.Singleton);

            builder.Register<GameFramework.World.IWorld>(c => new SkydiveWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<SkydiveMoveSystem>(),
                c.Resolve<StaminaSystem>(),
                c.Resolve<SkydiveConfig>()), Lifetime.Singleton);

            builder.Register<ICharacterCreator, SkydivePlayerCreator>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, SkydiveRuleSystem>(Lifetime.Singleton);
        }
    }
}
