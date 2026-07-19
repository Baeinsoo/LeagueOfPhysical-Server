using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [Serializable]
    public struct RankingData
    {
        public string playerId;
        public int ranking;

        public RankingData(string playerId, int ranking)
        {
            this.playerId = playerId;
            this.ranking = ranking;
        }
    }
}
