using GameFramework;
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class MirrorNetworkImpl : MonoBehaviour, INetwork
    {
        public event Action<int, IMessage> onMessage;

        private void Awake()
        {
            RegisterMessage();
        }

        private void OnDestroy()
        {
            onMessage = null;

            UnregisterMessage();
        }

        private void RegisterMessage()
        {
            NetworkServer.RegisterHandler<CustomMirrorMessage>((conn, message) =>
            {
                onMessage?.Invoke(conn.connectionId, message.payload);
            });
        }

        private void UnregisterMessage()
        {
            NetworkClient.UnregisterHandler<CustomMirrorMessage>();
        }

        public void Send(IMessage message, int targetId, bool reliable = true, bool instant = false)
        {
            if (!NetworkServer.connections.ContainsKey(targetId))
            {
                Debug.LogWarning($"Target is not connected. targetId: {targetId}");
                return;
            }

            if (!NetworkServer.connections[targetId].isAuthenticated)
            {
                Debug.LogWarning($"Target is not authenticated. targetId: {targetId}");
                return;
            }

            var customMirrorMessage = new CustomMirrorMessage
            {
                payload = message,
            };

            NetworkServer.connections[targetId].Send(customMirrorMessage);
        }

        public void SendToAll(IMessage message, bool reliable = true, bool instant = false)
        {
            foreach (var connectionId in NetworkServer.connections.Keys)
            {
                Send(message, connectionId, reliable, instant);
            }
        }

        public void SendToNear(IMessage message, Vector3 center, float radius, bool reliable = true, bool instant = false)
        {
            //foreach (IEntity entity in Entities.Get(center, radius, EntityRole.Player))
            //{
            //    if (GameIdMap.TryGetConnectionIdByEntityId(entity.EntityId, out var connectionId))
            //    {
            //        Send(msg, connectionId, reliable, instant);
            //    }
            //}
        }
    }
}
