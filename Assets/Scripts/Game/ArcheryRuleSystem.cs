using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Archery 룰(서버). 참가자를 사대에 세운다 — 원형 맵은 SpawnPoint 둘레에 등간격으로,
    /// 사거리 맵은 자기 레인 위에. 판 길이도 맵이 고른다(<see cref="ArcheryCourse.MatchDurationTicks"/>
    /// 로 위임) — 원형 맵은 마스터데이터에 적힌 시간, 사거리 맵은 코스(순서)가 다 지나가는 시간이다.
    /// 등수는 <see cref="ArcheryScore"/>로 가린다 — 점수가 높은 사람이 앞이고 동점은 공동 순위다.
    /// </summary>
    public class ArcheryRuleSystem : IGameRuleSystem
    {
        // 맵에 사대 마커가 없을 때만 쓰는 폴백. 원 둘레에 등간격으로 세운다.
        private const float FallbackRingRadius = 12f;

        private const string BodyVisualId = "Assets/Art/Characters/Knight/Knight.prefab";

        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ArcheryConfig config;
        private readonly ArcheryCourse course;
        private readonly Dictionary<string, string> entityIdToUserId = new Dictionary<string, string>();

        public ArcheryRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner,
                                 GameFramework.World.EntityRegistry entityRegistry,
                                 ArcheryConfig config, ArcheryCourse course)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
            this.entityRegistry = entityRegistry;
            this.config = config;
            this.course = course;
        }

        public void Initialize()
        {
            entityIdToUserId.Clear();

            var playerList = roomDataStore.match.playerList;

            //  사거리 맵·한 발 승부는 사대가 레인 위에 있다. 원형 맵은 예전처럼 SpawnPoint를 쓴다 —
            //  맵이 값으로 방식을 고르고, 룰은 맵 이름을 모른다.
            bool laned = config.CourseKind == ArcheryCourseKind.Range
                      || config.CourseKind == ArcheryCourseKind.ShootOff;
            var lanes = laned ? ArcheryRangeLayout.FromOpenScenes() : null;

            if (lanes != null)
            {
                string problem = ArcheryRangeValidation.Check(lanes, config.Range, playerList.Length,
                                                              config.CourseKind);
                if (problem != null)
                {
                    //  조용히 이상한 판을 시작하느니 여기서 끊는다 — 원인이 바로 보인다.
                    throw new System.InvalidOperationException("[Archery] " + problem);
                }
            }

            // 사대 위치는 맵이 정한다 — 룰이 좌표를 들고 있으면 맵을 새로 만들 때마다 룰을 고쳐야 한다.
            var slots = lanes == null
                ? SpawnPlacement.Arrange(UnityEngine.Object.FindObjectsByType<SpawnPoint>(
                      FindObjectsInactive.Include, FindObjectsSortMode.None))
                : null;
            if (slots != null && slots.Count == 0)
            {
                Debug.LogWarning("[Archery] 맵에 SpawnPoint가 없다 — 원 둘레에 등간격으로 세운다");
            }

            for (int i = 0; i < playerList.Length; i++)
            {
                Vector3 position;
                Vector3 rotation;

                if (lanes != null)
                {
                    //  한 발 승부는 모두 레인 0 사대에 선다 — 누구 화면에서나 과녁이 똑같이 보이게.
                    var lane = lanes.Lanes[config.CourseKind == ArcheryCourseKind.ShootOff ? 0 : i];
                    position = lane.ShooterPosition;
                    rotation = new Vector3(0f, Mathf.Atan2(lane.Forward.x, lane.Forward.z) * Mathf.Rad2Deg, 0f);
                }
                else
                {
                    position = slots.Count > 0 ? slots[i % slots.Count] : RingSlot(i, playerList.Length);
                    // 가운데를 바라보게 세운다 — 사대는 원의 안쪽을 본다.
                    rotation = new Vector3(0f, Mathf.Atan2(-position.x, -position.z) * Mathf.Rad2Deg, 0f);
                }

                string entityId = entitySpawner.GenerateEntityId();
                entityIdToUserId[entityId] = playerList[i];

                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = playerList[i],
                    entityId = entityId,
                    visualId = BodyVisualId,
                    characterCode = "",
                    position = position,
                    rotation = rotation,
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

        /// <summary>
        /// 이 판의 길이. <b>맵이 정한다</b> — 원형 맵은 데이터에 적힌 60초(3000틱),
        /// 사거리 맵은 순서가 다 지나가는 데 걸리는 시간이다(화살이 남아도 거기서 끝난다).
        /// </summary>
        public long MatchDurationTicks => course.MatchDurationTicks;

        /// <summary>점수가 높은 사람이 앞이다. 동점은 공동 순위.</summary>
        public MatchOutcome ResolveOutcome()
        {
            var scores = new List<(string userId, int score, Dictionary<string, int> stats)>();
            foreach (var pair in entityIdToUserId)
            {
                //  판이 끝나기 전에 몸이 사라진 사람(나간 사람)은 전부 0으로 본다.
                var archeryScore = entityRegistry.Get(pair.Key)?.Get<ArcheryScore>();
                int gained = archeryScore?.Gained ?? 0;
                int lost = archeryScore?.Lost ?? 0;
                int value = archeryScore?.Value ?? 0;

                var stats = new Dictionary<string, int>
                {
                    [ArcheryStatKeys.Score] = value,
                    [ArcheryStatKeys.Gained] = gained,
                    [ArcheryStatKeys.Lost] = lost,
                };

                scores.Add((pair.Value, value, stats));
            }
            return ScorePlacements.Resolve(scores);
        }
    }
}
