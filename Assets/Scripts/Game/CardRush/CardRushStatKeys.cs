using System;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>카드 러시 결과 지표 키. 로비 퀘스트 표의 clauses가 같은 문자열로 찾는다.</summary>
    public static class CardRushStatKeys
    {
        /// <summary>판 중 가장 길게 이어 맞힌 수.</summary>
        public const string StreakBest = "cardStreakBest";

        /// <summary>판 중 틀린 횟수.</summary>
        public const string Misses = "cardMisses";

        /// <summary>
        /// 참가자의 등수 줄마다 두 지표를 싣는다. 몸이 이미 없어 상태를 모르면 0을 싣는다
        /// (0 포함 — "서버가 0이라 했다"와 "옛 서버라 안 보냈다"가 갈리게, <see cref="RaceStatKeys.AttachFinished"/>와 같은 관례).
        /// </summary>
        /// <param name="stateOf">몸 id → 그 몸의 지금 상태. 몸이 없으면 null.</param>
        public static void Attach(MatchOutcome outcome, IReadOnlyDictionary<string, string> userIdToEntityId,
                                  Func<string, CardRushRunnerState?> stateOf)
        {
            foreach (var placement in outcome.placements)
            {
                if (placement.userId == null || userIdToEntityId.TryGetValue(placement.userId, out string entityId) == false)
                {
                    continue;
                }
                var state = stateOf(entityId);
                placement.stats ??= new Dictionary<string, int>();
                placement.stats[StreakBest] = state?.BestStreak ?? 0;
                placement.stats[Misses] = state?.Misses ?? 0;
            }
        }
    }
}
