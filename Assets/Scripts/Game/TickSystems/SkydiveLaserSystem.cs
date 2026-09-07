using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 레이저에 닿았는지 매 틱 판정하고, 닿았으면 마지막으로 지난 선반으로 되돌린다.
    ///
    /// <para><b>서버에서만 돈다.</b> 클라는 죽음을 예측하지 않는다 — 잘못 예측한 죽음은 되돌릴 때
    /// 훨씬 잔인하고, 스치는 판정에서 갈리면 그 대가가 선반 하나만큼의 위치 불일치다.
    /// (2026-07-12에 같은 이유로 클라 데미지 예측을 짓지 않기로 했다.)</para>
    /// </summary>
    public class SkydiveLaserSystem : GameFramework.Runner.ITickSystem
    {
        //  부활 지점 근처를 지나는 빔에 즉시 다시 죽는 고리를 막는다. 이 무적 시간은 레이저
        //  판정만의 것이다 — 문(SkydiveDoorSystem)과 공유하면 한쪽에서 죽었는데 다른 쪽 무적 때문에
        //  안 죽는 이상한 결합이 생긴다(SkydiveRespawn 주석 참고).
        private const float InvulnerableSeconds = 2.0f;

        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly LaserField laserField;
        private readonly SkydiveConfig config;
        private readonly IReadOnlyList<float> shelfYs;
        private readonly float spawnY;
        private readonly IReadOnlyDictionary<float, Vector3> respawnPoints;

        //  틱 N의 끝 위치 == 틱 N+1의 시작 위치. 이 캐시가 이번 틱에 지나온 경로를 만든다.
        private readonly Dictionary<string, Vector3> previousPositions = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, float> invulnerableUntil = new Dictionary<string, float>();
        private readonly Dictionary<float, int> respawnCounts = new Dictionary<float, int>();
        private readonly List<GameFramework.World.Entity> divers = new List<GameFramework.World.Entity>();
        private int cappedIterations;   // CA가 상한까지 돌아 관대하게 통과시킨 횟수

        public SkydiveLaserSystem(GameFramework.World.EntityRegistry entityRegistry,
                                  LaserField laserField,
                                  SkydiveConfig config,
                                  IReadOnlyList<float> shelfYs,
                                  float spawnY,
                                  IReadOnlyDictionary<float, Vector3> respawnPoints)
        {
            this.entityRegistry = entityRegistry;
            this.laserField = laserField;
            this.config = config;
            this.shelfYs = shelfYs;
            this.spawnY = spawnY;
            this.respawnPoints = respawnPoints;
        }

        public void Tick(long tick, float deltaTime)
        {
            CollectDivers();

            float now = tick * deltaTime;

            for (int i = 0; i < divers.Count; i++)
            {
                GameFramework.World.Entity diver = divers[i];
                Vector3 to = GameFramework.World.EntityMotionExtensions.GetPosition(diver);

                if (previousPositions.TryGetValue(diver.Id, out Vector3 from) == false)
                {
                    previousPositions[diver.Id] = to;
                    continue;   // 첫 틱은 지나온 경로가 없다
                }
                //  틱 규약(캐시를 여기서 씀): 이번 틱이 끝난 지금 위치를 다음 틱의 "시작 위치"로
                //  남긴다. 그래서 다음 틱에서 꺼내 쓰는 `from`은 "틱 tick이 시작할 때의 위치"이고
                //  `to`는 "그 틱이 끝난(=지금) 위치"다. 한편 LaserGeometry.SegmentAt(laser, t)는 t를
                //  "그 틱이 시작할 때의 자세"로 읽는다(AnyLaserHits가 tick + t로 넘긴다). 그래서 이
                //  스윕(from→to)은 SegmentAt(tick)(시작 자세) ~ SegmentAt(tick+1)(다음 틱 시작 자세 =
                //  이번 틱 끝난 자세)와 짝이 맞는다. 이 규약을 벗어나면(예: 나중에 뷰가 "지금 자세"를
                //  SegmentAt(currentTick) 대신 다른 틱 값으로 그리면) 가장 빠른 문지기 빔 끝에서 한
                //  틱(≈4.6m)만큼 어긋나 보인다.
                previousPositions[diver.Id] = to;

                if (invulnerableUntil.TryGetValue(diver.Id, out float until) && now < until)
                {
                    continue;
                }

                if (AnyLaserHits(tick, from, to))
                {
                    Respawn(diver, to.y, now);
                }
            }
        }

        private bool AnyLaserHits(long tick, Vector3 from, Vector3 to)
        {
            float radius = config.BodyRadius;
            float height = config.BodyHeight;

            //  이동이 쓰는 캡슐(KinematicMover.Cast)과 같은 규격이어야 한다 — 축을 반지름만큼
            //  안으로 당기지 않으면 부풀린 뒤 키가 height + 2·radius가 되어, 실제 몸(height)보다
            //  44% 큰 판정 캡슐이 된다. 그러면 억울한 죽음이 늘어 "애매하면 살려준다" 원칙에 어긋난다.
            var bottomFrom = new System.Numerics.Vector3(from.x, from.y + radius, from.z);
            var topFrom = new System.Numerics.Vector3(from.x, from.y + height - radius, from.z);
            var bottomTo = new System.Numerics.Vector3(to.x, to.y + radius, to.z);
            var topTo = new System.Numerics.Vector3(to.x, to.y + height - radius, to.z);

            IReadOnlyList<Laser> lasers = laserField.All;
            for (int i = 0; i < lasers.Count; i++)
            {
                bool hit = LaserSweep.Hit(lasers[i], tick, bottomFrom, topFrom, bottomTo, topTo,
                                          radius, out _, out bool exhausted);
                //  상한까지 돌면 관대하게 통과시킨다. 잦으면 레이저가 조용히 약해지므로 센다.
                if (hit == false && exhausted)
                {
                    cappedIterations++;
                    if (cappedIterations % 100 == 1)
                    {
                        Debug.LogWarning($"[Laser] CA 반복 상한 도달 누적 {cappedIterations}회 — " +
                                         "잦으면 MaxIterations를 올려야 한다");
                    }
                }
                if (hit)
                {
                    return true;
                }
            }
            return false;
        }

        private void Respawn(GameFramework.World.Entity diver, float deathY, float now)
        {
            //  respawnCounts는 선반별 부활 순번(spread용) — 레이저 자신의 상태다. SkydiveRespawn은
            //  이 값을 읽어 각도를 정하고 하나 늘려 돌려줄 뿐, 저장은 하지 않는다.
            float shelfY = SkydiveCheckpoints.LastPassedShelfY(deathY, shelfYs, spawnY);
            respawnCounts.TryGetValue(shelfY, out int order);
            SkydiveRespawn.To(diver, deathY, config, shelfYs, spawnY, respawnPoints, ref order);
            respawnCounts[shelfY] = order;

            previousPositions[diver.Id] = GameFramework.World.EntityMotionExtensions.GetPosition(diver);
            invulnerableUntil[diver.Id] = now + InvulnerableSeconds;
        }

        //  걸러내는 기준(EntityKind=Character + Simulated)은 SkydiveWorld.CollectDivers와 같아야
        //  한다 — 다른 집합을 보면 판정과 시뮬이 어긋난다. 다만 정렬은 하지 않는다: 여기는 각
        //  diver를 독립적으로 판정만 할 뿐 서로 순서에 기대는 계산이 없어 순서가 결과에 영향을
        //  주지 않는다(SkydiveWorld가 결정론을 위해 id로 정렬하는 것과는 이유가 다르다).
        private void CollectDivers()
        {
            divers.Clear();
            foreach (GameFramework.World.Entity entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }
                if (entity.Has<GameFramework.World.Simulated>() == false)
                {
                    continue;
                }
                divers.Add(entity);
            }
        }
    }
}
