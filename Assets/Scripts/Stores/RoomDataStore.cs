using System;
using MessagePipe;

namespace LOP
{
    public class RoomDataStore : IRoomDataStore, IDisposable
    {
        public Room room { get; set; }
        public Match match { get; set; }
        public MatchOutcome outcome { get; set; }

        private readonly IDisposable subscriptions;

        public RoomDataStore(
            ISubscriber<GetMatchResponse> getMatchSubscriber,
            ISubscriber<GetRoomResponse> getRoomSubscriber,
            ISubscriber<UpdateRoomStatusResponse> updateRoomStatusSubscriber)
        {
            var bag = DisposableBag.CreateBuilder();
            getMatchSubscriber.Subscribe(HandleGetMatch).AddTo(bag);
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
        }
    }
}
