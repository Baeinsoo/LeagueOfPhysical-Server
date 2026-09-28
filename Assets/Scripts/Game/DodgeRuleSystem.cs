using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Dodge 룰(서버). 참가자마다 맵의 자리에 몸을 세우고, 지금은 시간 상한으로 판을 끝낸다.
    /// 목숨·탈락·등수(스펙 §1)는 슬라이스 2가 여기에 붙는다.
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

        public DodgeRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
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
                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = playerList[i],
                    entityId = entitySpawner.GenerateEntityId(),
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
        }

        public void Deinitialize() { }

        // 목숨·탈락은 슬라이스 2. 그때까지는 시간 상한으로만 끝난다.
        public bool IsMatchOver => false;

        // 50Hz × 60초.
        public long MatchDurationTicks => 3000;

        // 진짜 등수(탈락 순서의 역순)는 슬라이스 2. 그때까지는 보고 경로가 끊기지 않게 무작위로 둔다.
        public MatchOutcome ResolveOutcome()
        {
            var userIds = roomDataStore.match.playerList.ToList();
            for (int i = userIds.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (userIds[i], userIds[j]) = (userIds[j], userIds[i]);
            }

            var outcome = new MatchOutcome();
            for (int i = 0; i < userIds.Count; i++)
            {
                outcome.placements.Add(new MatchPlacement { userId = userIds[i], placement = i + 1 });
            }
            return outcome;
        }
    }
}
