using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>둘레 위 자리와 그 자리에서 가운데를 보는 각.</summary>
    public static class KnockbackSpawn
    {
        public static Vector3 Position(int index, int count, float radius)
        {
            float theta = index / (float)count * 2f * Mathf.PI;
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
        private readonly Dictionary<string, string> entityIdToUserId = new Dictionary<string, string>();

        public KnockbackRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner,
                                   KnockbackMatchState state, KnockbackConfig config, KnockbackTestBotFill fillTo = null)
        {
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

                var position = KnockbackSpawn.Position(i, total, config.SpawnRadius);
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

        public bool IsMatchOver => DodgeRuleSystem.MatchOver(entityIdToUserId.Count, state.AliveCount(),
            state.Eliminations.Count > 0 ? state.Eliminations[state.Eliminations.Count - 1].tick : -1, state.LastTick);

        public long MatchDurationTicks => config.MatchDurationTicks(TickInterval);

        /// <summary>사람이 둘 이상이어야 겨룬 판이다 — 혼자 잡힌 판은 바로 이겨도 보상을 주지 않는다.</summary>
        public static bool IsPractice(int humanCount) => humanCount < 2;

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
            var outcome = DodgePlacements.Resolve(alive, eliminations);
            outcome.practice = IsPractice(entityIdToUserId.Count - state.Bots.Count);
            return outcome;
        }
    }
}
