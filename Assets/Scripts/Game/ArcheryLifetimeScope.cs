using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>Archery 덩어리(서버) — 제자리에 선 사수들, 날아가는 화살.</summary>
    public class ArcheryLifetimeScope : GameLifetimeScope
    {
        // 50Hz. 시뮬이 당긴 시간을 초로 환산할 때 쓴다.
        private const float TickInterval = 0.02f;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<ArcheryConfigProvider>(Lifetime.Singleton);
            builder.Register<ArcheryConfig>(c => c.Resolve<ArcheryConfigProvider>().Get(), Lifetime.Singleton);

            //  과녁이 언제 어디 서는지를 정하는 한 곳. 명단은 매치 시작 시점의 것을 그대로 쓴다 —
            //  중간에 나간 사람이 있어도 과녁 주인이 밀리지 않게(스펙 6.2절).
            builder.Register<ArcheryCourse>(c => new ArcheryCourse(
                c.Resolve<ArcheryConfig>(),
                c.Resolve<IMatchSeed>(),
                c.Resolve<IRoomDataStore>().match.playerList,
                TickInterval), Lifetime.Singleton);

            builder.Register<ArcheryAimSystem>(Lifetime.Singleton);
            builder.Register<ArcheryWorld>(c => new ArcheryWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<ArcheryAimSystem>(),
                TickInterval), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            builder.Register<ICharacterCreator, ArcheryPlayerCreator>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, ArcheryRuleSystem>(Lifetime.Singleton);

            builder.Register<ArcheryWaveState>(Lifetime.Singleton);
            builder.Register(c => new ArcheryHitSystem(
                c.Resolve<ArcheryWorld>(),
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<ArcheryCourse>(),
                c.Resolve<ArcheryWaveState>(),
                TickInterval), Lifetime.Singleton);
            builder.Register<ArcheryStateBroadcastSystem>(Lifetime.Singleton);

            // 화살이 생긴 *뒤*에 판정해야 하므로 world.Tick 다음인 End에 문다. 그러면 여기서 쌓은
            // 사건은 이번 틱 드레인을 놓쳐 다음 틱(20ms 뒤)에 나간다 — 점수 자체는 스냅샷으로
            // 가므로 이 지연은 연출에만 걸린다.
            builder.RegisterBuildCallback(container =>
            {
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<ArcheryHitSystem>());
                //  판정 다음에 내보낸다 — 같은 틱의 결과가 그 틱에 나간다.
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<ArcheryStateBroadcastSystem>());
            });
        }
    }
}
