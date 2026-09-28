using System.Collections.Generic;
using System.Linq;

namespace LOP
{
    /// <summary>탈락 순서 → 등수. 살아 남은 사람은 모두 1등, 그 뒤는 늦게 탈락할수록 앞선다. 같은 틱 탈락은 공동 순위.</summary>
    public static class DodgePlacements
    {
        public static MatchOutcome Resolve(IReadOnlyList<string> aliveUserIds,
                                           IReadOnlyList<(string userId, long tick)> eliminations)
        {
            var outcome = new MatchOutcome();
            foreach (var user in aliveUserIds)
            {
                outcome.placements.Add(new MatchPlacement { userId = user, placement = 1 });
            }

            int taken = aliveUserIds.Count;
            foreach (var group in eliminations.GroupBy(e => e.tick).OrderByDescending(g => g.Key))
            {
                int rank = taken + 1;
                foreach (var (userId, _) in group)
                {
                    outcome.placements.Add(new MatchPlacement { userId = userId, placement = rank });
                }
                taken += group.Count();
            }
            return outcome;
        }
    }
}
