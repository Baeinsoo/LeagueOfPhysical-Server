using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 장외 막타 크레딧. 몸마다 "마지막으로 날린 사람"을 기억했다가, 떨어질 때 누가 장외시켰는지 알려 준다.
    /// 탄에 맞으면 쏜 사람, 날아가는 몸에 부딪혀 날아가면(연쇄) 부딪혀 온 몸의 크레딧을 물려받는다.
    /// </summary>
    public sealed class KnockbackKoCredit
    {
        //  이 시간 안에 떨어져야 크레딧이 유효하다. 오래 전에 맞고 제 발로 떨어진 건 자멸이다.
        public const float WindowSeconds = 6f;

        private readonly struct Record
        {
            public readonly string By;
            public readonly long Tick;
            public readonly bool Chain;

            public Record(string by, long tick, bool chain)
            {
                By = by;
                Tick = tick;
                Chain = chain;
            }
        }

        private readonly Dictionary<string, Record> records = new Dictionary<string, Record>();
        private readonly long windowTicks;

        public KnockbackKoCredit(float tickInterval = 0.02f)
        {
            windowTicks = (long)System.Math.Round(WindowSeconds / tickInterval);
        }

        /// <summary>탄에 맞았다. 앞 기록은 덮인다.</summary>
        public void RecordShotHit(string victimId, string shooterId, long tick)
        {
            records[victimId] = new Record(shooterId, tick, false);
        }

        /// <summary>
        /// 연쇄로 날아간 몸들에 부딪혀 온 몸의 크레딧을 물려준다. 출처에 (유효한) 크레딧이 없거나,
        /// 물려받을 사람이 자기 자신이나 같은 팀이면 아무것도 하지 않는다.
        /// </summary>
        public void ApplyChains(IReadOnlyList<(string targetId, string sourceId)> chains, long tick,
                                IReadOnlyDictionary<string, int> teamOf)
        {
            //  한 묶음은 같은 순간의 일이다 — 앞 항목이 쓴 장부를 뒤 항목이 출처로 읽으면 순서가 결과를 바꾼다.
            var before = new Dictionary<string, Record>(records);
            for (int i = 0; i < chains.Count; i++)
            {
                var (targetId, sourceId) = chains[i];
                if (!before.TryGetValue(sourceId, out var source) || tick - source.Tick > windowTicks)
                {
                    continue;
                }
                if (source.By == targetId)
                {
                    continue;
                }
                if (KnockbackTeams.SameTeam(TeamOf(teamOf, source.By), TeamOf(teamOf, targetId)))
                {
                    continue;
                }
                records[targetId] = new Record(source.By, tick, true);
            }
        }

        /// <summary>탈락 순간에 부른다. 자멸이면 by는 빈 값.</summary>
        public void Resolve(string victimId, long tick, out string by, out bool chain)
        {
            if (records.TryGetValue(victimId, out var record) && tick - record.Tick <= windowTicks)
            {
                by = record.By;
                chain = record.Chain;
                return;
            }
            by = string.Empty;
            chain = false;
        }

        private static int TeamOf(IReadOnlyDictionary<string, int> teamOf, string id)
            => teamOf != null && teamOf.TryGetValue(id, out var team) ? team : 0;
    }
}
