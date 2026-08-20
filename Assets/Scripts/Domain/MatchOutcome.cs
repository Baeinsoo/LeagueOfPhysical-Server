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
    }
}
