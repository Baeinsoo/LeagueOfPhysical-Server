using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using GameFramework;
using VContainer;

namespace LOP
{
    [DIMonoBehaviour]
    public class LOPNetworkAuthenticator : NetworkAuthenticator
    {
        [Inject]
        private IDataContextManager dataManager;

        #region Messages
        public struct AuthRequestMessage : NetworkMessage
        {
            public CustomProperties customProperties;
        }

        public struct AuthResponseMessage : NetworkMessage
        {
            public int code;
            public string message;
        }
        #endregion

        #region Server
        /// <summary>
        /// Called on server from StartServer to initialize the Authenticator
        /// <para>Server message handlers should be registered in this method.</para>
        /// </summary>
        public override void OnStartServer()
        {
            // register a handler for the authentication request we expect from client
            NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequestMessage, false);
        }

        /// <summary>
        /// Called on server from StopServer to reset the Authenticator
        /// <para>Server message handlers should be registered in this method.</para>
        /// </summary>
        public override void OnStopServer()
        {
            // unregister the handler for the authentication request
            NetworkServer.UnregisterHandler<AuthRequestMessage>();
        }

        /// <summary>
        /// Called on server from OnServerAuthenticateInternal when a client needs to authenticate
        /// </summary>
        /// <param name="conn">Connection to client.</param>
        public override void OnServerAuthenticate(NetworkConnectionToClient conn) { }

        /// <summary>
        /// Called on server when the client's AuthRequestMessage arrives
        /// </summary>
        /// <param name="conn">Connection to client.</param>
        /// <param name="msg">The message payload</param>
        public void OnAuthRequestMessage(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            bool authenticated = IsAuthenticated(conn, msg);

            if (authenticated)
            {
                // Store the customProperties for later reference, e.g. when spawning the player
                conn.authenticationData = msg.customProperties;

                // Send a response to client telling it to proceed as authenticated
                conn.Send(new AuthResponseMessage { code = 200, message = "success" });

                // Accept the successful authentication
                ServerAccept(conn);
            }
            else
            {
                // create and send msg to client so it knows to disconnect
                conn.Send(new AuthResponseMessage { code = 401, message = "Invalid Credentials" });

                // must set NetworkConnection isAuthenticated = false
                conn.isAuthenticated = false;

                ServerReject(conn);
            }
        }

        private bool IsAuthenticated(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            bool authenticated = true;

            if (dataManager.Get<RoomDataContext>().match.playerList.Contains(msg.customProperties.userId) == false)
            {
                authenticated = false;
            }

            return authenticated;
        }
        #endregion
    }
}
