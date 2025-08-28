using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using VContainer;

namespace LOP
{
    public class ConfigureRoomComponent : IEntranceComponent
    {
        [Inject]
        private IRoomDataStore roomDataStore;

        public async Task Execute()
        {
            string roomId = null;
            ushort port = 0;

            try
            {
#if UNITY_EDITOR
                port = 7777;
                Blackboard.Write("port", port);

                Room room = new Room
                {
                    id = "EditorTestRoom",
                    matchId = "EditorTestMatch",
                    status = RoomStatus.Initializing,
                    ip = "localhost",
                    port = port,
                };

                roomDataStore.room = room;

                Match match = new Match
                {
                    id = "EditorTestMatch",
                    matchType = GameMode.Normal,
                    subGameId = "FlapWang",
                    mapId = "FlapWangMap",
                    targetRating = 1500,
                    playerList = new string[]
                    {
                        "c391ab13-d4ef-407e-be8a-0ff5aa53bec0",
                        "ae6764c1-4469-442d-a89c-709badeb997b",
                        "c503d4cc-035c-44cd-9be0-7e828017bb68",
                        "f7ed95fc-a74a-496b-b11c-a783a5097c91",
                        "a2401694-e2b7-4cc6-967e-4292e7c22c62",
                    }
                };
                roomDataStore.match = match;
#else
                roomId = Environment.GetEnvironmentVariable("ROOM_ID");
                port = ushort.Parse(Environment.GetEnvironmentVariable("PORT"));
                Blackboard.Write("port", port);
                
                var getRoom = await WebAPI.GetRoom(roomId);
                var getMatch = await WebAPI.GetMatch(getRoom.response.room.matchId);
#endif
            }
            catch (Exception e)
            {
                Debug.LogException(e);

                if (string.IsNullOrEmpty(roomId))
                {
                    await WebAPI.UpdateRoomStatus(new UpdateRoomStatusRequest
                    {
                        roomId = roomId,
                        status = RoomStatus.Error,
                    });
                }
            }
        }
    }
}
