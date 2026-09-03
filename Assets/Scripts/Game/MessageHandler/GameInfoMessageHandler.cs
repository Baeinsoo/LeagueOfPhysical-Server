using GameFramework;
using GameFramework.Runner;
using LOP.Event.LOPRunner.Update;
using MessagePipe;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class GameInfoMessageHandler : MessageHandlerBase, ITickSystem
    {
        private readonly IRunner runner;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly MatchSeed matchSeed;
        private readonly IEntityCreationDataFactory entityCreationDataFactory;
        private readonly EntitySpawner entitySpawner;
        private readonly MatchStartSystem matchStartSystem;
        private readonly ISubscriber<ClientMessage<GameInfoToS>> gameInfoSubscriber;

        private List<ClientMessage<GameInfoToS>> gameInfoToSList = new List<ClientMessage<GameInfoToS>>();

        public GameInfoMessageHandler(
            IRunner runner,
            GameFramework.World.EntityRegistry entityRegistry,
            MatchSeed matchSeed,
            IEntityCreationDataFactory entityCreationDataFactory,
            EntitySpawner entitySpawner,
            MatchStartSystem matchStartSystem,
            ISubscriber<ClientMessage<GameInfoToS>> gameInfoSubscriber)
        {
            this.runner = runner;
            this.entityRegistry = entityRegistry;
            this.matchSeed = matchSeed;
            this.entityCreationDataFactory = entityCreationDataFactory;
            this.entitySpawner = entitySpawner;
            this.matchStartSystem = matchStartSystem;
            this.gameInfoSubscriber = gameInfoSubscriber;
        }

        protected override void Subscribe()
        {
            Track(gameInfoSubscriber.Subscribe(OnGameInfoToS));
            runner.RegisterSystem<End>(this);
        }

        public override void Dispose()
        {
            base.Dispose();                  // 구독 일괄 해제
            runner.UnregisterSystem(this);   // 추가 teardown
        }

        private void OnGameInfoToS(ClientMessage<GameInfoToS> received)
        {
            gameInfoToSList.Add(received);
        }

        public void Tick(long tick, float deltaTime)
        {
            if (gameInfoToSList.Count == 0)
            {
                return;
            }

            EntityCreationData[] allEntityCreationDatas = BuildAllEntityCreationDatas();

            foreach (var received in gameInfoToSList)
            {
                var session = received.Session;
                string entityId = entitySpawner.GetEntityIdByUserId(session.userId);

                var gameInfoToC = new GameInfoToC
                {
                    //  관전 중(탈락)이면 조종할 몸이 없다. proto의 문자열 필드는 null을 거부하므로
                    //  빈 값으로 보낸다 — 클라는 이 값을 비교에만 쓰므로 어떤 엔티티와도 안 맞아
                    //  "내 몸 없음"이 그대로 표현된다. 세상 정보(다른 새들)는 그대로 보낸다,
                    //  관전하려면 그게 필요하다.
                    EntityId = entityId ?? string.Empty,
                    SessionId = session.sessionId,
                    // 판치기 등 입력을 안 보내는 모드는 InputBuffer가 없다 — 없는 게 정상이니 조회 실패를 감내한다.
                    ExpectedNextSequence = (entityId == null
                        ? null : entityRegistry.Get(entityId)?.Get<InputBuffer>())?.ExpectedNextSequence ?? 0,
                    GameInfo = new GameInfo
                    {
                        // Tick·ElapsedTime은 클라가 더 이상 시드로 쓰지 않는다(자기 시계에서 유도).
                        // 진단용으로만 남긴다 — 보낸 순간의 값이라 시작 시점을 이걸로 정하면 어긋난다.
                        // Interval·MatchSeed는 계속 필요하다(스냅샷으로 대체 불가) — 지우지 말 것.
                        Tick = runner.tickUpdater.tick,
                        Interval = runner.tickUpdater.interval,
                        ElapsedTime = runner.tickUpdater.elapsedTime,
                        MatchSeed = matchSeed.Value,
                    },
                };

                gameInfoToC.GameInfo.EntityCreationDatas.AddRange(allEntityCreationDatas);

                session.Send(gameInfoToC);
                //  이미 달리는 판에 붙은 사람도 이 한 줄로 출발틱을 받아 바로 참여한다 —
                //  지각 입장용 별도 경로가 필요 없다.
                session.Send(matchStartSystem.BuildMessage());
            }

            gameInfoToSList.Clear();
        }

        private EntityCreationData[] BuildAllEntityCreationDatas()
        {
            var list = new List<EntityCreationData>();
            foreach (var worldEntity in entityRegistry.All)
            {
                list.Add(entityCreationDataFactory.Create(worldEntity));
            }
            return list.ToArray();
        }
    }
}
