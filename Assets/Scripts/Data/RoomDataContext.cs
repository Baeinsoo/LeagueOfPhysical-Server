using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public partial class RoomDataContext : IDataContext
    {
        public RoomDto room;
        public MatchDto match;

        public RoomDataContext() { }

        public void Clear()
        {
            room = null;
            match = null;
        }
    }
}
