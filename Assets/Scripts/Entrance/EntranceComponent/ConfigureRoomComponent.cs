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
        private readonly IRoomDataStore roomDataStore;

        public ConfigureRoomComponent(IRoomDataStore roomDataStore)
        {
            this.roomDataStore = roomDataStore;
        }

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
                    queueId = 1,
                    rounds = new MatchRound[]
                    {
                        new MatchRound { index = 0, gameModeId = 1, mapId = 1 },
                    },
                    targetMmr = 1500,
                    playerList = new string[]
                    {
                        "5f3a8505-2fc0-42d4-9810-af0fcd3cfdf1",   // 메인 에디터 게스트 (로컬 픽스처)
                        "6d1682d8-3e97-4990-9429-89d387c61972",   // MPPM 가상 플레이어 게스트
                        //"119bef82-8d1c-466b-ad3f-182f41672922",
                        //"d11fc5f5-a948-4690-9eba-69a819376f91",
                        //"ae6764c1-4469-442d-a89c-709badeb997b",
                        //"c503d4cc-035c-44cd-9be0-7e828017bb68",
                        //"f7ed95fc-a74a-496b-b11c-a783a5097c91",
                        //"a2401694-e2b7-4cc6-967e-4292e7c22c62",
                    }
                };
                roomDataStore.match = match;
#else
                roomId = Environment.GetEnvironmentVariable("ROOM_ID");
                port = ushort.Parse(Environment.GetEnvironmentVariable("PORT"));
                Blackboard.Write("port", port);
                
                var getRoom = await WebAPI.GetRoom(roomId);
                var getMatch = await WebAPI.GetMatch(getRoom.room.matchId);
#endif
            }
            catch (Exception e)
            {
                Debug.LogException(e);

                // roomId를 아는 경우에만 그 룸을 Error로 보고할 수 있다. 조건이 반대로 되어 있어서,
                // 정작 보고해야 할 때(roomId를 아는데 조회가 실패한 경우)는 건너뛰고
                // 알 수 없을 때 빈 roomId로 요청을 보냈다. 그래서 실패가 룸 서버에 전달되지 않고
                // 하트비트 타임아웃(60초)으로만 정리됐다.
                if (!string.IsNullOrEmpty(roomId))
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
