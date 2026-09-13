using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 점수로 등수를 매기는 규칙. 게임을 모르는 순수 계산이다 — 점수로 겨루는 게임이 모두 같은 답을 낸다.
    /// 동점은 <b>공동 순위</b>이고 다음 등수는 그만큼 건너뛴다(1·1·3) — <see cref="FinishPlacements"/>와 같은 관례.
    /// </summary>
    public static class ScorePlacements
    {
        public static MatchOutcome Resolve(IReadOnlyList<(string userId, int score)> scores)
        {
            var sorted = new List<(string userId, int score)>(scores);
            //  점수가 같으면 사람 id로 갈라 순서를 못박는다 — 안 그러면 목록에 담긴 차례(딕셔너리
            //  순회 순서)가 결과 화면의 줄 순서를 정해 판마다 달라진다.
            sorted.Sort((a, b) =>
            {
                int byScore = b.score.CompareTo(a.score);
                return byScore != 0 ? byScore : string.CompareOrdinal(a.userId, b.userId);
            });

            var outcome = new MatchOutcome();
            int placement = 0;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i == 0 || sorted[i].score != sorted[i - 1].score)
                {
                    placement = i + 1;
                }
                outcome.placements.Add(new MatchPlacement { userId = sorted[i].userId, placement = placement });
            }
            return outcome;
        }
    }
}
