using GameFramework;

namespace LOP
{
    public class RoomDataStore : IRoomDataStore
    {
        public Room room { get; set; }
        public Match match { get; set; }

        public RoomDataStore()
        {
            EventBus.Default.Subscribe<GetMatchResponse>(EventTopic.WebResponse, HandleGetMatch);
            EventBus.Default.Subscribe<GetRoomResponse>(EventTopic.WebResponse, HandleGetRoom);
            EventBus.Default.Subscribe<UpdateRoomStatusResponse>(EventTopic.WebResponse, HandleUpdateRoomStatus);
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
