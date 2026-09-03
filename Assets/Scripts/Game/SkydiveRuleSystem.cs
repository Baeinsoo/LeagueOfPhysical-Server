using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Skydive 룰(서버). 참가자마다 몸을 하늘에 세우고, 전원이 바닥에 닿으면 판을 끝내며 도착 순서로
    /// 등수를 매긴다. 죽음 처리는 슬라이스 4가 여기에 붙는다.
    /// </summary>
    public class SkydiveRuleSystem : IGameRuleSystem
    {
        // 맵에 스폰 마커가 없을 때만 쓰는 폴백. 같은 자리에 겹쳐 세우면 누가 누군지 안 보인다.
        private const float FallbackSpawnY = 200f;
        private const float FallbackSpawnSpacingX = 3f;

        // Bird.prefab은 Animator가 없어 어떤 자세도 못 취한다. Knight는 리그가 있는 사람 몸이라
        // 지금은 기울기만 쓰지만, 나중에 진짜 스카이다이빙 클립(다이브/대자/패러세일)을 얹을 자리가 된다.
        private const string BodyVisualId = "Assets/Art/Characters/Knight/Knight.prefab";

        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly FinishLineTrackingSystem finishSystem;

        // 도착 감시(entityId 기준)를 등수(userId)로 옮기려면 이 대응표가 있어야 한다.
        // 남아 있는 사람의 몸 위치를 다시 찾을 때(ResolveOutcome 2단계)도 이걸로 entityId를 얻는다.
        private readonly Dictionary<string, string> entityIdToUserId = new Dictionary<string, string>();

        public SkydiveRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner,
                                  GameFramework.World.EntityRegistry entityRegistry, FinishLineTrackingSystem finishSystem)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
            this.entityRegistry = entityRegistry;
            this.finishSystem = finishSystem;
        }

        public void Initialize()
        {
            // 정리는 Deinitialize가 하지만, 그게 안 불린 채 다시 시작하는 경로가 생기면 지난 판의
            // 엔티티 id가 남아 결과 꼬리에 "나간 사람"으로 둔갑해 붙는다. 시작할 때도 비워 둔다.
            entityIdToUserId.Clear();

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
                entityIdToUserId[entityId] = playerList[i];
                finishSystem.Watch(entityId);

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
            entityIdToUserId.Clear();
            finishSystem.Reset();  // 다음 판이 이번 판의 도착 순서를 물려받으면 등수가 처음부터 틀어진다
        }

        /// <summary>남아 있는 사람이 전원 바닥에 닿으면 끝난다. 시간 상한은 러너가 따로 본다.</summary>
        public bool IsMatchOver => finishSystem.AllWatchedFinished;

        // 50Hz × 300초. 대자(60m/s)로 3000m를 51초, 다이브(90m/s)로 35초에 내려온다.
        // 그런데 패러세일은 6m/s라, 스태미나를 발판에서 채워 가며 타면 훨씬 오래 걸린다 —
        // 지금은 그 조작을 시험하는 중이라 넉넉하게 둔다. 재미가 잡히면 다시 조인다.
        public long MatchDurationTicks => 15000;

        /// <summary>
        /// 먼저 닿은 순서로 등수를 매긴다. 규칙 자체는 <see cref="FinishPlacements"/>에 있고 여기는
        /// 이 게임의 진행도를 넘길 뿐이다 — 아래로 내려갈수록 앞선 것이라 부호를 뒤집어 준다.
        /// </summary>
        public MatchOutcome ResolveOutcome()
        {
            var unfinished = new List<(string userId, float progress)>();
            var left = new List<string>();

            foreach (var pair in entityIdToUserId)
            {
                if (finishSystem.HasFinished(pair.Key))
                {
                    continue;
                }
                var body = entityRegistry.Get(pair.Key)?.Get<GameFramework.World.Transform>();
                if (body == null)
                {
                    left.Add(pair.Value);
                }
                else
                {
                    unfinished.Add((pair.Value, -body.Position.Y));   // 낮을수록 앞섰다
                }
            }

            //  스카이다이브에는 탈락이 없다.
            return FinishPlacements.Resolve(finishSystem.Ordered, entityIdToUserId,
                unfinished, System.Array.Empty<string>(), left);
        }
    }
}
