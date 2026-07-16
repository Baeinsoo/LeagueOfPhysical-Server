using System;
using MessagePipe;

namespace LOP
{
    public class RoomDataStore : IRoomDataStore, IDisposable
    {
        public Room room { get; set; }
        public Match match { get; set; }

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
        }
    }
}
