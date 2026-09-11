using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Archery 룰(서버). 참가자를 사대에 세우고 60초 뒤 판을 끝낸다.
    /// <b>점수는 슬라이스 2가 여기에 붙는다</b> — 지금은 순위를 가릴 근거가 없어 전원 동순위다.
    /// </summary>
    public class ArcheryRuleSystem : IGameRuleSystem
    {
        // 맵에 사대 마커가 없을 때만 쓰는 폴백. 원 둘레에 등간격으로 세운다.
        private const float FallbackRingRadius = 12f;

        private const string BodyVisualId = "Assets/Art/Characters/Knight/Knight.prefab";

        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;
        private readonly Dictionary<string, string> entityIdToUserId = new Dictionary<string, string>();

        public ArcheryRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
        }

        public void Initialize()
        {
            entityIdToUserId.Clear();

            // 사대 위치는 맵이 정한다 — 룰이 좌표를 들고 있으면 맵을 새로 만들 때마다 룰을 고쳐야 한다.
            var slots = SpawnPlacement.Arrange(
                UnityEngine.Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            if (slots.Count == 0)
            {
                Debug.LogWarning("[Archery] 맵에 SpawnPoint가 없다 — 원 둘레에 등간격으로 세운다");
            }

            var playerList = roomDataStore.match.playerList;
            for (int i = 0; i < playerList.Length; i++)
            {
                Vector3 position = slots.Count > 0 ? slots[i % slots.Count] : RingSlot(i, playerList.Length);

                // 가운데를 바라보게 세운다 — 사대는 원의 안쪽을 본다.
                Vector3 lookAtCenter = new Vector3(0f, Mathf.Atan2(-position.x, -position.z) * Mathf.Rad2Deg, 0f);

                string entityId = entitySpawner.GenerateEntityId();
                entityIdToUserId[entityId] = playerList[i];

                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = playerList[i],
                    entityId = entityId,
                    visualId = BodyVisualId,
                    characterCode = "",
                    position = position,
                    rotation = lookAtCenter,
                    velocity = Vector3.zero,
                });
            }
        }

        private static Vector3 RingSlot(int index, int total)
        {
            float angle = (index / (float)Mathf.Max(total, 1)) * Mathf.PI * 2f;
            return new Vector3(Mathf.Sin(angle) * FallbackRingRadius, 0f, Mathf.Cos(angle) * FallbackRingRadius);
        }

        public void Deinitialize()
        {
            entityIdToUserId.Clear();
        }

        /// <summary>이 슬라이스에는 끝낼 조건이 없다 — 시간만으로 끝난다.</summary>
        public bool IsMatchOver => false;

        /// <summary>50Hz × 60초.</summary>
        public long MatchDurationTicks => 3000;

        /// <summary>
        /// 점수가 없으므로 전원 동순위다. 슬라이스 2가 점수를 진행도로 넘긴다.
        /// </summary>
        public MatchOutcome ResolveOutcome()
        {
            var unfinished = new List<(string userId, float progress)>();
            foreach (var pair in entityIdToUserId)
            {
                unfinished.Add((pair.Value, 0f));
            }

            // 첫 인자는 도착 기록 목록(FinishRecord)이다 — 문자열이 아니다. 이 게임엔 결승선이 없어 빈 목록.
            return FinishPlacements.Resolve(
                System.Array.Empty<FinishRecord>(), entityIdToUserId,
                unfinished, System.Array.Empty<string>(), new List<string>());
        }
    }
}
