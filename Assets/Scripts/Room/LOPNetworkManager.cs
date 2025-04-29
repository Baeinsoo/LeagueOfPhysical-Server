using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class LOPNetworkManager : NetworkManager
    {
        public event Action onStartServer;
        public event Action onStopServer;

        public event Action<LOPConnectionData> onServerConnect;
        public event Action<LOPConnectionData> onServerDisconnect;

        private PortTransport _portTransport;
        public PortTransport portTransport
        {
            get
            {
                return _portTransport ??= (transport is LatencySimulation latencySimulation ? latencySimulation.wrap : transport) as PortTransport;
            }
        }

        public ushort port
        {
            set => portTransport.Port = value;
            get => portTransport.Port;
        }

        #region Server System Callbacks
        /// <summary>
        /// Called on the server when a new client connects.
        /// <para>Unity calls this on the Server when a Client connects to the Server. Use an override to tell the NetworkManager what to do when a client connects to the server.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);

            onServerConnect?.Invoke(new LOPConnectionData
            {
                networkConnection = conn,
            });
        }

        /// <summary>
        /// Called on the server when a client disconnects.
        /// <para>This is called on the Server when a Client disconnects from the Server. Use an override to decide what should happen when a disconnection is detected.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);

            onServerDisconnect?.Invoke(new LOPConnectionData
            {
                networkConnection = conn,
            });
        }
        #endregion

        #region Start & Stop Callbacks
        /// <summary>
        /// This is invoked when a server is started - including when a host is started.
        /// <para>StartServer has multiple signatures, but they all cause this hook to be called.</para>
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();

            onStartServer?.Invoke();
        }

        /// <summary>
        /// This is called when a server is stopped - including when a host is stopped.
        /// </summary>
        public override void OnStopServer()
        {
            base.OnStopServer();

            onStopServer?.Invoke();
        }
        #endregion
    }
}
