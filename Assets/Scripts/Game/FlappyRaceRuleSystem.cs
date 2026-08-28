using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Flappy Race 룰(서버). 참가자마다 새를 세우고, 전원이 결승선을 넘으면 판을 끝낸다.
    /// 판정 자체는 공용 순수 C#(<see cref="FlappyRaceProgress"/>)에 있고 여기는 배선만 한다.
    /// 등수(통과 순서)는 아직 없다 — 별도 슬라이스.
    /// </summary>
    public class FlappyRaceRuleSystem : IGameRuleSystem
    {
        // 맵에 스폰 마커가 없을 때만 쓰는 폴백 간격. 같은 자리에 겹쳐 세우면 누가 누군지 안 보인다.
        private const float SpawnSpacingY = 2f;
        private const string BirdVisualId = "Assets/Art/Characters/FlappyBird/Bird.prefab";

        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        //  이 판에 세운 새들. 종료 판정 때 이 id로 레지스트리를 되짚는다.
        private readonly List<string> birdEntityIds = new List<string>();
        //  판정 때마다 새로 만들지 않으려고 재사용한다(매 틱 불린다).
        private readonly List<float> finishScratch = new List<float>();

        private FlappyRaceProgress progress;

        public FlappyRaceRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner,
                                    GameFramework.World.EntityRegistry entityRegistry)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
            this.entityRegistry = entityRegistry;
        }

        public void Initialize()
        {
            progress = new FlappyRaceProgress(FindFinishX());

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
                birdEntityIds.Add(entityId);

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
        private static float FindFinishX()
        {
            var markers = UnityEngine.Object.FindObjectsByType<FinishLine>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            //  스폰 지점과 달리 폴백을 두지 않는다. 결승선을 짐작해 세우면 판이 엉뚱한 데서
            //  끝나거나 영영 안 끝나는데, 둘 다 조용히 굴러가 원인을 찾기 어렵다.
            if (markers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"[FlappyRace] 맵에 FinishLine 마커가 정확히 하나 있어야 한다 (발견: {markers.Length}개).");
            }

            return markers[0].transform.position.x;
        }

        public void Deinitialize()
        {
            birdEntityIds.Clear();
        }

        /// <summary>남아 있는 새가 전원 결승선을 넘으면 끝난다. 시간 상한은 러너가 따로 본다.</summary>
        public bool IsMatchOver
        {
            get
            {
                finishScratch.Clear();
                foreach (string entityId in birdEntityIds)
                {
                    //  나간 사람의 새는 이미 없다 — 남은 사람만 보고 판단한다. 안 그러면
                    //  한 명이 나간 판은 시간 상한까지 절대 안 끝난다.
                    var entity = entityRegistry.Get(entityId);
                    if (entity == null)
                    {
                        continue;
                    }
                    finishScratch.Add(entity.Get<GameFramework.World.Transform>().Position.X);
                }
                return progress.AllFinished(finishScratch);
            }
        }

        //  50Hz × 90초. 전원이 결승선을 넘으면 그 전에 끝나고, 이건 아무도 못 들어왔을 때의 상한이다.
        //  코스 640m를 11m/s로 달리면 57.5초라, 스턴(0.8초)을 40번 먹어도 완주할 여유가 있다.
        public long MatchDurationTicks => 4500;

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
