using System;
using System.Collections.Generic;
using MessagePipe;
using UnityEngine;

namespace LOP
{
    public class RoomDataStore : IRoomDataStore, IDisposable
    {
        public Room room { get; set; }
        public Match match { get; set; }
        public MatchOutcome outcome { get; set; }
        public IReadOnlyDictionary<string, PlayerLookDto> looks { get; set; }

        private readonly IDisposable subscriptions;

        public RoomDataStore(
            ISubscriber<GetMatchResponse> getMatchSubscriber,
            ISubscriber<GetMatchLooksResponse> getMatchLooksSubscriber,
            ISubscriber<GetRoomResponse> getRoomSubscriber,
            ISubscriber<UpdateRoomStatusResponse> updateRoomStatusSubscriber)
        {
            var bag = DisposableBag.CreateBuilder();
            getMatchSubscriber.Subscribe(HandleGetMatch).AddTo(bag);
            getMatchLooksSubscriber.Subscribe(HandleGetMatchLooks).AddTo(bag);
            getRoomSubscriber.Subscribe(HandleGetRoom).AddTo(bag);
            updateRoomStatusSubscriber.Subscribe(HandleUpdateRoomStatus).AddTo(bag);
            subscriptions = bag.Build();
        }

        public void Dispose()
        {
            subscriptions.Dispose();
        }

        private void HandleGetMatch(GetMatchResponse response)
        {
            match = MapperConfig.mapper.Map<Match>(response.match);
        }

        //  실패(타임아웃·4xx/5xx)해도 looks를 null로 돌려 "전원 기본 룩"으로 가게 한다 — 룩 조회는
        //  방을 여는 데 필수 조건이 아니다.
        private void HandleGetMatchLooks(GetMatchLooksResponse response)
        {
            if (response.code != 200)
            {
                Debug.LogWarning($"룩 조회 응답이 실패 코드(code={response.code}) — 전원 기본 룩으로 진행");
            }

            looks = response.code == 200 ? response.looks : null;
        }

        private void HandleGetRoom(GetRoomResponse response)
        {
            if (response.room == null)
            {
                return;
            }

            room = MapperConfig.mapper.Map<Room>(response.room);
        }

        private void HandleUpdateRoomStatus(UpdateRoomStatusResponse response)
        {
            if (response.room == null)
            {
                return;
            }

            room = MapperConfig.mapper.Map<Room>(response.room);
        }

        public void Clear()
        {
            room = null;
            match = null;
            //  안 지우면 같은 프로세스가 다음 판을 시작했을 때 지난 판의 등수가 남아,
            //  아직 러너가 새 등수를 채우기 전(EndMatch 호출 전) 방이 닫히는 경로에서
            //  엉뚱한 등수가 새 matchId로 보고될 수 있다.
            outcome = null;
            looks = null;
        }
    }
}
