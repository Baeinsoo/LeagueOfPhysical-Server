using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 세게 착지한 사람을 마지막으로 지난 선반으로 되돌린다.
    ///
    /// <para>충격 속도는 <see cref="LandingImpact"/>가 이미 들고 있다 — 이동이 끝나면 그 값을
    /// 다시 구할 수 없어서 공유 이동 단계가 남겨 둔 것이다(<see cref="SkydiveDoorSystem"/>은
    /// 틱과 위치로 다시 계산할 수 있어 나를 것이 없었다).</para>
    /// </summary>
    public class SkydiveLandingSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly SkydiveConfig config;
        private readonly IReadOnlyList<float> shelfYs;
        private readonly float spawnY;
        private readonly IReadOnlyDictionary<float, Vector3> respawnPoints;

        //  선반별 부활 순번 — 레이저·문과 공유하지 않는다(SkydiveRespawn 주석 참고).
        private readonly Dictionary<float, int> respawnCounts = new Dictionary<float, int>();
        private readonly List<GameFramework.World.Entity> divers = new List<GameFramework.World.Entity>();

        public SkydiveLandingSystem(GameFramework.World.EntityRegistry entityRegistry,
                                    SkydiveConfig config,
                                    IReadOnlyList<float> shelfYs,
                                    float spawnY,
                                    IReadOnlyDictionary<float, Vector3> respawnPoints)
        {
            this.entityRegistry = entityRegistry;
            this.config = config;
            this.shelfYs = shelfYs;
            this.spawnY = spawnY;
            this.respawnPoints = respawnPoints;
        }

        public void Tick(long tick, float deltaTime)
        {
            CollectDivers();
            for (int i = 0; i < divers.Count; i++)
            {
                GameFramework.World.Entity diver = divers[i];
                if (SkydiveLanding.IsLethal(diver, config) == false)
                {
                    continue;
                }
                Respawn(diver, GameFramework.World.EntityMotionExtensions.GetPosition(diver).y);
            }
        }

        private void Respawn(GameFramework.World.Entity diver, float deathY)
        {
            float shelfY = SkydiveCheckpoints.LastPassedShelfY(deathY, shelfYs, spawnY);
            respawnCounts.TryGetValue(shelfY, out int order);
            SkydiveRespawn.To(diver, deathY, config, shelfYs, spawnY, respawnPoints, ref order);
            respawnCounts[shelfY] = order;
        }

        //  걸러내는 기준은 SkydiveDoorSystem.CollectDivers와 같아야 한다 — 판정 대상 집합이
        //  어긋나면 안 된다.
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
