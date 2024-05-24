using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class NotifyStartServerRequest
    {
        public string roomId;
        public string matchId;
        public string[] expectedPlayerList;
        public MatchSetting matchSetting;
        public string ip;
        public int port;
    }
}
