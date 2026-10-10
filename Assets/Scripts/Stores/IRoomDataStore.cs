using GameFramework;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public interface IRoomDataStore : IDataStore
    {
        Room room { get; set; }
        Match match { get; set; }

        /// <summary>이번 판의 등수. 매치가 끝나는 순간 러너가 채우고, 방이 닫히기 전에 보고에 실린다.</summary>
        MatchOutcome outcome { get; set; }

        /// <summary>방을 열 때 로비에서 받은 참가자 룩. null = 조회 실패/미조회 → 전원 기본 룩으로 진행.</summary>
        IReadOnlyDictionary<string, PlayerLookDto> looks { get; set; }
    }
}
