using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameInputMessageHandler : IGameMessageHandler
    {
        [Inject]
        private IGame game;

        [Inject]
        private IRoomNetwork roomNetwork;

        public void Register()
        {
            //RoomNetwork.instance.UnregisterHandler(typeof(InputSequnceToC), OnInputSequnceToC);
        }

        public void Unregister()
        {
            //RoomNetwork.instance.UnregisterHandler(typeof(InputSequnceToC), OnInputSequnceToC);
        }
    }
}
