using GameFramework;
using UnityEngine;
using Mirror;
using System;

namespace LOP
{
    public class LOPSession : ISession
    {
        public string sessionId { get; }
        public string userId { get; }

        public bool isConnected => networkConnection != null && networkConnection.isReady;

        public NetworkConnection networkConnection { get; set; }

        public LOPSession(string userId, NetworkConnection networkConnection)
        {
            this.sessionId = Guid.NewGuid().ToString();
            this.userId = userId;
            this.networkConnection = networkConnection;
        }

        public void Send<T>(T message) where T : IMessage
        {
            if (isConnected == false)
            {
                return;
            }
        
            networkConnection.Send(new CustomMirrorMessage
            {
                payload = message,
            });
        }

        public IMessage Receive()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            if (networkConnection != null)
            {
                networkConnection.Disconnect();
                networkConnection = null;
            }
        }
    }
}
