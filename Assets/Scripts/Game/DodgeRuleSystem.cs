using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Dodge 룰(서버). 참가자마다 맵의 자리에 몸을 세우고 목숨을 준다. 산 사람이 한 명 이하가 되면 끝나고,
    /// 등수는 탈락 순서의 역순이다(스펙 §1). 맞음·탈락 자체는 DodgeHazardSystem이 정한다.
    /// </summary>
    public class DodgeRuleSystem : IGameRuleSystem
    {
        // 맵에 SpawnPoint가 없을 때만 쓰는 폴백 간격(m). 겹쳐 세우면 누가 누군지 안 보인다.
        private const float FallbackSpacingX = 2f;

        // 한 발 승부 치비 — 새 룩. 클라가 같은 키로 원격 그룹에서 받는다. 이동만 하므로 능력치·스킬은 기사 캐릭터 코드를 그대로 빌린다.
        public const string BodyVisualId = "Assets/Characters/Chibi/Chibi.prefab";
        private const string BodyCharacterCode = "character_001";

        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;
        private readonly DodgeMatchState state;
        private readonly DodgeConfig config;
        private readonly Dictionary<string, string> entityIdToUserId = new Dictionary<string, string>();

        public DodgeRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner,
                               DodgeMatchState state, DodgeConfig config)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
            this.state = state;
            this.config = config;
        }

        /// <summary>i번째 사람이 설 자리. 자리가 모자라면 돌려 쓰고, 없으면 바닥에 가로로 줄세운다.</summary>
        public static Vector3 SpawnPositionFor(IReadOnlyList<Vector3> slots, int index)
        {
            return slots.Count > 0
                ? slots[index % slots.Count]
                : new Vector3(index * FallbackSpacingX, 0f, 0f);
        }

        public void Initialize()
        {
            // 자리는 맵이 정한다. 비활성 마커까지 찾는다 — 보일 필요가 없어 꺼 둘 수 있다.
            var slots = SpawnPlacement.Arrange(
                Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            if (slots.Count == 0)
            {
                Debug.LogWarning("[Dodge] 맵에 SpawnPoint가 없다 — 바닥에 가로로 세운다");
            }

            var playerList = roomDataStore.match.playerList;
            for (int i = 0; i < playerList.Length; i++)
            {
                string entityId = entitySpawner.GenerateEntityId();
                entityIdToUserId[entityId] = playerList[i];
                state.Players[entityId] = new DodgePlayerLife { Lives = config.Lives };

                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = playerList[i],
                    entityId = entityId,
                    visualId = BodyVisualId,
                    characterCode = BodyCharacterCode,
                    position = SpawnPositionFor(slots, i),
                    rotation = Vector3.zero,
                    velocity = Vector3.zero,
                    maxHP = 100000,
                    currentHP = 100000,
                    maxMP = 1000,
                    currentMP = 1000,
                    level = 1,
                    currentExp = 0,
                });
            }
            SpawnReferee();
            state.MarkChanged();
        }

        // 탄막 투척 심판 — 선수가 아닌 캐릭터. 관중석 너머(Gate)에서 기다리다 탄막 스테이지에 들어온다(DodgeRefereeSystem).
        // 월드 시뮬을 빼야 벽·중력에 안 걸리고 식대로 선다. 목숨·판정·등수에는 안 든다(state.Players 밖).
        private void SpawnReferee()
        {
            string id = entitySpawner.GenerateEntityId();
            entitySpawner.Spawn(new CharacterCreationData
            {
                userId = null,
                entityId = id,
                visualId = BodyVisualId,
                characterCode = BodyCharacterCode,
                position = new Vector3(DodgeReferee.Gate.x, 0f, DodgeReferee.Gate.y),
                rotation = new Vector3(0f, 180f, 0f),
                velocity = Vector3.zero,
                maxHP = 100000,
                currentHP = 100000,
                maxMP = 1000,
                currentMP = 1000,
                level = 1,
                currentExp = 0,
                scriptedMotion = true,
            });
            state.RefereeId = id;
        }

        public void Deinitialize() { }

        public bool IsMatchOver => MatchOver(entityIdToUserId.Count, state.AliveCount,
            state.Eliminations.Count > 0 ? state.Eliminations[state.Eliminations.Count - 1].tick : -1, state.LastTick);

        /// <summary>마지막 탈락 뒤 결과로 넘어가기까지(3초, 50Hz). 클라의 탈락 자막(2.5초)·들것(2초)이 다 보이게.</summary>
        public const int EndGraceTicks = 150;

        // 둘 이상이면 "한 명 남음", 혼자면(최소 인원 1 — 혼자 연습) "탈락"으로 갈린다. 갈린 뒤 여운이 지나면 끝.
        public static bool MatchOver(int players, int alive, long lastEliminationTick, long now) =>
            players >= 1 && alive <= (players >= 2 ? 1 : 0) && now - lastEliminationTick >= EndGraceTicks;

        // 50Hz × 5분. 슬라이스 3의 서든데스가 판을 끝내기 전까지의 안전 상한.
        public long MatchDurationTicks => 15000;

        public MatchOutcome ResolveOutcome()
        {
            var alive = new List<string>();
            foreach (var kv in state.Players)
            {
                if (kv.Value.Alive && entityIdToUserId.TryGetValue(kv.Key, out var user)) alive.Add(user);
            }
            var eliminations = new List<(string, long)>();
            foreach (var (entityId, tick) in state.Eliminations)
            {
                if (entityIdToUserId.TryGetValue(entityId, out var user)) eliminations.Add((user, tick));
            }
            return DodgePlacements.Resolve(alive, eliminations);
        }
    }
}
