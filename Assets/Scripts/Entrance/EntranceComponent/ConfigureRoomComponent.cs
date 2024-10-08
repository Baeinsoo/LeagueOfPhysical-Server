using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

namespace LOP
{
    public class ConfigureRoomComponent : IEntranceComponent
    {
        public async Task Execute()
        {
            string roomId = null;
            ushort port = 0;

            try
            {
#if UNITY_EDITOR
                roomId = "EditorTestRoom";
                port = 7777;
                Blackboard.Write("port", port);

                RoomDto room = new RoomDto
                {
                    id = "EditorTestRoom",
                    matchId = "EditorTestMatch",
                    status = RoomStatus.Initializing,
                    ip = "localhost",
                    port = 0,
                };
                Blackboard.Write(room);

                MatchDto match = new MatchDto
                {
                    id = "EditorTestMatch",
                    matchType = MatchType.Friendly,
                    subGameId = "FlapWang",
                    mapId = "FlapWangMap",
                    targetRating = 1500,
                    playerList = new string[]
                    {
                        "375f9694a1e5c3af13ff9c75e11e1cb158f65521",
                    }
                };
                Blackboard.Write(match);
#else
                roomId = Environment.GetEnvironmentVariable("ROOM_ID");
                port = ushort.Parse(Environment.GetEnvironmentVariable("PORT"));
                Blackboard.Write("port", port);
                
                var getRoom = await WebAPI.GetRoom(roomId);
                Blackboard.Write(getRoom.response.room);

                var getMatch = await WebAPI.GetMatch(getRoom.response.room.matchId);
                Blackboard.Write(getMatch.response.match);
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
