using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>둘레 위 자리와 그 자리에서 가운데를 보는 각. rotationDegrees는 기둥 고리를 돌린 각과 같게 줘서 둘이 같이 돈다.</summary>
    public static class KnockbackSpawn
    {
        public static Vector3 Position(int index, int count, float radius, float rotationDegrees = 0f)
        {
            float theta = (index / (float)count * 360f + rotationDegrees) * Mathf.Deg2Rad;
            return new Vector3(radius * Mathf.Sin(theta), 0f, radius * Mathf.Cos(theta));
        }

        public static float YawFacingCenter(Vector3 p) => Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// 넉백 생존전 룰(서버). 참가자를 바닥 둘레에 둥글게 세우고, 떨어지지 않은 사람이 한 명 이하가 되면 끝난다.
    /// 등수는 탈락 순서의 역순(피하기와 같은 규칙). 떨어짐 자체는 KnockbackFallSystem이 정한다.
    /// </summary>
    public class KnockbackRuleSystem : IGameRuleSystem, ISettledResults
    {
        private const float TickInterval = 0.02f;
        private const string BodyCharacterCode = "character_001";

        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;
        private readonly KnockbackMatchState state;
        private readonly KnockbackConfig config;
        private readonly KnockbackTestBotFill fillTo;
        private readonly KnockbackMatchMode mode;
        private readonly KnockbackPillars pillars;
        private readonly IMatchSeed seed;
        private readonly Dictionary<string, string> entityIdToUserId = new Dictionary<string, string>();

        public KnockbackRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner,
                                   KnockbackMatchState state, KnockbackConfig config, KnockbackTestBotFill fillTo = null,
                                   KnockbackMatchMode mode = null, KnockbackPillars pillars = null, IMatchSeed seed = null)
        {
            this.pillars = pillars;
            this.seed = seed;
            this.mode = mode ?? KnockbackMatchMode.Solo;
            this.fillTo = fillTo ?? new KnockbackTestBotFill(() => 0);
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
            this.state = state;
            this.config = config;
        }

        public void Initialize()
        {
            var playerList = roomDataStore.match.playerList;
            int fill = fillTo.Read();
            int botCount = MountedArcheryBots.CountToAdd(fill, playerList.Length);
            int total = playerList.Length + botCount;
            state.TeamCount = mode.TeamCount;
            if (fill > 0)
            {
                UnityEngine.Debug.Log($"[TestBots] fillTo={fill} humans={playerList.Length} bots={botCount}");
            }
            for (int i = 0; i < total; i++)
            {
                //  봇은 사람 다음 번호의 자리를 쓴다.
                bool isBot = i >= playerList.Length;
                int botNumber = i - playerList.Length + 1;
                string userId = isBot ? MountedArcheryBots.UserIdFor(botNumber) : playerList[i];
                string entityId = entitySpawner.GenerateEntityId();
                entityIdToUserId[entityId] = userId;
                state.Players.Add(entityId);
                if (isBot) state.Bots.Add(entityId);
                state.Team[entityId] = KnockbackTeams.TeamForSeat(i, mode.TeamCount);

                var position = KnockbackSpawn.Position(i, total, config.SpawnRadius, pillars?.RotationDegrees ?? 0f);
                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = userId,
                    entityId = entityId,
                    visualId = DodgeRuleSystem.BodyVisualId,
                    characterCode = BodyCharacterCode,
                    position = position,
                    rotation = new Vector3(0f, KnockbackSpawn.YawFacingCenter(position), 0f),
                    velocity = Vector3.zero,
                    maxHP = 100000,
                    currentHP = 100000,
                    maxMP = 1000,
                    currentMP = 1000,
                    level = 1,
                    currentExp = 0,
                    look = isBot
                        ? new PlayerLook(null, MountedArcheryBots.DisplayNameFor(botNumber), 1)
                        : PlayerLookResolver.Resolve(roomDataStore.looks, userId, i),
                });
            }
            SpawnBarrels();
            //  팀을 채운 상태를 틱 0부터 보낸다 — 판본이 0이면 첫 원 공개(8초)까지 클라가 팀을 모른다.
            state.MarkChanged();
        }

        //  드럼통 시작 자리는 씨앗으로 정해 클라가 따로 계산하지 않는다 - 엔티티로 보내 스냅샷이 따라오게 한다.
        private void SpawnBarrels()
        {
            var layout = config.Barrels;
            if (layout.Count <= 0)
            {
                return;
            }
            var centers = new List<Vector2>();
            float pillarRadius = 0f;
            if (pillars != null)
            {
                for (int i = 0; i < pillars.Count; i++)
                {
                    centers.Add(pillars.Center(i));
                }
                pillarRadius = pillars.Radius;
            }
            var spawns = KnockbackMapRandom.BarrelSpawns(seed?.Value ?? 0UL, layout, centers, pillarRadius);
            for (int i = 0; i < spawns.Length; i++)
            {
                entitySpawner.Spawn(new BarrelCreationData
                {
                    entityId = entitySpawner.GenerateEntityId(),
                    position = new Vector3(spawns[i].x, 0f, spawns[i].y),
                    rotation = Vector3.zero,
                    velocity = Vector3.zero,
                    visualId = KnockbackBarrel.VisualId,
                });
            }
        }

        public void Deinitialize() { }

        /// <summary>떨어졌으면 결과가 정해졌다 — 그 뒤 관전하다 나가도 탈락 순서 그대로.</summary>
        public bool IsResultSettled(string userId)
        {
            foreach (var pair in entityIdToUserId)
            {
                if (pair.Value == userId) return state.EliminatedTick.ContainsKey(pair.Key);
            }
            return false;
        }

        public bool IsMatchOver
        {
            get
            {
                long lastElimination = state.Eliminations.Count > 0 ? state.Eliminations[state.Eliminations.Count - 1].tick : -1;
                if (mode.IsTeam)
                {
                    //  팀전은 "한 명 남음"이 아니라 "한 팀 남음" — 탈락 때 상태가 승부를 확정해 둔다.
                    return state.TeamDecided
                        && state.LastTick - state.TeamDecidedTick >= DodgeRuleSystem.EndGraceTicks;
                }
                return DodgeRuleSystem.MatchOver(entityIdToUserId.Count, state.AliveCount(), lastElimination, state.LastTick);
            }
        }

        public long MatchDurationTicks => config.MatchDurationTicks(TickInterval);

        /// <summary>사람이 둘 이상이어야 겨룬 판이다 — 혼자 잡힌 판은 바로 이겨도 보상을 주지 않는다.</summary>
        public static bool IsPractice(int humanCount) => humanCount < 2;

        /// <summary>살아 있는 팀 전원 1위, 나머지 2위. 아무도 안 살았으면(동시 전멸) 전원 1위.</summary>
        public static MatchOutcome TeamOutcome(IReadOnlyDictionary<string, int> teamOfUser, IReadOnlyCollection<string> aliveUsers)
        {
            var aliveTeams = new HashSet<int>();
            foreach (var user in aliveUsers)
            {
                aliveTeams.Add(teamOfUser.TryGetValue(user, out var team) ? team : 0);
            }
            var outcome = new MatchOutcome();
            foreach (var pair in teamOfUser)
            {
                bool won = aliveTeams.Count == 0 || aliveTeams.Contains(pair.Value);
                outcome.placements.Add(new MatchPlacement { userId = pair.Key, placement = won ? 1 : 2 });
            }
            return outcome;
        }

        public MatchOutcome ResolveOutcome()
        {
            var alive = new List<string>();
            foreach (var id in state.Players)
            {
                if (state.EliminatedTick.ContainsKey(id) == false && entityIdToUserId.TryGetValue(id, out var user)) alive.Add(user);
            }
            var eliminations = new List<(string, long)>();
            foreach (var (entityId, tick) in state.Eliminations)
            {
                if (entityIdToUserId.TryGetValue(entityId, out var user)) eliminations.Add((user, tick));
            }
            MatchOutcome outcome;
            if (mode.IsTeam)
            {
                var teamOfUser = new Dictionary<string, int>();
                foreach (var pair in entityIdToUserId)
                {
                    teamOfUser[pair.Value] = state.Team.TryGetValue(pair.Key, out var team) ? team : 0;
                }
                //  승부가 정해진 틱의 생존자로 매긴다 — 그 뒤에 이긴 팀이 떨어져도 결과는 그대로.
                //  정해지지 않았으면(시간 끝, 두 팀 생존) 지금 살아 있는 사람으로.
                var winners = alive;
                if (state.TeamDecided)
                {
                    winners = new List<string>();
                    foreach (var id in state.TeamWinners)
                    {
                        if (entityIdToUserId.TryGetValue(id, out var user)) winners.Add(user);
                    }
                }
                outcome = TeamOutcome(teamOfUser, winners);
                outcome.denseRanking = true;
            }
            else
            {
                outcome = DodgePlacements.Resolve(alive, eliminations);
            }
            outcome.practice = IsPractice(entityIdToUserId.Count - state.Bots.Count);
            return outcome;
        }
    }
}
