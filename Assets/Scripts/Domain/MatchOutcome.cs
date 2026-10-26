using System;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>한 판이 끝났을 때 게임이 내놓는 등수. 1이 1등이고 같은 값이면 동점이다.</summary>
    public class MatchOutcome
    {
        public List<MatchPlacement> placements = new List<MatchPlacement>();

        //  봇이 없어도 룰이 "이 판은 연습"이라고 정할 수 있다(넉백 슬라이스 1: 아직 남을 떨어뜨릴 수 없어서).
        public bool practice;
    }

    [Serializable]
    public class MatchPlacement
    {
        public string userId;
        public int placement;

        // 모드별 결과 지표. 키는 모드가 정한다(활쏘기: ArcheryStatKeys). 점수 개념이 없는 모드는 비운다.
        public Dictionary<string, int> stats;

        //  로비가 이 값으로 보상(코인·경험치)을 계산한다. 나간 사람은 로비가 left로 보고 0 처리한다.
        public int playedSeconds;
    }
}
