using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 결승선 기록으로 등수를 매기는 규칙. 물리도 축도 게임도 모르는 순수 계산이라, 레이스형 게임이
    /// 모두 같은 답을 낸다(Flappy·Skydive가 이걸 공유한다).
    ///
    /// <para>세 무리를 이 순서로 놓는다: <b>닿은 사람</b>(먼저 닿은 순, 완전히 같으면 공동 순위) →
    /// <b>못 닿은 사람</b>(더 멀리 간 순) → <b>몸이 사라진 사람</b>(나간 사람).</para>
    /// </summary>
    public static class FinishPlacements
    {
        /// <param name="finished">먼저 닿은 순으로 정렬된 기록(<see cref="FinishOrderTracker.Ordered"/>).</param>
        /// <param name="entityIdToUserId">기록은 몸 id로 남으므로 사람 id로 옮길 대응표.</param>
        /// <param name="unfinished">못 닿은 사람과 그 진행도. <b>클수록 앞선 것</b>으로 본다
        /// (Flappy는 x 그대로, Skydive는 아래로 갈수록 앞서므로 −y를 넘긴다).</param>
        /// <param name="left">몸이 이미 없는 사람(나간 사람).</param>
        public static MatchOutcome Resolve(
            IReadOnlyList<FinishRecord> finished,
            IReadOnlyDictionary<string, string> entityIdToUserId,
            IReadOnlyList<(string userId, float progress)> unfinished,
            IReadOnlyList<string> left)
        {
            var outcome = new MatchOutcome();

            //  공동 순위는 다음 등수를 그만큼 건너뛴다(1,1,3) — 스포츠 표준.
            int placement = 0;
            int counted = 0;
            for (int i = 0; i < finished.Count; i++)
            {
                if (entityIdToUserId.TryGetValue(finished[i].EntityId, out string userId) == false)
                {
                    continue;   // 대응표에 없을 수 없지만, 있어도 등수를 못 매길 뿐 판이 죽으면 안 된다
                }
                //  건너뛴 몫은 "지금까지 실제로 매긴 사람 수"로 센다 — 대응표에 없어 건너뛴 기록이
                //  있으면 i는 그만큼 앞서 있어서 등수에 구멍이 생긴다.
                if (counted == 0 || finished[i].SameRankAs(finished[i - 1]) == false)
                {
                    placement = counted + 1;
                }
                outcome.placements.Add(new MatchPlacement { userId = userId, placement = placement });
                counted++;
            }

            var sorted = new List<(string userId, float progress)>(unfinished);
            sorted.Sort((a, b) => b.progress.CompareTo(a.progress));

            int next = counted + 1;
            for (int i = 0; i < sorted.Count; i++)
            {
                outcome.placements.Add(new MatchPlacement { userId = sorted[i].userId, placement = next++ });
            }
            for (int i = 0; i < left.Count; i++)
            {
                outcome.placements.Add(new MatchPlacement { userId = left[i], placement = next++ });
            }

            return outcome;
        }
    }
}
