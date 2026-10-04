using VContainer;

namespace LOP
{
    /// <summary>Dodge 덩어리(서버) — 플랩왕의 캐릭터 월드에 위험 진행·판정·방송을 얹는다.</summary>
    public class DodgeLifetimeScope : GameLifetimeScope
    {
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.IWorld, LOPWorld>(Lifetime.Singleton);
            builder.Register<ICharacterCreator, CharacterCreator>(Lifetime.Singleton);

            builder.Register<DodgeConfigProvider>(Lifetime.Singleton);
            builder.Register<DodgeConfig>(c => c.Resolve<DodgeConfigProvider>().Get(), Lifetime.Singleton);
            builder.Register<DodgeMatchState>(Lifetime.Singleton);
            builder.Register<DodgeStageProvider>(Lifetime.Singleton);
            builder.Register<DodgeStageTable>(c => c.Resolve<DodgeStageProvider>().Get(), Lifetime.Singleton);
            builder.Register(c => new DodgeDirector(c.Resolve<IMatchSeed>().Value, c.Resolve<DodgeConfig>(),
                                                    c.Resolve<DodgeStageTable>()), Lifetime.Singleton);
            builder.Register<DodgeDirectorSystem>(Lifetime.Singleton);
            builder.Register(c => new DodgeHazardSystem(
                c.Resolve<DodgeMatchState>(),
                c.Resolve<GameFramework.World.IWorld>().EntityRegistry,
                c.Resolve<DodgeConfig>(),
                c.Resolve<EntitySpawner>().Despawn,
                (id, tick, lives) =>
                {
                    var world = c.Resolve<GameFramework.World.IWorld>();
                    var at = c.Resolve<DodgeStageTable>().At(tick, world.GameplayStartTick, c.Resolve<DodgeConfig>());
                    UnityEngine.Debug.Log(DodgeHitLog.Format(at, tick, world.GameplayStartTick, lives));
                }), Lifetime.Singleton);
            builder.Register<DodgeStateBroadcastSystem>(Lifetime.Singleton);
            builder.Register<DodgeRefereeSystem>(Lifetime.Singleton);

            builder.Register<IGameRuleSystem, DodgeRuleSystem>(Lifetime.Singleton);

            // 순서가 뜻이다: 고르기(끝난 것 빼기 포함) → 판정 → 방송. 시스템이 러너를 직접 잡으면
            // 러너→룰→시스템→러너 순환으로 컨테이너가 안 만들어진다 — 빌드 콜백에서 붙인다.
            builder.RegisterBuildCallback(container =>
            {
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(container.Resolve<DodgeRefereeSystem>());   // 심판이 먼저 선다 — 진행기가 그 자리에서 쏜다
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(container.Resolve<DodgeDirectorSystem>());
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(container.Resolve<DodgeHazardSystem>());
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(container.Resolve<DodgeStateBroadcastSystem>());
            });
        }
    }
}
