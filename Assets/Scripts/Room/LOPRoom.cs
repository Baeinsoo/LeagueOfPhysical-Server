using GameFramework;
using Mirror;
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
        private const int HEARTBEAT_INTERVAL = 2;  //  sec

        [Inject] public IGame game { get; private set; }
        [Inject] private RoomNetwork roomNetwork;
        [Inject] private LOPNetworkManager networkManager;

        public bool initialized { get; private set; }

        private async void Awake()
        {
            await InitializeAsync();

            StartGame();
        }

        private async void OnDestroy()
        {
            StopGame();

            await DeinitializeAsync();
        }

        public async Task InitializeAsync()
        {
            Data.Room.room = Blackboard.Read<RoomDto>(erase: true);
            Data.Room.match = Blackboard.Read<MatchDto>(erase: true);

            networkManager.port = Blackboard.Read<ushort>("port", erase: true);

            await game.InitializeAsync();

            initialized = true;

            await WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
            {
                roomId = Data.Room.room.id,
                status = RoomStatus.Initializing,
            });
        }

        public async Task DeinitializeAsync()
        {
            await game.DeinitializeAsync();

            Data.Room.Clear();

            initialized = false;
        }

        public void StartGame()
        {
            networkManager.StartServer();

            InvokeRepeating("SendHeartbeat", 0, HEARTBEAT_INTERVAL);

            if (NetworkServer.active)
            {
                WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
                {
                    roomId = Data.Room.room.id,
                    status = RoomStatus.WaitingForPlayers,
                });
            }

            Invoke("EndRoom", 60);
        }

        public void StopGame()
        {
            networkManager.StopServer();
        }

        private void SendHeartbeat()
        {
            WebAPI.Heartbeat(Data.Room.room.id);
        }

        private void EndRoom()
        {
            WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
            {
                roomId = Data.Room.room.id,
                status = RoomStatus.Closed,
            });
        }
    }
}
