using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 삼색 그랩 룰(서버). 참가자(사람 + 부족하면 봇)를 맵 자리에 세우고 판 엔진에 등록한다.
    /// 판은 엔진이 Over(라운드가 다 끝남)가 된 뒤 여운이 지나면 끝나고, 등수는 엔진의 Placements(점수 내림차순, 같은 점수는 공동)다.
    /// 점수·카드는 TriGrabMatchSystem이 매 틱 굴린다.
    /// </summary>
    public class TriGrabRuleSystem : IGameRuleSystem, ISettledResults
    {
        private const string BodyCharacterCode = "character_001";
        private const float FallbackRingRadius = 7.8f;

        /// <summary>엔진 상한(MatchCapTicks)에 카운트다운·여운을 더한 안전 상한. 러너가 이 시간이 지나면 강제로 끝낸다.</summary>
        public const long MatchDurationPaddingTicks = 500;

        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;
        private readonly LOP.TriGrab.TriGrabMatch match;
        private readonly TriGrabRunClock clock;
        private readonly TriGrabBotFill botFill;
        private readonly Dictionary<string, string> entityIdToUserId = new Dictionary<string, string>();
        private int humanCount;

        public TriGrabRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner, LOP.TriGrab.TriGrabMatch match,
                                 TriGrabRunClock clock, TriGrabBotFill botFill = null)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
            this.match = match;
            this.clock = clock;
            this.botFill = botFill ?? new TriGrabBotFill(() => 0, 0);
        }

        /// <summary>i번째가 설 자리. 맵 SpawnPoint가 있으면 그대로, 없으면 반지름 7.8 m 고리.</summary>
        public static Vector3 SpawnPositionFor(IReadOnlyList<Vector3> slots, int index, int total)
        {
            return slots.Count > 0
                ? slots[index % slots.Count]
                : KnockbackSpawn.Position(index, System.Math.Max(1, total), FallbackRingRadius);
        }

        public void Initialize()
        {
            entityIdToUserId.Clear();
            var slots = SpawnPlacement.Arrange(
                Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            if (slots.Count == 0)
            {
                Debug.LogWarning("[TriGrab] 맵에 SpawnPoint가 없다 — 고리 모양으로 세운다");
            }

            var playerList = roomDataStore.match.playerList;
            humanCount = playerList.Length;
            int botCount = botFill.BotsToAdd(humanCount);
            int total = humanCount + botCount;
            for (int i = 0; i < total; i++)
            {
                bool isBot = i >= humanCount;
                int botNumber = i - humanCount + 1;
                string userId = isBot ? MountedArcheryBots.UserIdFor(botNumber) : playerList[i];
                string entityId = entitySpawner.GenerateEntityId();
                entityIdToUserId[entityId] = userId;
                match.AddPlayer(entityId);

                var position = SpawnPositionFor(slots, i, total);
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
            match.State.Version++;   // 사람·봇이 서 있는 모습을 판이 시작하기 전부터 보낸다
        }

        public void Deinitialize() { }

        /// <summary>판이 끝난 뒤에만 결과가 정해진다 — 점수는 끝까지 바뀔 수 있다.</summary>
        public bool IsResultSettled(string userId) => false;

        /// <summary>Over가 된 지 여운(EndGraceTicks)이 지났으면 끝. 상한은 MatchDurationTicks가 맡는다.</summary>
        public bool IsMatchOver => MatchOver(clock.OverSinceTick, clock.LastTick);

        public static bool MatchOver(long overSinceTick, long now) =>
            overSinceTick >= 0 && now - overSinceTick >= LOP.TriGrab.TriGrabConfig.EndGraceTicks;

        public long MatchDurationTicks => LOP.TriGrab.TriGrabConfig.MatchCapTicks + MatchDurationPaddingTicks;

        /// <summary>사람이 둘 이상이어야 겨룬 판이다 — 혼자 봇과 한 판은 연습.</summary>
        public static bool IsPractice(int humanCount) => humanCount < 2;

        public MatchOutcome ResolveOutcome()
        {
            var outcome = new MatchOutcome();
            foreach (var (entityId, place) in match.Placements())
            {
                if (entityIdToUserId.TryGetValue(entityId, out var user))
                {
                    outcome.placements.Add(new MatchPlacement { userId = user, placement = place });
                }
            }
            outcome.practice = IsPractice(humanCount);
            return outcome;
        }
    }
}
