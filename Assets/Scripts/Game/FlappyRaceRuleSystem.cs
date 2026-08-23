using System.Linq;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Flappy Race 룰(서버). 지금은 매치 시작 시 참가자마다 새를 하나씩 세우는 일만 한다 —
    /// 결승선·순위·종료 판정은 다음 슬라이스에서 여기에 들어온다.
    /// </summary>
    public class FlappyRaceRuleSystem : IGameRuleSystem
    {
        // 맵에 스폰 마커가 없을 때만 쓰는 폴백 간격. 같은 자리에 겹쳐 세우면 누가 누군지 안 보인다.
        private const float SpawnSpacingY = 2f;
        private const string BirdVisualId = "Assets/Art/Characters/FlappyBird/Bird.prefab";

        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;

        public FlappyRaceRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
        }

        public void Initialize()
        {
            //  시작 지점은 맵이 정한다 — 룰이 좌표를 들고 있으면 맵을 새로 만들 때마다 룰을 고쳐야 한다.
            //  비활성 마커까지 찾는다: 마커는 보일 필요가 없어 꺼 둘 수도 있다.
            var slots = SpawnPlacement.Arrange(
                Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            if (slots.Count == 0)
            {
                Debug.LogWarning("[FlappyRace] 맵에 SpawnPoint가 없다 — 원점에 세로로 세운다");
            }

            var playerList = roomDataStore.match.playerList;
            for (int i = 0; i < playerList.Length; i++)
            {
                //  자리가 사람보다 적으면 앞에서부터 다시 쓴다. 겹쳐 서긴 해도 아무도 맵 밖에 나지 않는다.
                Vector3 position = slots.Count > 0
                    ? slots[i % slots.Count]
                    : new Vector3(0f, i * SpawnSpacingY, 0f);

                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = playerList[i],
                    entityId = entitySpawner.GenerateEntityId(),
                    visualId = BirdVisualId,
                    characterCode = "",
                    position = position,
                    rotation = Vector3.zero,
                    velocity = Vector3.zero,
                });
            }
        }

        public void Deinitialize()
        {
        }

        //  진짜 등수(결승선 통과 순서)는 게임플레이가 붙는 슬라이스에서 채운다. 그때까지는
        //  보고 경로가 끊기지 않도록 무작위로 둔다.
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
