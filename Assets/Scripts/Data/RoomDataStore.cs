using GameFramework;

namespace LOP
{
    public class RoomDataStore : IRoomDataStore
    {
        public Room room { get; set; }
        public Match match { get; set; }

        [DataListen(typeof(GetMatchResponse))]
        private void HandleGetMatch(GetMatchResponse response)
        {
            match = MapperConfig.mapper.Map<Match>(response.match);
        }

        [DataListen(typeof(GetRoomResponse))]
        private void HandleGetRoom(GetRoomResponse response)
        {
            if (response.room == null)
            {
                return;
            }

            room = MapperConfig.mapper.Map<Room>(response.room);
        }

        [DataListen(typeof(UpdateRoomStatusResponse))]
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
