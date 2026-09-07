using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 닫힌 문에 끼었는지 매 틱 판정하고, 끼었으면 마지막으로 지난 선반으로 되돌린다.
    ///
    /// <para><see cref="SkydiveLaserSystem"/>과 같은 모양이지만 스윕이 필요 없다 — 문은 안 움직이는
    /// 판정(위치 겹침)만 보면 된다. 레이저는 빔이 지나가는 순간을 놓치지 않으려 이전 틱 위치까지
    /// 스윕하지만, 문은 매 틱 "지금 겹치는가"만 물어도 충분하다(닫힌 문은 몇 틱 동안 계속 닫혀
    /// 있어서 한 틱을 놓칠 일이 없다).</para>
    /// </summary>
    public class SkydiveDoorSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly DoorField doorField;
        private readonly SkydiveConfig config;
        private readonly IReadOnlyList<float> shelfYs;
        private readonly float spawnY;
        private readonly IReadOnlyDictionary<float, Vector3> respawnPoints;

        //  선반별 부활 순번(spread용) — 문 판정만의 상태다. 레이저의 respawnCounts와 공유하지
        //  않는다(SkydiveRespawn 주석 참고 — 공유하면 서로 다른 위험이 한 카운터로 뒤섞인다).
        private readonly Dictionary<float, int> respawnCounts = new Dictionary<float, int>();
        private readonly List<GameFramework.World.Entity> divers = new List<GameFramework.World.Entity>();

        public SkydiveDoorSystem(GameFramework.World.EntityRegistry entityRegistry,
                                 DoorField doorField,
                                 SkydiveConfig config,
                                 IReadOnlyList<float> shelfYs,
                                 float spawnY,
                                 IReadOnlyDictionary<float, Vector3> respawnPoints)
        {
            this.entityRegistry = entityRegistry;
            this.doorField = doorField;
            this.config = config;
            this.shelfYs = shelfYs;
            this.spawnY = spawnY;
            this.respawnPoints = respawnPoints;
        }

        public void Tick(long tick, float deltaTime)
        {
            CollectDivers();
            IReadOnlyList<DoorVolume> doors = doorField.All;
            for (int i = 0; i < divers.Count; i++)
            {
                GameFramework.World.Entity diver = divers[i];
                Vector3 p = GameFramework.World.EntityMotionExtensions.GetPosition(diver);
                //  이동이 쓰는 캡슐과 같은 규격이어야 한다 — 축을 반지름만큼 안으로 당긴다.
                var bottom = new System.Numerics.Vector3(p.x, p.y + config.BodyRadius, p.z);
                var top = new System.Numerics.Vector3(p.x, p.y + config.BodyHeight - config.BodyRadius, p.z);

                for (int d = 0; d < doors.Count; d++)
                {
                    if (DoorGeometry.Crushes(doors[d].ToDoor(), tick, bottom, top, config.BodyRadius))
                    {
                        Respawn(diver, p.y);
                        break;
                    }
                }
            }
        }

        private void Respawn(GameFramework.World.Entity diver, float deathY)
        {
            float shelfY = SkydiveCheckpoints.LastPassedShelfY(deathY, shelfYs, spawnY);
            respawnCounts.TryGetValue(shelfY, out int order);
            SkydiveRespawn.To(diver, deathY, config, shelfYs, spawnY, respawnPoints, ref order);
            respawnCounts[shelfY] = order;
        }

        //  걸러내는 기준은 SkydiveLaserSystem.CollectDivers와 같아야 한다 — 판정 대상 집합이
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
