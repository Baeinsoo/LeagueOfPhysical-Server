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

        [Inject] public IGame game { get; private set; }
        [Inject] private LOPNetworkManager networkManager;
        [Inject] private ISessionManager sessionManager;
        [Inject] private IRoomDataStore roomDataStore;
        [Inject] private IMessageDispatcher messageDispatcher;
        [Inject] private IEnumerable<IRoomMessageHandler> roomMessageHandlers;

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

                await WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
                {
                    roomId = roomDataStore.room.id,
                    status = RoomStatus.Error,
                });
            }
        }

        private async void OnDestroy()
        {
            await ShutdownRoomServerAsync();
            await DeinitializeAsync();
        }

        public async Task InitializeAsync()
        {
            foreach (var roomMessageHandler in roomMessageHandlers.OrEmpty())
            {
                roomMessageHandler.Register();
            }

            game.onGameStateChanged += OnGameStateChanged;

            InvokeRepeating("SendHeartbeat", 0, HEARTBEAT_INTERVAL);

            await game.InitializeAsync();

            await WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
            {
                roomId = roomDataStore.room.id,
                status = RoomStatus.Initializing,
            });

            initialized = true;
        }

        public async Task DeinitializeAsync()
        {
            await game.DeinitializeAsync();

            CancelInvoke("SendHeartbeat");

            game.onGameStateChanged -= OnGameStateChanged;

            foreach (var roomMessageHandler in roomMessageHandlers.OrEmpty())
            {
                roomMessageHandler.Unregister();
            }

            roomDataStore.Clear();

            messageDispatcher.Dispose();

            initialized = false;
        }

        public async Task StartRoomServerAsync()
        {
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
            await WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
            {
                roomId = roomDataStore.room.id,
                status = RoomStatus.WaitingForPlayers,
            });

            game.Run(0, TICK_INTERVAL, 0);
        }

        private void SendHeartbeat()
        {
            WebAPI.Heartbeat(roomDataStore.room.id);
        }

        private void OnGameStateChanged(IGameState gameState)
        {
            switch (gameState)
            {
                case GameOver:
                    Debug.Log("Game Over");
                    WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
                    {
                        roomId = roomDataStore.room.id,
                        status = RoomStatus.Closed,
                    });
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
