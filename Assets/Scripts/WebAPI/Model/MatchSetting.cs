using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [Serializable]
    public struct MatchSetting
    {
        public MatchType matchType;
        public string subGameId;
        public string mapId;

        public MatchSetting(MatchType matchType, string subGameId, string mapId)
        {
            this.matchType = matchType;
            this.subGameId = subGameId;
            this.mapId = mapId;
        }
    }
}
