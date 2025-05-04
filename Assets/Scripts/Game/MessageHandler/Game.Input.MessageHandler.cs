using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameInputMessageHandler : IGameMessageHandler
    {
        [Inject]
        private IGameEngine gameEngine;

        [Inject]
        private IMessageDispatcher messageDispatcher;

        public void Register()
        {
            //messageDispatcher.RegisterHandler<PlayerInputToS>(OnPlayerInputToS, LOPRoomMessageInterceptor.Default);
        }

        public void Unregister()
        {
            //messageDispatcher.UnregisterHandler<PlayerInputToS>(OnPlayerInputToS);
        }
    }
}
