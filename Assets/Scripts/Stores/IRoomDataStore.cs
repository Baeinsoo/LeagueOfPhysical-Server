using GameFramework;
using UnityEngine;

namespace LOP
{
    public interface IRoomDataStore : IDataStore
    {
        Room room { get; set; }
        Match match { get; set; }

        /// <summary>이번 판의 등수. 매치가 끝나는 순간 러너가 채우고, 방이 닫히기 전에 보고에 실린다.</summary>
        MatchOutcome outcome { get; set; }
    }
}
