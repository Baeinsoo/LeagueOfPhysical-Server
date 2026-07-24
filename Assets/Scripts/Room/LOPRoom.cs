using Cysharp.Threading.Tasks;
using GameFramework;
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniRx;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class LOPRoom : MonoBehaviour, IServerRoom
    {
        private const int HEARTBEAT_INTERVAL = 2;       //  sec
        private const double TICK_INTERVAL = 1 / 50d;   //  sec

        [Inject] private IGameFactory gameFactory;
        [Inject] private LOPNetworkManager networkManager;
        [Inject] private ISessionManager sessionManager;
        [Inject] private IRoomDataStore roomDataStore;
        [Inject] private NetworkMessageDispatcher dispatcher;

        public IRunner runner { get; private set; }

        public bool initialized { get; private set; }

        private async void Awake()
        {
            try
            {
                await InitializeAsync();
                await StartRoomServerAsync();
                await StartGameAsync();
            }
            catch (Exception e)
            {
                Debug.LogError(e);

                if (!EnvironmentSettings.active.Standalone)
                {
                    await WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
                    {
                        roomId = roomDataStore.room.id,
                        status = RoomStatus.Error,
                    });
                }
            }
        }

        private async void OnDestroy()
        {
            await ShutdownRoomServerAsync();
            await DeinitializeAsync();
        }

        public async Task InitializeAsync()
        {
            runner = await gameFactory.CreateAsync();
            runner.onGameStateChanged += OnGameStateChanged;

            InvokeRepeating("SendHeartbeat", 0, HEARTBEAT_INTERVAL);

            await runner.InitializeAsync();

            if (!EnvironmentSettings.active.Standalone)
            {
                await WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
                {
                    roomId = roomDataStore.room.id,
                    status = RoomStatus.Initializing,
                });
            }

            initialized = true;
        }

        public async Task DeinitializeAsync()
        {
            await runner.DeinitializeAsync();

            CancelInvoke("SendHeartbeat");

            runner.onGameStateChanged -= OnGameStateChanged;

            await gameFactory.DestroyAsync();
            runner = null;

            roomDataStore.Clear();

            initialized = false;
        }

        public async Task StartRoomServerAsync()
        {
            NetworkServer.RegisterHandler<CustomMirrorMessage>((id, message) =>
            {
                dispatcher.Dispatch(message.payload);
            });

            networkManager.onServerConnect += OnPlayerConnect;
            networkManager.onServerDisconnect += OnPlayerDisconnect;
            networkManager.port = Blackboard.Read<ushort>("port", erase: true);
            networkManager.StartServer();

            await UniTask.WaitUntil(() => NetworkServer.active);
        }

        private async Task ShutdownRoomServerAsync()
        {
            networkManager.StopClient();

            await UniTask.WaitUntil(() => NetworkServer.active == false);
        }

        public async Task StartGameAsync()
        {
            if (EnvironmentSettings.active.Standalone)
            {
                await Task.CompletedTask;
            }
            else
            {
                await WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
                {
                    roomId = roomDataStore.room.id,
                    status = RoomStatus.WaitingForPlayers,
                });
            }

            runner.Run(0, TICK_INTERVAL, 0);
        }

        private void SendHeartbeat()
        {
            if (!EnvironmentSettings.active.Standalone)
            {
                WebAPI.Heartbeat(roomDataStore.room.id);
            }
        }

        private void OnGameStateChanged(IGameState gameState)
        {
            switch (gameState)
            {
                case GameOver:
                    Debug.Log("Game Over");

                    // 룸을 닫으면 클라 연결이 끊겨 못 받는다 — 상태 갱신보다 반드시 먼저 보낸다.
                    foreach (var session in sessionManager.GetAllSessions())
                    {
                        session.Send(new MatchEndedToC());
                    }

                    if (!EnvironmentSettings.active.Standalone)
                    {
                        WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
                        {
                            roomId = roomDataStore.room.id,
                            status = RoomStatus.Closed,
                        });
                    }
                    break;
            }
        }

        public void OnPlayerConnect(IConnectionData connectionData)
        {
            if (connectionData is not LOPConnectionData data)
            {
                throw new ArgumentException("Invalid connection data");
            }

            var conn = data.networkConnection;
            var customProperties = conn.authenticationData as CustomProperties;

            Debug.Log($"[OnPlayerEnter] userId: {customProperties.userId}, identity: {conn.identity}");

            if (sessionManager.TryGetSessionByUserId<LOPSession>(customProperties.userId, out var session))
            {
                session.networkConnection = conn;
            }
            else
            {
                sessionManager.AddSession(new LOPSession(customProperties.userId, conn));
            }
        }

        public void OnPlayerDisconnect(IConnectionData connectionData)
        {
            if (connectionData is not LOPConnectionData data)
            {
                throw new ArgumentException("Invalid connection data");
            }

            var conn = data.networkConnection;
            var customProperties = conn.authenticationData as CustomProperties;

            Debug.Log($"[OnPlayerLeave] userId: {customProperties.userId}, identity: {conn.identity}");

            var session = sessionManager.GetSessionByUserId<LOPSession>(customProperties.userId);
            session.networkConnection = null;
        }
    }
}
