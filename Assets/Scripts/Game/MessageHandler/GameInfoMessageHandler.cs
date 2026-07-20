using GameFramework;
using LOP.Event.LOPRunner.Update;
using MessagePipe;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameInfoMessageHandler : MessageHandlerBase
    {
        [Inject]
        private IRunner runner;

        [Inject]
        private ISessionManager sessionManager;

        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        [Inject]
        private MatchSeed matchSeed;

        [Inject]
        private IEntityCreationDataFactory entityCreationDataFactory;

        [Inject]
        private EntitySpawner entitySpawner;

        [Inject]
        private ISubscriber<GameInfoToS> gameInfoSubscriber;

        private List<GameInfoToS> gameInfoToSList = new List<GameInfoToS>();

        protected override void Subscribe()
        {
            Track(gameInfoSubscriber.Subscribe(OnGameInfoToS));
            runner.AddListener(this);
        }

        public override void Dispose()
        {
            base.Dispose();               // 구독 일괄 해제
            runner.RemoveListener(this);  // 추가 teardown
        }

        private void OnGameInfoToS(GameInfoToS gameInfoToS)
        {
            gameInfoToSList.Add(gameInfoToS);
        }

        [RunnerListen(typeof(End))]
        private void OnEnd()
        {
            if (gameInfoToSList.Count == 0)
            {
                return;
            }

            EntityCreationData[] allEntityCreationDatas = BuildAllEntityCreationDatas();

            foreach (var gameInfoToS in gameInfoToSList)
            {
                var session = sessionManager.GetSessionByUserId(gameInfoToS.UserId);
                string entityId = entitySpawner.GetEntityIdByUserId(gameInfoToS.UserId);

                var gameInfoToC = new GameInfoToC
                {
                    EntityId = entityId,
                    SessionId = session.sessionId,
                    ExpectedNextSequence = entityRegistry.Get(entityId).Get<InputBuffer>().ExpectedNextSequence,
                    GameInfo = new GameInfo
                    {
                        Tick = Runner.Time.tick,
                        Interval = Runner.Time.tickInterval,
                        ElapsedTime = Runner.Time.elapsedTime,
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
