using VContainer;

namespace LOP
{
    /// <summary>카드 러시 덩어리(서버) — 물리 없는 상태 월드·레인 몸·완주 순위 룰.</summary>
    public class CardRushLifetimeScope : GameLifetimeScope
    {
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<CardRushConfigProvider>(Lifetime.Singleton);
            builder.Register<CardRushConfig>(c => c.Resolve<CardRushConfigProvider>().Get(), Lifetime.Singleton);

            //  맵 씬의 CardRushMapRules 마커가 맵 로드 때 여기에 값을 넣는다. 등록이 없으면 마커의 [Inject]가
            //  씬 주입을 끊는다 — 클·서 양쪽 다 등록해야 한다(FlappyMapRulesField와 같은 이유).
            builder.Register<CardRushMapRulesField>(Lifetime.Singleton);

            //  씨앗은 GameplayInstaller의 MatchSeed — 스코프마다 한 번 만들어지고 판 중에 안 바뀐다.
            builder.Register<CardRushWorld>(c => new CardRushWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<CardRushMapRulesField>(),
                c.Resolve<IMatchSeed>(),
                c.Resolve<CardRushConfig>().Timing), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            builder.Register<ICharacterCreator, CardRushRunnerCreator>(Lifetime.Singleton);
            builder.Register<FinishTrackingSystem>(Lifetime.Singleton);

            //  최소 레인 채우기(매칭 규칙) + 시험 스위치. 스위치가 꺼져 있어도 config.MinLanes만큼은 채운다.
            builder.Register(c => new CardRushBotFill(
                CardRushTestBots.ReadFillTo, c.Resolve<CardRushConfig>().MinLanes), Lifetime.Singleton);
            builder.Register<IGameRuleSystem, CardRushRuleSystem>(Lifetime.Singleton);

            builder.Register(c => new CardRushBotSystem(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<CardRushWorld>(),
                c.Resolve<InputBufferSystem>(),
                c.Resolve<IMatchSeed>(),
                c.Resolve<CardRushConfig>(),
                MountedArcheryBots.IsBot), Lifetime.Singleton);

            //  도착 감시를 러너의 End 페이즈에 문다. 시스템이 스스로 IRunner를 잡으면
            //  러너→룰→도착→러너로 고리가 생겨 컨테이너가 아예 안 만들어진다(FlappyRaceLifetimeScope와 같다).
            builder.RegisterBuildCallback(container =>
            {
                //  봇 입력은 사람 입력이 처리되기 전(Begin)에 이번 틱 번호로 넣어야 같은 틱에 쓰인다.
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.Begin>(
                    container.Resolve<CardRushBotSystem>());
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<FinishTrackingSystem>());
            });
        }
    }
}
