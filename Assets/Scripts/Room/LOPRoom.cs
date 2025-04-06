using Cysharp.Threading.Tasks;
using GameFramework;
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UniRx;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class LOPRoom : MonoBehaviour, IRoom
    {
        private const int HEARTBEAT_INTERVAL = 2;       //  sec
        private const double TICK_INTERVAL = 1 / 60f;   //  sec

        [Inject] public IGame game { get; private set; }
        [Inject] private RoomNetwork roomNetwork;
        [Inject] private LOPNetworkManager networkManager;
        [Inject] private IDataContextManager dataManager;

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
                    roomId = dataManager.Get<RoomDataContext>().room.id,
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
            await game.InitializeAsync();

            InvokeRepeating("SendHeartbeat", 0, HEARTBEAT_INTERVAL);

            game.onGameStateChanged += OnGameStateChanged;

            await WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
            {
                roomId = dataManager.Get<RoomDataContext>().room.id,
                status = RoomStatus.Initializing,
            });

            initialized = true;
        }

        public async Task DeinitializeAsync()
        {
            await game.DeinitializeAsync();

            CancelInvoke("SendHeartbeat");

            dataManager.Get<RoomDataContext>().Clear();

            game.onGameStateChanged -= OnGameStateChanged;

            initialized = false;
        }

        public async Task StartRoomServerAsync()
        {
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
                roomId = dataManager.Get<RoomDataContext>().room.id,
                status = RoomStatus.WaitingForPlayers,
            });

            game.Run(0, TICK_INTERVAL, 0);
        }

        private void SendHeartbeat()
        {
            WebAPI.Heartbeat(dataManager.Get<RoomDataContext>().room.id);
        }

        private void OnGameStateChanged(GameState gameState)
        {
            switch (gameState)
            {
                case GameState.GameOver:
                    WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
                    {
                        roomId = dataManager.Get<RoomDataContext>().room.id,
                        status = RoomStatus.Closed,
                    });
                    break;
            }
        }
    }
}
