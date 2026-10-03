using System.Collections.Generic;

namespace LOP
{
    /// <summary>한 사람의 목숨. 탈락하지 않았으면 EliminatedTick은 -1.</summary>
    public class DodgePlayerLife
    {
        public int Lives;
        public long InvulnerableUntilTick;
        public long EliminatedTick = -1;
        public bool Alive => EliminatedTick < 0;
    }

    /// <summary>
    /// 피하기 판 상태(서버 권위). 떠 있는 패턴, 사람별 목숨, 탈락 기록.
    /// 바뀔 때마다 <see cref="MarkChanged"/> — 방송 시스템이 판본을 보고 다시 보낸다.
    /// </summary>
    public class DodgeMatchState
    {
        public readonly List<DodgePattern> Patterns = new List<DodgePattern>();
        public readonly Dictionary<string, DodgePlayerLife> Players = new Dictionary<string, DodgePlayerLife>();
        public readonly List<(string entityId, long tick)> Eliminations = new List<(string, long)>();

        public int Version { get; private set; }

        /// <summary>판정 시스템이 마지막으로 돈 틱 — 룰이 "마지막 탈락 뒤 얼마나 지났나"를 잰다.</summary>
        public long LastTick;

        public void MarkChanged() => Version++;

        public int AliveCount
        {
            get
            {
                int n = 0;
                foreach (var life in Players.Values)
                {
                    if (life.Alive) n++;
                }
                return n;
            }
        }
    }
}
