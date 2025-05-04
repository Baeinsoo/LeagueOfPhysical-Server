using GameFramework;
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

        public void Register()
        {
            messageDispatcher.RegisterHandler<GameInfoToS>(OnGameInfoToS, LOPRoomMessageInterceptor.Default);
        }

        public void Unregister()
        {
            messageDispatcher.UnregisterHandler<GameInfoToS>(OnGameInfoToS);
        }

        private void OnGameInfoToS(GameInfoToS gameInfoToS)
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

            session.Send(gameInfoToC);
        }
    }
}
