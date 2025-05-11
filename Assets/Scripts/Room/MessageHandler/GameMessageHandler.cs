using GameFramework;
using LOP.Event.LOPGameEngine.Update;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameMessageHandler : IRoomMessageHandler
    {
        [Inject]
        private IGameEngine gameEngine;

        [Inject]
        private IMessageDispatcher messageDispatcher;

        [Inject]
        private ISessionManager sessionManager;

        private List<GameInfoToS> gameInfoToSList = new List<GameInfoToS>();

        public void Register()
        {
            messageDispatcher.RegisterHandler<GameInfoToS>(OnGameInfoToS, LOPRoomMessageInterceptor.Default);

            gameEngine.AddListener(this);
        }

        public void Unregister()
        {
            messageDispatcher.UnregisterHandler<GameInfoToS>(OnGameInfoToS);

            gameEngine.RemoveListener(this);
        }

        private void OnGameInfoToS(GameInfoToS gameInfoToS)
        {
            gameInfoToSList.Add(gameInfoToS);
        }

        [GameEngineListen(typeof(End))]
        private void OnEnd()
        {
            EntitySnap[] allEntitySnaps = (gameEngine as LOPGameEngine).entityManager.GetAllEntitySnaps();

            foreach (var gameInfoToS in gameInfoToSList)
            {
                var session = sessionManager.GetSessionByUserId(gameInfoToS.UserId);
                var entity = gameEngine.entityManager.GetEntityByUserId<LOPEntity>(gameInfoToS.UserId);

                var gameInfoToC = new GameInfoToC
                {
                    EntityId = entity.entityId,
                    SessionId = session.sessionId,
                    GameInfo = new GameInfo
                    {
                        Tick = GameEngine.Time.tick,
                        Interval = GameEngine.Time.tickInterval,
                        ElapsedTime = GameEngine.Time.elapsedTime,
                    },
                };

                gameInfoToC.GameInfo.EntitySnaps.AddRange(allEntitySnaps);

                session.Send(gameInfoToC);
            }

            gameInfoToSList.Clear();
        }
    }
}
