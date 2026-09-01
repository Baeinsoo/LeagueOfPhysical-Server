using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class LOPNetworkManager : NetworkManager
    {
        public event System.Action onStartServer;
        public event System.Action onStopServer;

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

        /// <summary>
        /// 지연 시뮬레이터가 감싸고 있으면 벗겨 실제 트랜스포트를 돌려준다. 안 감싸고 있으면 그대로.
        /// </summary>
        public static Transport Unwrap(Transport configured)
        {
            return configured is LatencySimulation simulation && simulation.wrap != null
                ? simulation.wrap
                : configured;
        }

        /// <summary>
        /// 빌드에서는 지연 시뮬레이터를 쓰지 않는다 — 씬에 켜 둔 채로 빌드하면 그 지연이 그대로
        /// 실려 나간다. 클라에서 실제로 그렇게 편도 100ms가 실린 APK가 나갔다(2026-09-01).
        ///
        /// <para>지금 서버 씬에는 시뮬레이터가 없어 이 줄은 아무 일도 하지 않는다. 그럼에도 미리
        /// 두는 이유: 클·서 양쪽에 지연을 걸어야 실제 환경과 같은 비대칭 없는 테스트가 되므로
        /// 서버 씬에도 붙일 참이고, 게임서버는 라이브 클러스터에 배포되어 사고 범위가 훨씬 크다.</para>
        ///
        /// <para>시뮬레이터를 끄지는 않는다 — <c>LatencySimulation.OnDisable()</c>이 자기가 감싸던
        /// 트랜스포트까지 같이 꺼서 진짜 통신이 죽는다. <c>base.Awake()</c>가
        /// <c>InitializeSingleton()</c>에서 <c>Transport.active</c>를 굳히므로 그 전에 바꾼다.</para>
        /// </summary>
        public override void Awake()
        {
#if !UNITY_EDITOR
            transport = Unwrap(transport);
#endif
            base.Awake();
        }

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
