using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Skydive 룰(서버). 참가자마다 몸을 하늘에 세우고, 시간 상한으로 판을 끝낸다.
    /// 결승선 판정(등수)은 슬라이스 3, 죽음 처리는 슬라이스 4가 여기에 붙는다.
    /// </summary>
    public class SkydiveRuleSystem : IGameRuleSystem
    {
        // 맵에 스폰 마커가 없을 때만 쓰는 폴백. 같은 자리에 겹쳐 세우면 누가 누군지 안 보인다.
        private const float FallbackSpawnY = 200f;
        private const float FallbackSpawnSpacingX = 3f;

        // 겉모습은 Flappy의 새를 빌려 쓴다 — 슬라이스 1에서 확인할 것은 "떨어지는가"뿐이고,
        // 전용 모델을 기다리면 그 확인이 막힌다. 자세(다이브/대자/패러세일)가 생기는 슬라이스 2에서
        // 자세별 애니메이션이 있는 몸으로 바꾼다.
        private const string BodyVisualId = "Assets/Art/Characters/FlappyBird/Bird.prefab";

        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;

        private readonly List<string> bodyEntityIds = new List<string>();

        public SkydiveRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
        }

        public void Initialize()
        {
            // 시작 지점은 맵이 정한다 — 룰이 좌표를 들고 있으면 맵을 새로 만들 때마다 룰을 고쳐야 한다.
            // 비활성 마커까지 찾는다: 마커는 보일 필요가 없어 꺼 둘 수도 있다.
            var slots = SpawnPlacement.Arrange(
                UnityEngine.Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            if (slots.Count == 0)
            {
                Debug.LogWarning("[Skydive] 맵에 SpawnPoint가 없다 — 하늘에 가로로 세운다");
            }

            var playerList = roomDataStore.match.playerList;
            for (int i = 0; i < playerList.Length; i++)
            {
                Vector3 position = slots.Count > 0
                    ? slots[i % slots.Count]
                    : new Vector3(i * FallbackSpawnSpacingX, FallbackSpawnY, 0f);

                string entityId = entitySpawner.GenerateEntityId();
                bodyEntityIds.Add(entityId);

                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = playerList[i],
                    entityId = entityId,
                    visualId = BodyVisualId,
                    characterCode = "",
                    position = position,
                    rotation = Vector3.zero,
                    velocity = Vector3.zero,
                });
            }
        }

        public void Deinitialize()
        {
            bodyEntityIds.Clear();
        }

        // 결승선이 아직 없다(슬라이스 3). 그때까지는 시간 상한만으로 끝난다.
        public bool IsMatchOver => false;

        // 50Hz × 60초. 200m를 40m/s 상한으로 떨어지면 10초 남짓이라 넉넉한 상한이다.
        public long MatchDurationTicks => 3000;

        // 진짜 등수(결승선 통과 순서)는 슬라이스 3에서 채운다. 그때까지는 보고 경로가 끊기지
        // 않도록 무작위로 둔다.
        public MatchOutcome ResolveOutcome()
        {
            var userIds = roomDataStore.match.playerList.ToList();

            for (int i = userIds.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
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
