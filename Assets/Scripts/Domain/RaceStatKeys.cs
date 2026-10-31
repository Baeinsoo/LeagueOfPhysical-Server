using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 레이스형 모드가 함께 쓰는 결과 지표 키(spec 2026-10-30 §4). 로비 퀘스트 표의 clauses가 같은 문자열로 찾는다.
    /// Skydive도 결승선이 있어 나중에 같은 키를 낸다.
    /// </summary>
    public static class RaceStatKeys
    {
        /// <summary>결승선을 넘었으면 1, 아니면 0.</summary>
        public const string Finished = "finished";

        /// <summary>참가자의 등수 줄마다 완주 여부를 싣는다(0 포함 — "서버가 0이라 했다"와 "옛 서버라 안 보냈다"가 갈리게). 다른 키는 그대로 둔다.</summary>
        public static void AttachFinished(MatchOutcome outcome, IEnumerable<string> participants, ICollection<string> finished)
        {
            var who = new HashSet<string>(participants);
            foreach (var placement in outcome.placements)
            {
                if (placement.userId == null || who.Contains(placement.userId) == false)
                {
                    continue;
                }
                placement.stats ??= new Dictionary<string, int>();
                placement.stats[Finished] = finished.Contains(placement.userId) ? 1 : 0;
            }
        }
    }
}
