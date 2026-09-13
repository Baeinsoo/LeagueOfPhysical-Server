using System;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>한 판이 끝났을 때 게임이 내놓는 등수. 1이 1등이고 같은 값이면 동점이다.</summary>
    public class MatchOutcome
    {
        public List<MatchPlacement> placements = new List<MatchPlacement>();
    }

    [Serializable]
    public class MatchPlacement
    {
        public string userId;
        public int placement;

        // 모드별 결과 지표. 키는 모드가 정한다(활쏘기: ArcheryStatKeys). 점수 개념이 없는 모드는 비운다.
        public Dictionary<string, int> stats;
    }
}
