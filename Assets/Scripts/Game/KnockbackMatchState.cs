using System.Collections.Generic;

namespace LOP
{
    /// <summary>넉백 생존전 판 상태(서버). 누가 참가했고 누가 언제 떨어졌나.</summary>
    public class KnockbackMatchState
    {
        /// <summary>참가한 몸의 엔티티 id. 룰이 몸을 세울 때 채운다.</summary>
        public readonly List<string> Players = new List<string>();
        public readonly Dictionary<string, long> EliminatedTick = new Dictionary<string, long>();
        public readonly List<(string entityId, long tick)> Eliminations = new List<(string, long)>();

        /// <summary>떨어짐 판정이 마지막으로 돈 틱 — 룰이 "마지막 탈락 뒤 얼마나 지났나"를 잰다.</summary>
        public long LastTick;

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
