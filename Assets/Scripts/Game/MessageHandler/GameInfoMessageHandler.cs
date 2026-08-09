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
        private readonly ISubscriber<ClientMessage<GameInfoToS>> gameInfoSubscriber;

        private List<ClientMessage<GameInfoToS>> gameInfoToSList = new List<ClientMessage<GameInfoToS>>();

        public GameInfoMessageHandler(
            IRunner runner,
            GameFramework.World.EntityRegistry entityRegistry,
            MatchSeed matchSeed,
            IEntityCreationDataFactory entityCreationDataFactory,
            EntitySpawner entitySpawner,
            ISubscriber<ClientMessage<GameInfoToS>> gameInfoSubscriber)
        {
            this.runner = runner;
            this.entityRegistry = entityRegistry;
            this.matchSeed = matchSeed;
            this.entityCreationDataFactory = entityCreationDataFactory;
            this.entitySpawner = entitySpawner;
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
                    EntityId = entityId,
                    SessionId = session.sessionId,
                    ExpectedNextSequence = entityRegistry.Get(entityId).Get<InputBuffer>().ExpectedNextSequence,
                    GameInfo = new GameInfo
                    {
                        Tick = runner.tickUpdater.tick,
                        Interval = runner.tickUpdater.interval,
                        ElapsedTime = runner.tickUpdater.elapsedTime,
                        MatchSeed = matchSeed.Value,
                    },
                };

                gameInfoToC.GameInfo.EntityCreationDatas.AddRange(allEntityCreationDatas);

                session.Send(gameInfoToC);
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
