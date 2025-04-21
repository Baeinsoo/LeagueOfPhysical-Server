using GameFramework;
using LOP;
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
            roomNetwork.RegisterHandler<GameInfoRequest>(OnGameInfoRequest, LOPRoomMessageInterceptor.Default);
        }

        public void Unregister()
        {
            roomNetwork.UnregisterHandler<GameInfoRequest>(OnGameInfoRequest);
        }

        private void OnGameInfoRequest(int id, GameInfoRequest request)
        {
            var gameInfoResponse = new GameInfoResponse
            {
                EntityId = "1",
                GameInfo = new GameInfo
                {
                    Tick = GameEngine.Time.tick,
                    Interval = GameEngine.Time.tickInterval,
                    ElapsedTime = GameEngine.Time.elapsedTime,
                },
            };

            roomNetwork.Send(gameInfoResponse, id);
        }
    }
}
