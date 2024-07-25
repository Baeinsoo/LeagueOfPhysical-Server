using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class RoomNetwork : MonoSingleton<RoomNetwork>
    {
        private Dictionary<Type, Action<int, IMessage>> handlerMap;

        private INetwork networkImpl;

        protected override void Awake()
        {
            base.Awake();

            networkImpl = GetComponent<INetwork>() ?? gameObject.AddComponent<MirrorNetworkImpl>();
            networkImpl.onMessage += OnMessage;

            handlerMap = new Dictionary<Type, Action<int, IMessage>>();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            networkImpl.onMessage -= OnMessage;
        }

        private void OnMessage(int connectionId, IMessage message)
        {
            if (handlerMap.TryGetValue(message.GetType(), out var handler))
            {
                handler?.Invoke(connectionId, message);
            };
        }

        public void Send(IMessage message, int targetId, bool reliable = true, bool instant = false)
        {
            networkImpl.Send(message, targetId, reliable, instant);
        }

        public void SendToAll(IMessage message, bool reliable = true, bool instant = false)
        {
            networkImpl.SendToAll(message, reliable, instant);
        }

        public void SendToNear(IMessage message, Vector3 center, float radius, bool reliable = true, bool instant = false)
        {
            networkImpl.SendToNear(message, center, radius, reliable, instant);
        }

        public void RegisterHandler(Type type, Action<int, IMessage> handler)
        {
            if (handlerMap.ContainsKey(type) == true)
            {
                handlerMap[type] += handler;
            }
            else
            {
                handlerMap[type] = handler;
            }
        }

        public void UnregisterHandler(Type type, Action<int, IMessage> handler)
        {
            handlerMap[type] -= handler;
            if (handlerMap[type] == null)
            {
                handlerMap.Remove(type);
            }
        }
    }
}
