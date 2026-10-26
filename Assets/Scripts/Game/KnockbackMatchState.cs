using System.Collections.Generic;

namespace LOP
{
    /// <summary>넉백 생존전 판 상태(서버). 누가 참가했고 누가 언제 떨어졌나.</summary>
    public class KnockbackMatchState
    {
        /// <summary>참가한 몸의 엔티티 id. 룰이 몸을 세울 때 채운다.</summary>
        public readonly List<string> Players = new List<string>();
        /// <summary>Players 중 시험용 봇의 몸 id. 봇 시스템만 읽는다.</summary>
        public readonly HashSet<string> Bots = new HashSet<string>();
        public readonly Dictionary<string, long> EliminatedTick = new Dictionary<string, long>();
        public readonly List<(string entityId, long tick)> Eliminations = new List<(string, long)>();

        /// <summary>떨어짐 판정이 마지막으로 돈 틱 — 룰이 "마지막 탈락 뒤 얼마나 지났나"를 잰다.</summary>
        public long LastTick;

        /// <summary>서버가 공개한 원 단계(순서대로). 한 번 공개하면 바뀌지 않는다.</summary>
        public readonly List<ZoneStage> ZoneStages = new List<ZoneStage>();

        /// <summary>바뀔 때마다 오른다 — 방송 시스템이 판본을 보고 다시 보낸다.</summary>
        public int Version { get; private set; }

        public void MarkChanged() => Version++;

        public void AddZoneStage(ZoneStage stage)
        {
            ZoneStages.Add(stage);
            MarkChanged();
        }

        /// <summary>탈락시키는 유일한 길. 이미 탈락했으면 false.</summary>
        public bool Eliminate(string entityId, long tick)
        {
            if (EliminatedTick.ContainsKey(entityId)) return false;
            EliminatedTick[entityId] = tick;
            Eliminations.Add((entityId, tick));
            MarkChanged();
            return true;
        }

        public int AliveCount()
        {
            int n = 0;
            foreach (var id in Players)
            {
                if (EliminatedTick.ContainsKey(id) == false) n++;
            }
            return n;
        }
    }
}
