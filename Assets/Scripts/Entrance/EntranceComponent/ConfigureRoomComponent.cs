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
        private IDataContextManager dataManager;

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

                dataManager.Get<RoomDataContext>().room = room;

                Match match = new Match
                {
                    id = "EditorTestMatch",
                    matchType = GameMode.Normal,
                    subGameId = "FlapWang",
                    mapId = "FlapWangMap",
                    targetRating = 1500,
                    playerList = new string[]
                    {
                        "375f9694a1e5c3af13ff9c75e11e1cb158f65521",
                    }
                };
                dataManager.Get<RoomDataContext>().match = match;
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
