using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [Serializable]
    public class Room
    {
        public string id;
        public string matchId;
        public RoomStatus status;
        public string ip;
        public int port;
    }

    [Serializable]
    public enum RoomStatus
    {
        None = 0,
        Spawning = 1,
        Spawned = 2,
        Ready = 3,
        Playing = 4,
        Finished = 5,
    }
}
