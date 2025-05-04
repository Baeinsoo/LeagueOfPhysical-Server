using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameEntityMessageHandler : IGameMessageHandler
    {
        [Inject]
        private IMessageDispatcher messageDispatcher;

        public void Register()
        {
            //RoomNetwork.instance.UnregisterHandler(typeof(EnterRoomToC), OnEnterRoomToC);
            //RoomNetwork.instance.UnregisterHandler(typeof(EntityStatesToC), OnEntityStatesToC);
        }

        public void Unregister()
        {
            //RoomNetwork.instance.UnregisterHandler(typeof(EnterRoomToC), OnEnterRoomToC);
            //RoomNetwork.instance.UnregisterHandler(typeof(EntityStatesToC), OnEntityStatesToC);
        }
    }
}
