using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameMessageHandler : IRoomMessageHandler
    {
        [Inject]
        private IGame game;

        [Inject]
        private IRoomNetwork roomNetwork;

        public void Register()
        {
            roomNetwork.RegisterHandler<GameInfoToS>(OnGameInfoToS, LOPRoomMessageInterceptor.Default);
        }

        public void Unregister()
        {
            roomNetwork.UnregisterHandler<GameInfoToS>(OnGameInfoToS);
        }

        private void OnGameInfoToS(int id, GameInfoToS gameInfoToS)
        {
            var gameInfoToC = new GameInfoToC
            {
                EntityId = "1",
                GameInfo = new GameInfo
                {
                    Tick = GameEngine.Time.tick,
                    Interval = GameEngine.Time.tickInterval,
                    ElapsedTime = GameEngine.Time.elapsedTime,
                },
            };

            roomNetwork.Send(gameInfoToC, id);
        }
    }
}
