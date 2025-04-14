using System;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public interface IRoomNetwork
    {
        event Action<IMessage> onMessage;
        void SendToServer(IMessage message, bool reliable = true, bool instant = false) { }
        void RegisterHandler<T>(Action<T> handler) where T : IMessage { }
        void UnregisterHandler<T>(Action<T> handler) where T : IMessage { }
    }
}
