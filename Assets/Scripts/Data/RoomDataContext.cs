using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework;
using System;

namespace LOP
{
    public partial class RoomDataContext : IDataContext
    {
        public Type[] subscribedTypes => new Type[] { typeof(RoomDto), typeof(MatchDto) };

        public RoomDto room;
        public MatchDto match;

        public void UpdateData<T>(T data)
        {
            if (data is RoomDto roomDto)
            {
                room = roomDto;
            }
            else if (data is MatchDto matchDto)
            {
                match = matchDto;
            }
        }

        public void Clear()
        {
            room = null;
            match = null;
        }
    }
}
