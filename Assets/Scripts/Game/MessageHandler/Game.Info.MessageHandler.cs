using GameFramework;
using LOP.Event.LOPRunner.Update;
using MessagePipe;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameInfoMessageHandler : IGameMessageHandler
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
        private ISubscriber<GameInfoToS> gameInfoSubscriber;

        private List<GameInfoToS> gameInfoToSList = new List<GameInfoToS>();
        private System.IDisposable subscription;

        public void Initialize()
        {
            subscription = gameInfoSubscriber.Subscribe(OnGameInfoToS);

            runner.AddListener(this);
        }

        public void Dispose()
        {
            subscription?.Dispose();

            runner.RemoveListener(this);
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

            EntityCreationData[] allEntityCreationDatas = (runner as LOPRunner).entityManager.GetAllEntityCreationDatas();

            foreach (var gameInfoToS in gameInfoToSList)
            {
                var session = sessionManager.GetSessionByUserId(gameInfoToS.UserId);
                var actor = runner.entityManager.GetEntityByUserId<LOPActor>(gameInfoToS.UserId);

                var gameInfoToC = new GameInfoToC
                {
                    EntityId = actor.entityId,
                    SessionId = session.sessionId,
                    ExpectedNextSequence = entityRegistry.Get(actor.entityId).Get<InputBuffer>().ExpectedNextSequence,
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
    }
}
