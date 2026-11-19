using VContainer;

namespace LOP
{
    /// <summary>TriGrab 덩어리(서버) — 걷는 몸 위에 판 엔진(카드·갈고리·점수)·봇·방송을 얹는다.</summary>
    public class TriGrabLifetimeScope : GameLifetimeScope
    {
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.IWorld, LOPWorld>(Lifetime.Singleton);
            builder.Register<ICharacterCreator, CharacterCreator>(Lifetime.Singleton);
            RegisterGame(builder, runner);
        }

        /// <summary>
        /// 월드·몸 생성기를 뺀 나머지 등록(판 엔진·룰·봇·방송)과 틱 페이즈 연결. 시험이 실제 배선을 지나도록 따로 뺐다.
        /// </summary>
        public static void RegisterGame(IContainerBuilder builder, LOPRunner runner)
        {
            builder.Register(c => new TriGrabMatch(unchecked((int)c.Resolve<IMatchSeed>().Value)), Lifetime.Singleton);
            builder.Register<TriGrabRunClock>(Lifetime.Singleton);
            builder.Register(c => new TriGrabBotFill(TriGrabTestBots.ReadFillTo, TriGrabConfig.MinPlayersWithBots), Lifetime.Singleton);
            builder.Register<IGameRuleSystem, TriGrabRuleSystem>(Lifetime.Singleton);

            builder.Register<TriGrabMatchSystem>(Lifetime.Singleton);
            builder.Register<TriGrabStateBroadcastSystem>(Lifetime.Singleton);
            builder.Register(c => new TriGrabBotSystem(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<TriGrabMatch>(),
                c.Resolve<InputBufferSystem>(),
                c.Resolve<IMatchSeed>(),
                MountedArcheryBots.IsBot), Lifetime.Singleton);

            // 시스템이 러너를 직접 잡으면 순환이 생겨 빌드 콜백에서 붙인다(DodgeLifetimeScope와 같다).
            builder.RegisterBuildCallback(container =>
            {
                //  봇 입력은 사람 입력이 처리되기 전(Begin)에 이번 틱 번호로 넣어야 같은 틱에 쓰인다.
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.Begin>(container.Resolve<TriGrabBotSystem>());
                //  순서가 뜻이다: 판 진행 → 방송.
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(container.Resolve<TriGrabMatchSystem>());
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(container.Resolve<TriGrabStateBroadcastSystem>());
            });
        }
    }
}
