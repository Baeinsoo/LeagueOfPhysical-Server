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

        // 플랩왕의 기사 몸을 빌려 쓴다. 이 모드는 이동만 하므로 능력치·스킬은 쓰이지 않는다.
        private const string BodyVisualId = "Assets/Art/Characters/Knight/Knight.prefab";
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
            state.MarkChanged();
        }

        public void Deinitialize() { }

        // 혼자 들어온 판은 시작하자마자 끝나지 않게, 두 명 이상일 때만 "한 명 남음"으로 끝낸다.
        public bool IsMatchOver => entityIdToUserId.Count >= 2 && state.AliveCount <= 1;

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
