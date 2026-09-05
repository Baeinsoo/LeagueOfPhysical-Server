using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 뒤에서 오는 벽(추격자)이 누구를 잡았는지 매 틱 지켜본다. 벽 위치는 시계만 보면 나오므로
    /// (<see cref="FlappyChaserCurve"/>) 클라에 보낼 것이 없다 — 잡는 판단만 서버 권위다.
    ///
    /// <para>순서를 적는 일과 판을 끝내는 일을 나누는 것은 <see cref="FinishTrackingSystem"/>과
    /// 같은 구조다. 룰에는 틱이 없어서다.</para>
    /// </summary>
    public class FlappyChaserSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.IWorld world;
        private readonly FinishTrackingSystem finishSystem;
        private readonly EntitySpawner entitySpawner;
        private readonly FinishLineBounds finishLine;
        private readonly FlappyConfig config;

        private readonly List<string> watched = new List<string>();
        private readonly List<string> eliminated = new List<string>();

        public FlappyChaserSystem(GameFramework.World.EntityRegistry entityRegistry,
                                  GameFramework.World.IWorld world,
                                  FinishTrackingSystem finishSystem,
                                  EntitySpawner entitySpawner,
                                  FinishLineBounds finishLine,
                                  FlappyConfig config)
        {
            this.entityRegistry = entityRegistry;
            this.world = world;
            this.finishSystem = finishSystem;
            this.entitySpawner = entitySpawner;
            this.finishLine = finishLine;
            this.config = config;
        }

        /// <summary>먼저 잡힌 순. 등수는 이 역순이다 — 오래 버틴 사람이 위다.</summary>
        public IReadOnlyList<string> EliminatedOrder => eliminated;

        public bool IsEliminated(string entityId) => eliminated.Contains(entityId);

        public void Watch(string entityId) => watched.Add(entityId);

        public void Reset()
        {
            watched.Clear();
            eliminated.Clear();
        }

        public void Tick(long tick, float deltaTime)
        {
            //  출발 전엔 벽이 시작점에 멈춰 있다. 출발틱이 아직 안 정해졌으면 long.MaxValue라
            //  이 비교가 그 경우도 같이 막는다.
            if (tick < world.GameplayStartTick)
            {
                return;
            }

            //  벽은 결승선에서 멈춘다 — 그 너머엔 감속해 서 있는 완주자들이 있고,
            //  벽이 지나가면 안 죽는데도 먹히는 것처럼 보인다.
            float stopAtX = finishLine.TryGet(out var bounds) ? bounds.min.x : float.MaxValue;
            float wallX = FlappyChaserCurve.XAt(
                config, (tick - world.GameplayStartTick) * deltaTime, stopAtX);

            for (int i = 0; i < watched.Count; i++)
            {
                string entityId = watched[i];
                if (finishSystem.HasFinished(entityId) || eliminated.Contains(entityId))
                {
                    continue;
                }

                var body = entityRegistry.Get(entityId)?.Get<GameFramework.World.Transform>();
                if (body == null)
                {
                    continue;   // 나간 사람의 몸은 이미 없다
                }

                //  중심이 아니라 꼬리로 잰다 — 결승선도 형상으로 재므로(부리가 닿는 순간),
                //  여기만 중심으로 재면 화면에서 보이는 것과 결과가 어긋난다.
                if (body.Position.X - config.BodyRadius > wallX)
                {
                    continue;
                }

                eliminated.Add(entityId);
                entitySpawner.Despawn(entityId);
                Debug.Log($"[Chaser] {entityId} 탈락 — tick={tick} 벽={wallX:F1}m 새={body.Position.X:F1}m");
            }
        }
    }
}
