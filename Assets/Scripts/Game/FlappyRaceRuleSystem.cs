using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Flappy Race 룰(서버). 참가자마다 새를 세우고, 전원이 결승선을 넘으면 판을 끝내고 등수를 답한다.
    /// 누가 먼저 닿았는지 세는 일은 <see cref="FinishLineTrackingSystem"/>이 매 틱 한다 — 룰에는
    /// 틱이 없어서 나눠 두었다(판치기의 룰/턴 짝과 같은 구조).
    /// </summary>
    public class FlappyRaceRuleSystem : IGameRuleSystem
    {
        // 맵에 스폰 마커가 없을 때만 쓰는 폴백 간격. 같은 자리에 겹쳐 세우면 누가 누군지 안 보인다.
        private const float SpawnSpacingY = 2f;
        private const string BirdVisualId = "Assets/Art/Characters/FlappyBird/Bird.prefab";

        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        private readonly FinishLineTrackingSystem finishSystem;

        //  등수를 답할 때 통과 기록(entityId)을 userId로 옮기고, 못 들어온 사람의 몸을 되찾는 데 쓴다.
        private readonly Dictionary<string, string> entityIdToUserId = new Dictionary<string, string>();

        public FlappyRaceRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner,
                                    GameFramework.World.EntityRegistry entityRegistry,
                                    FinishLineTrackingSystem finishSystem)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
            this.entityRegistry = entityRegistry;
            this.finishSystem = finishSystem;
        }

        public void Initialize()
        {
            //  마커가 없으면 여기서 크게 터뜨린다 — 결승선을 짐작해 세우면 판이 엉뚱한 데서 끝나거나
            //  영영 안 끝나는데, 둘 다 조용히 굴러가 원인을 찾기 어렵다. 실제 판정은 추적 시스템이
            //  같은 마커를 다시 찾아서 한다(형상 기준이라 좌표가 아니라 바운드가 필요해서다).
            RequireFinishLineMarker();

            //  지난 판이 남아 있으면 이번 판 등수 꼬리에 "나간 사람"으로 둔갑해 붙는다.
            entityIdToUserId.Clear();

            //  시작 지점은 맵이 정한다 — 룰이 좌표를 들고 있으면 맵을 새로 만들 때마다 룰을 고쳐야 한다.
            //  비활성 마커까지 찾는다: 마커는 보일 필요가 없어 꺼 둘 수도 있다.
            var slots = SpawnPlacement.Arrange(
                UnityEngine.Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None));
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

                string entityId = entitySpawner.GenerateEntityId();
                entityIdToUserId[entityId] = playerList[i];
                finishSystem.Watch(entityId);

                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = playerList[i],
                    entityId = entityId,
                    visualId = BirdVisualId,
                    characterCode = "",
                    position = position,
                    rotation = Vector3.zero,
                    velocity = Vector3.zero,
                });
            }
        }

        //  결승선은 맵이 정한다(스폰 지점과 같은 이유). 비활성 마커까지 찾는다 — 보일 필요가 없어
        //  꺼 둘 수 있다.
        private static void RequireFinishLineMarker()
        {
            var markers = UnityEngine.Object.FindObjectsByType<FinishLine>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (markers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"[FlappyRace] 맵에 FinishLine 마커가 정확히 하나 있어야 한다 (발견: {markers.Length}개).");
            }
        }

        public void Deinitialize()
        {
            entityIdToUserId.Clear();
            finishSystem.Reset();
        }

        /// <summary>남아 있는 새가 전원 결승선에 닿으면 끝난다. 시간 상한은 러너가 따로 본다.</summary>
        public bool IsMatchOver => finishSystem.AllWatchedFinished;

        //  50Hz × 90초. 전원이 결승선을 넘으면 그 전에 끝나고, 이건 아무도 못 들어왔을 때의 상한이다.
        //  코스 640m를 11m/s로 달리면 57.5초라, 스턴(0.8초)을 40번 먹어도 완주할 여유가 있다.
        public long MatchDurationTicks => 4500;

        /// <summary>
        /// 먼저 닿은 순서로 등수를 매긴다. 규칙 자체는 <see cref="FinishPlacements"/>에 있고 여기는
        /// 이 게임의 진행도(달린 거리 = x)를 넘길 뿐이다.
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
                    left.Add(pair.Value);   // 나간 사람의 새는 이미 없다
                }
                else
                {
                    unfinished.Add((pair.Value, body.Position.X));   // +x로 달리므로 클수록 앞선다
                }
            }

            return FinishPlacements.Resolve(finishSystem.Ordered, entityIdToUserId, unfinished, left);
        }
    }
}
