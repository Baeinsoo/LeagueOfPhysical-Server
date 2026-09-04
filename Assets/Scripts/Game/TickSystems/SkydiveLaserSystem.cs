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
        //  부활 지점 근처를 지나는 빔에 즉시 다시 죽는 고리를 막는다.
        private const float InvulnerableSeconds = 2.0f;

        //  같은 자리에 여러 명이 부활하면 서로 밀어낸다(캐릭터끼리는 단단한 벽이다).
        private const float RespawnSpreadRadius = 2f;
        private const int RespawnSpreadCount = 6;

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

            var bottomFrom = new System.Numerics.Vector3(from.x, from.y, from.z);
            var topFrom = new System.Numerics.Vector3(from.x, from.y + height, from.z);
            var bottomTo = new System.Numerics.Vector3(to.x, to.y, to.z);
            var topTo = new System.Numerics.Vector3(to.x, to.y + height, to.z);

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
            float shelfY = SkydiveCheckpoints.LastPassedShelfY(deathY, shelfYs, spawnY);

            Vector3 basePoint = respawnPoints.TryGetValue(shelfY, out Vector3 point)
                ? point
                : new Vector3(0f, shelfY, 0f);

            respawnCounts.TryGetValue(shelfY, out int order);
            respawnCounts[shelfY] = order + 1;
            float angle = order % RespawnSpreadCount * (2f * Mathf.PI / RespawnSpreadCount);
            var spread = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * RespawnSpreadRadius;

            GameFramework.World.EntityMotionExtensions.Teleport(diver, basePoint + spread);
            GameFramework.World.EntityMotionExtensions.SetVelocity(diver, Vector3.zero);
            previousPositions[diver.Id] = basePoint + spread;

            var stamina = diver.Get<Stamina>();
            if (stamina != null)
            {
                stamina.Current = config.StaminaMax;
                stamina.EmergencyUsed = false;
                stamina.EmergencyRemaining = 0f;
            }

            //  펴진 채로 부활하면 조작이 끊긴 것처럼 보인다. 대자(Axis 0)로 되돌린다.
            var posture = diver.Get<Posture>();
            if (posture != null)
            {
                posture.Gliding = false;
                posture.Axis = 0f;
            }

            invulnerableUntil[diver.Id] = now + InvulnerableSeconds;

            Debug.Log($"[Laser] {diver.Id} 부활 — 죽은 고도 {deathY:F0} → 선반 {shelfY:F0}");
        }

        //  SkydiveWorld.CollectDivers와 같은 기준이어야 한다 — 다른 집합을 보면 판정과 시뮬이 어긋난다.
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
