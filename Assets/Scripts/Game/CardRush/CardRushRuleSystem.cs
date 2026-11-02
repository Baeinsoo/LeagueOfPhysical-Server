using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 카드 러시 룰(서버). 참가자마다 레인 출발점에 몸을 세우고, 남아 있는 사람이 모두 결승에 닿으면
    /// 여운 뒤 판을 끝낸다. 등수는 먼저 닿은 순 → 못 닿은 사람은 칸이 많은 순(같은 칸이면 먼저 도착한 순) → 나간 사람.
    /// 닿은 순서를 매 틱 모으는 일은 <see cref="FinishTrackingSystem"/>이 한다(플래피 레이스와 같은 짝).
    /// </summary>
    public class CardRushRuleSystem : IGameRuleSystem, ISettledResults
    {
        private const string BodyCharacterCode = "character_001";

        //  플래피 레이스와 같은 여운 — 마지막 완주자가 자기 도장·등수를 보고 넘어가게.
        public const long FinishGraceTicks = FlappyRaceRuleSystem.FinishGraceTicks;

        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly FinishTrackingSystem finishSystem;
        private readonly CardRushWorld world;
        private readonly CardRushConfig config;
        //  시험 봇 채우기 — 안 넘기면(단위 시험 등) 꺼진 것으로 본다(사람 수만 세운다).
        private readonly CardRushBotFill botFill;

        //  등수를 답할 때 통과 기록(entityId)을 userId로 옮기고, 못 들어온 사람의 상태를 되찾는 데 쓴다.
        private readonly Dictionary<string, string> entityIdToUserId = new Dictionary<string, string>();

        /// <summary>판 도중 나가 있는 사람 — 판이 그 사람을 기다리지 않게. 시험 등에서 없으면 아무도 안 나간 것으로 본다.</summary>
        [VContainer.Inject] public PlayerPresence Presence { get; set; }

        //  끊긴 사람뿐 아니라 한 번도 안 들어온 사람도 뺀다(최종 리뷰 I4) — 그 몸은 칸 0에 서 있어 영영 결승에 못 간다.
        //  추격자가 없는 종목이라 Flappy처럼 "잡혀서 정리됨"으로 풀리지도 않는다.
        private bool IsAway(string userId) => userId != null && Presence != null && Presence.IsAbsent(userId);

        public CardRushRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner,
                                  GameFramework.World.EntityRegistry entityRegistry,
                                  FinishTrackingSystem finishSystem,
                                  CardRushWorld world,
                                  CardRushConfig config,
                                  CardRushBotFill botFill = null)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
            this.entityRegistry = entityRegistry;
            this.finishSystem = finishSystem;
            this.world = world;
            this.config = config;
            this.botFill = botFill ?? new CardRushBotFill(() => 0, 0);
        }

        public void Initialize()
        {
            //  규칙이 없으면 월드가 아무것도 안 해 판이 조용히 2분 내내 멈춰 있다 — 여기서 크게 터뜨린다.
            RequireRules();

            finishSystem.IsAwayEntity = id => entityIdToUserId.TryGetValue(id, out var u) && IsAway(u);
            //  지난 판이 남아 있으면 이번 판 등수 꼬리에 "나간 사람"으로 둔갑해 붙는다.
            entityIdToUserId.Clear();

            //  사람 먼저, 그다음 봇 — 레인 번호가 곧 참가 순서다.
            var participants = new List<string>(roomDataStore.match.playerList);
            int humans = participants.Count;
            int botCount = botFill.BotsToAdd(humans);
            for (int n = 1; n <= botCount; n++)
            {
                participants.Add(MountedArcheryBots.UserIdFor(n));
            }
            if (participants.Count > CardRushLanes.MaxLanes)
            {
                throw new InvalidOperationException(
                    $"[CardRush] 참가자 {participants.Count}명 — 레인은 {CardRushLanes.MaxLanes}개뿐이다");
            }

            var forward = CardRushLanes.Flat(world.Rules.Forward);
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;   // 진행 방향을 본다
            for (int i = 0; i < participants.Count; i++)
            {
                string userId = participants[i];
                bool isBot = i >= humans;
                int botNumber = i - humans + 1;
                string entityId = entitySpawner.GenerateEntityId();
                entityIdToUserId[entityId] = userId;
                finishSystem.Watch(entityId);

                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = userId,
                    entityId = entityId,
                    visualId = DodgeRuleSystem.BodyVisualId,
                    characterCode = BodyCharacterCode,
                    position = world.Rules.LaneOrigin(i),
                    rotation = new Vector3(0f, yaw, 0f),
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

        //  맵 씬의 마커가 맵 로드 때 규칙을 넣는다. 비활성 마커까지 센다 — 보일 필요가 없어 꺼 둘 수 있다.
        private void RequireRules()
        {
            int markers = UnityEngine.Object.FindObjectsByType<CardRushMapRules>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            string error = null;
            bool valid = world.Rules.HasRules && world.Rules.Validate(out error);
            if (valid == false || markers != 1)
            {
                throw new InvalidOperationException(
                    $"카드 러시 맵에 CardRushMapRules가 하나 있어야 한다: 마커 {markers}개, " +
                    (world.Rules.HasRules ? $"규칙 오류 {error ?? "없음"}" : "규칙이 안 들어왔다"));
            }
        }

        public void Deinitialize()
        {
            entityIdToUserId.Clear();
            finishSystem.Reset();
            raceOverSinceTick = -1;
        }

        //  조건이 "처음" 참이 된 틱만 기억한다(래치) — 한 틱 깜빡여도 여운을 다시 세지 않는다(FlappyRaceRuleSystem과 같다).
        private long raceOverSinceTick = -1;

        /// <summary>남아 있는 사람이 모두 결승에 닿은 틱으로부터 <see cref="FinishGraceTicks"/> 뒤에 끝난다. 시간 상한은 러너가 본다.</summary>
        public bool IsMatchOver
        {
            get
            {
                if (finishSystem.AllWatchedFinished && raceOverSinceTick < 0)
                {
                    raceOverSinceTick = finishSystem.LastTick;
                }
                return raceOverSinceTick >= 0
                    && finishSystem.LastTick - raceOverSinceTick >= FinishGraceTicks;
            }
        }

        public long MatchDurationTicks => config.MatchDurationTicks;

        /// <summary>완주했으면 결과가 정해졌다 — 그 뒤 관전 화면에서 나가도 제자리.</summary>
        public bool IsResultSettled(string userId)
        {
            foreach (var pair in entityIdToUserId)
            {
                if (pair.Value == userId) return finishSystem.HasFinished(pair.Key);
            }
            return false;
        }

        public MatchOutcome ResolveOutcome()
        {
            var unfinished = new List<(string userId, float progress)>();
            var left = new List<string>();
            var finishedUsers = new HashSet<string>();
            var userIdToEntityId = new Dictionary<string, string>();

            foreach (var pair in entityIdToUserId)
            {
                userIdToEntityId[pair.Value] = pair.Key;
                if (finishSystem.HasFinished(pair.Key))
                {
                    finishedUsers.Add(pair.Value);
                    continue;
                }
                var runner = RunnerOf(pair.Key);
                if (runner != null)
                {
                    //  판 기준 틱으로 바꿔 매긴다 — 서버 절대 틱을 그대로 넣으면 오래 켠 서버에서 같은 칸 이웃 틱이 뭉개진다.
                    unfinished.Add((pair.Value, world.RankingProgress(runner.State, finishSystem.LastTick)));
                    continue;
                }
                left.Add(pair.Value);
            }

            var outcome = FinishPlacements.Resolve(finishSystem.Ordered, entityIdToUserId,
                unfinished, Array.Empty<string>(), left);
            //  완주 1/0 — 로비 퀘스트 "완주하기"가 읽는다(레이스형 공용 키).
            RaceStatKeys.AttachFinished(outcome, entityIdToUserId.Values, finishedUsers);
            CardRushStatKeys.Attach(outcome, userIdToEntityId, id => RunnerOf(id)?.State);
            return outcome;
        }

        private CardRushRunner RunnerOf(string entityId) => entityRegistry.Get(entityId)?.Get<CardRushRunner>();
    }
}
