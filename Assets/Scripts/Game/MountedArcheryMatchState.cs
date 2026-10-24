using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 말 타고 활쏘기 판 상태(서버 권위). 사람별 탈락 여부·처치자·표시 이름과 탈락 기록.
    /// 바뀔 때마다 판본을 올린다 — 방송 시스템이 판본을 보고 다시 보낸다.
    /// </summary>
    public sealed class MountedArcheryMatchState
    {
        public sealed class Player
        {
            public string UserId;
            public string DisplayName;
            public long EliminatedTick = -1;
            public string KillerEntityId = "";
            public bool Alive => EliminatedTick < 0;
        }

        private readonly Dictionary<string, Player> players = new Dictionary<string, Player>();

        /// <summary>엔티티 id → 사람.</summary>
        public IReadOnlyDictionary<string, Player> Players => players;

        /// <summary>탈락 순서대로. 순위는 이 역순이고, 같은 틱은 공동이다.</summary>
        public readonly List<(string userId, long tick)> Eliminations = new List<(string, long)>();

        /// <summary>서버가 공개한 원 단계(순서대로). 한 번 공개하면 바뀌지 않는다.</summary>
        public readonly List<ZoneStage> ZoneStages = new List<ZoneStage>();

        public long LastEliminationTick { get; private set; } = -1;

        /// <summary>판정 시스템이 마지막으로 돈 틱 — 룰이 "마지막 탈락 뒤 얼마나 지났나"를 잰다.</summary>
        public long LastTick;

        public int Version { get; private set; }

        public void MarkChanged() => Version++;

        public int AliveCount
        {
            get
            {
                int n = 0;
                foreach (var player in players.Values)
                {
                    if (player.Alive) n++;
                }
                return n;
            }
        }

        public void Add(string entityId, string userId, string displayName)
        {
            players[entityId] = new Player { UserId = userId, DisplayName = displayName };
            MarkChanged();
        }

        public void AddZoneStage(ZoneStage stage)
        {
            ZoneStages.Add(stage);
            MarkChanged();
        }

        /// <summary>
        /// 탈락시키는 유일한 길. 화살이든 원 밖이든 여기를 거쳐야 기록·판본이 한 모양으로 남는다.
        /// 이미 탈락했거나 모르는 사람이면 아무것도 안 한다.
        /// </summary>
        public bool Eliminate(string entityId, string killerEntityId, long tick)
        {
            if (players.TryGetValue(entityId, out var player) == false || player.Alive == false)
            {
                return false;
            }

            player.EliminatedTick = tick;
            player.KillerEntityId = killerEntityId ?? "";
            Eliminations.Add((player.UserId, tick));
            LastEliminationTick = tick;
            MarkChanged();
            return true;
        }
    }
}
