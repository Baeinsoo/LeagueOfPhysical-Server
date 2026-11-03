using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>떨어졌거나 떨어질 예정인 상자 하나. 떨어지는 틱 전에는 예고만 있고 주울 수 없다.</summary>
    public readonly struct KnockbackCrate
    {
        public readonly int Id;
        public readonly long LandTick;
        public readonly Vector2 Position;

        public KnockbackCrate(int id, long landTick, Vector2 position)
        {
            Id = id;
            LandTick = landTick;
            Position = position;
        }
    }

    /// <summary>넉백 생존전 판 상태(서버). 누가 참가했고 누가 언제 떨어졌나.</summary>
    public class KnockbackMatchState
    {
        /// <summary>참가한 몸의 엔티티 id. 룰이 몸을 세울 때 채운다.</summary>
        public readonly List<string> Players = new List<string>();
        /// <summary>Players 중 시험용 봇의 몸 id. 봇 시스템만 읽는다.</summary>
        public readonly HashSet<string> Bots = new HashSet<string>();
        /// <summary>몸 id → 팀 번호(0 = 개인전). 룰이 자리를 나눌 때 채운다.</summary>
        public readonly Dictionary<string, int> Team = new Dictionary<string, int>();
        /// <summary>0 = 개인전, 2 = 팀전.</summary>
        public int TeamCount;
        public readonly Dictionary<string, long> EliminatedTick = new Dictionary<string, long>();
        /// <summary>몸 id → 장외시킨 사람 id. 자멸이면 항목이 없다.</summary>
        public readonly Dictionary<string, string> KoBy = new Dictionary<string, string>();
        /// <summary>연쇄(날아가는 몸에 부딪힘)로 장외된 몸 id.</summary>
        public readonly HashSet<string> KoChain = new HashSet<string>();
        /// <summary>막타 크레딧 장부. 탄 맞음·연쇄가 쓰고 Eliminate가 읽는다.</summary>
        public readonly KnockbackKoCredit Credit = new KnockbackKoCredit();
        public readonly List<(string entityId, long tick)> Eliminations = new List<(string, long)>();

        /// <summary>떨어짐 판정이 마지막으로 돈 틱 — 룰이 "마지막 탈락 뒤 얼마나 지났나"를 잰다.</summary>
        public long LastTick;

        /// <summary>서버가 공개한 원 단계(순서대로). 한 번 공개하면 바뀌지 않는다.</summary>
        public readonly List<ZoneStage> ZoneStages = new List<ZoneStage>();

        /// <summary>지금 상자(충격파 폭탄이 든). 없으면 null. 아무도 안 주운 동안만 있다.</summary>
        public KnockbackCrate? Crate { get; private set; }

        /// <summary>다음에 예고할 상자 번호. 1부터, 상자마다 다르다.</summary>
        public int NextCrateId { get; private set; } = 1;

        public KnockbackCrate AnnounceCrate(long landTick, Vector2 position)
        {
            var crate = new KnockbackCrate(NextCrateId++, landTick, position);
            Crate = crate;
            MarkChanged();
            return crate;
        }

        public void ClearCrate()
        {
            Crate = null;
            MarkChanged();
        }

        /// <summary>금 간 칸 번호 → (금 수, 무너지는 틱 — 아직이면 -1). 금 가는 바닥 맵만 쓴다. 판정용 사실은 바닥(KnockbackFloor)에도 넣는다.</summary>
        public readonly SortedDictionary<int, (int cracks, long collapseTick)> Cracks = new SortedDictionary<int, (int, long)>();

        /// <summary>바뀔 때마다 오른다 — 방송 시스템이 판본을 보고 다시 보낸다.</summary>
        public int Version { get; private set; }

        public void MarkChanged() => Version++;

        public void AddZoneStage(ZoneStage stage)
        {
            ZoneStages.Add(stage);
            MarkChanged();
        }

        /// <summary>팀전에서 승부가 정해진 틱(-1 = 아직). 살아 있는 팀이 하나 이하가 된 첫 틱이다.</summary>
        public long TeamDecidedTick { get; private set; } = -1;

        /// <summary>
        /// 승부가 정해진 틱이 끝났을 때 살아 있던 몸들 = 이긴 팀. 비어 있으면 같은 틱 동시 전멸(무승부).
        /// 끝나기를 기다리는 사이 이긴 팀이 떨어져도 승부는 바뀌지 않는다.
        /// </summary>
        public readonly HashSet<string> TeamWinners = new HashSet<string>();

        public bool TeamDecided => TeamDecidedTick >= 0;

        /// <summary>탈락시키는 유일한 길. 이미 탈락했으면 false.</summary>
        public bool Eliminate(string entityId, long tick)
        {
            if (EliminatedTick.ContainsKey(entityId)) return false;
            EliminatedTick[entityId] = tick;
            Credit.Resolve(entityId, tick, out var koBy, out var koChain);
            if (koBy.Length > 0)
            {
                KoBy[entityId] = koBy;
                if (koChain) KoChain.Add(entityId);
            }
            Eliminations.Add((entityId, tick));
            DecideTeamResult(entityId, tick);
            MarkChanged();
            return true;
        }

        private void DecideTeamResult(string entityId, long tick)
        {
            if (TeamCount < 2) return;
            if (TeamDecided)
            {
                //  같은 틱에 떨어진 사람은 "그 틱에 살아 있던" 쪽이 아니다 — 같은 틱 동시 전멸은 무승부.
                if (tick == TeamDecidedTick) TeamWinners.Remove(entityId);
                return;
            }
            var alive = new List<string>();
            foreach (var id in Players)
            {
                if (EliminatedTick.ContainsKey(id) == false) alive.Add(id);
            }
            if (KnockbackTeams.TeamMatchOver(Team, alive) == false) return;
            TeamDecidedTick = tick;
            foreach (var id in alive) TeamWinners.Add(id);
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
