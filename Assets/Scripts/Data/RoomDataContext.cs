using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace LOP
{
    public class RoomDataContext : IRoomDataContext
    {
        public Type[] subscribedTypes => new Type[]
        {
            typeof(GetMatchResponse),
            typeof(GetRoomResponse),
            typeof(UpdateRoomStatusResponse),
        };

        private Dictionary<Type, Action<object>> updateHandlers;

        public Room room { get; set; }
        public Match match { get; set; }

        public RoomDataContext()
        {
            updateHandlers = new Dictionary<Type, Action<object>>
            {
                { typeof(GetMatchResponse), data => HandleGetMatch((GetMatchResponse)data) },
                { typeof(GetRoomResponse), data => HandleGetRoom((GetRoomResponse)data) },
                { typeof(UpdateRoomStatusResponse), data => HandleUpdateRoomStatus((UpdateRoomStatusResponse)data) },
            };
        }

        public void UpdateData<T>(T data)
        {
            if (updateHandlers.TryGetValue(data.GetType(), out var handler))
            {
                handler(data);
            }
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
            updateHandlers.Clear();
        }
    }
}
