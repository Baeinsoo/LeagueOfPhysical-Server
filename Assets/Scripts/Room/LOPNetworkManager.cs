using Mirror;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace LOP
{
    public class LOPNetworkManager : NetworkManager
    {
        #region Server System Callbacks
        /// <summary>
        /// Called on the server when a new client connects.
        /// <para>Unity calls this on the Server when a Client connects to the Server. Use an override to tell the NetworkManager what to do when a client connects to the server.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);

            //MessageBroker.Default.Publish(new PlayerEnter(conn));
        }

        /// <summary>
        /// Called on the server when a client disconnects.
        /// <para>This is called on the Server when a Client disconnects from the Server. Use an override to decide what should happen when a disconnection is detected.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);

            //MessageBroker.Default.Publish(new PlayerLeave(conn));
        }
        #endregion

        #region Start & Stop Callbacks
        /// <summary>
        /// This is invoked when a server is started - including when a host is started.
        /// <para>StartServer has multiple signatures, but they all cause this hook to be called.</para>
        /// </summary>
        public override void OnStartServer()
        {
            Debug.Log($"[OnStartServer]");

            //NotifyStartServerRequest request = new NotifyStartServerRequest
            //{
            //    roomId = LOP.Room.Instance.RoomId,
            //    matchId = SceneDataContainer.Get<MatchData>().matchId,
            //    expectedPlayerList = LOP.Room.Instance.ExpectedPlayerList,
            //    matchSetting = SceneDataContainer.Get<MatchData>().matchSetting,
            //    ip = LOP.Application.IP,
            //    port = LOP.Room.Instance.Port,
            //};

            //LOPWebAPI.UpdateStatus(request);
        }

        /// <summary>
        /// This is called when a server is stopped - including when a host is stopped.
        /// </summary>
        public override void OnStopServer()
        {
            Debug.Log($"[OnStopServer]");

            //LOPWebAPI.NotifyStopServer(LOP.Room.Instance.RoomId);
        }
        #endregion
    }
}
