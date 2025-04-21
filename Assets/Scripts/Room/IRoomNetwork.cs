using System;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public interface IRoomNetwork
    {
        event Action<int, IMessage> onMessage;
        void Send(IMessage message, int targetId, bool reliable = true, bool instant = false);
        void SendToAll(IMessage message, bool reliable = true, bool instant = false);
        void SendToNear(IMessage message, Vector3 center, float radius, bool reliable = true, bool instant = false);
        void RegisterHandler<T>(Action<int, T> handler) where T : IMessage;
        void UnregisterHandler<T>(Action<int, T> handler) where T : IMessage;
    }
}
