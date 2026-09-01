using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 결승선에 누가 먼저 닿았는지 매 틱 지켜보고 순서를 적어 둔다. <b>게임을 모른다</b> — 어느 축으로
    /// 어느 방향으로 달리는지만 받고, 나머지는 <see cref="FinishLineOverlap"/>과
    /// <see cref="FinishOrderTracker"/>가 답한다. Flappy는 <c>X, 커지는 방향</c>,
    /// Skydive는 <c>Y, 작아지는 방향</c>으로 등록한다.
    ///
    /// <para>판정은 <b>좌표 한 점이 아니라 형상</b>으로 한다 — 몸의 콜라이더 바운드와 결승선의 보이는
    /// 판(Renderer) 바운드가 그 축에서 겹치면 통과다. 그래야 새의 부리가 선에 닿은 순간이 통과가
    /// 되고, 화면에서 보이는 것과 결과가 어긋나지 않는다(중심으로 재면 몸 반지름만큼 늦게 잡힌다).</para>
    ///
    /// <para>순서를 세는 일과 판을 끝내는 일은 나눈다 — 룰에는 틱이 없어서다(판치기의 룰/턴 짝과 같은 구조).</para>
    /// </summary>
    public class FinishLineTrackingSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ActorRegistry actorRegistry;
        private readonly FinishAxis axis;
        private readonly bool increasing;
        //  마커를 못 찾았을 때 대신 쓸 좌표. null이면 추적을 아예 하지 않는다.
        private readonly float? fallbackCoordinate;

        private readonly FinishOrderTracker tracker = new FinishOrderTracker();
        private readonly List<string> watched = new List<string>();

        // 결승선은 맵이 정한다. 맵 씬은 나중에 로드되므로 생성자에서 찾으면 못 찾는다 —
        // 첫 틱까지 미뤘다가 그때 한 번만 찾는다.
        private Bounds? lineBounds;
        private bool lineResolved;

        public FinishLineTrackingSystem(GameFramework.World.EntityRegistry entityRegistry,
                                        ActorRegistry actorRegistry,
                                        FinishAxis axis, bool increasing,
                                        float? fallbackCoordinate = null)
        {
            this.entityRegistry = entityRegistry;
            this.actorRegistry = actorRegistry;
            this.axis = axis;
            this.increasing = increasing;
            this.fallbackCoordinate = fallbackCoordinate;
        }

        /// <summary>먼저 닿은 순. 같은 틱이면 깊이 넘은 쪽이 앞.</summary>
        public IReadOnlyList<FinishRecord> Ordered => tracker.Ordered;

        public bool HasFinished(string entityId) => tracker.HasFinished(entityId);

        public void Watch(string entityId) => watched.Add(entityId);

        public void Reset()
        {
            watched.Clear();
            tracker.Reset();
            lineBounds = null;
            lineResolved = false;
        }

        public void Tick(long tick, float deltaTime)
        {
            EnsureFinishLine();
            if (lineBounds.HasValue == false)
            {
                return;
            }

            for (int i = 0; i < watched.Count; i++)
            {
                string entityId = watched[i];
                if (tracker.HasFinished(entityId))
                {
                    continue;
                }
                if (TryGetBodyBounds(entityId, out Bounds body) == false)
                {
                    continue;   // 나간 사람의 몸은 이미 없다
                }

                tracker.Observe(entityId, tick,
                    FinishLineOverlap.Past(body, lineBounds.Value, axis, increasing));
            }
        }

        /// <summary>
        /// 남아 있는 사람이 전원 통과했나. <b>아무도 없으면 false</b> — 스폰 직전에 판이 끝나는 것을 막는다.
        /// </summary>
        public bool AllWatchedFinished
        {
            get
            {
                int alive = 0;
                for (int i = 0; i < watched.Count; i++)
                {
                    if (entityRegistry.Get(watched[i]) == null)
                    {
                        continue;   // 나간 사람은 세지 않는다. 세면 한 명 나간 판이 절대 안 끝난다
                    }
                    alive++;
                    if (tracker.HasFinished(watched[i]) == false)
                    {
                        return false;
                    }
                }
                return alive > 0;
            }
        }

        //  몸의 월드 바운드. 콜라이더가 있으면 그것이 곧 눈에 보이는 형상이다.
        //  없으면(있어선 안 되지만) 진실원본 좌표를 크기 0으로 써서, 적어도 옛 방식과 같게 굴러가게 한다.
        private bool TryGetBodyBounds(string entityId, out Bounds bounds)
        {
            bounds = default;

            var actor = actorRegistry.Get(entityId);
            if (actor != null)
            {
                var collider = actor.GetComponent<Collider>();
                if (collider != null)
                {
                    bounds = collider.bounds;
                    return true;
                }
            }

            var entity = entityRegistry.Get(entityId);
            if (entity?.Get<GameFramework.World.Transform>() == null)
            {
                return false;
            }
            bounds = new Bounds(GameFramework.World.EntityMotionExtensions.GetPosition(entity), Vector3.zero);
            return true;
        }

        private void EnsureFinishLine()
        {
            if (lineResolved)
            {
                return;
            }
            lineResolved = true;

            var markers = UnityEngine.Object.FindObjectsByType<FinishLine>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (markers.Length == 1)
            {
                lineBounds = MarkerBounds(markers[0]);
                return;
            }

            if (fallbackCoordinate.HasValue == false)
            {
                Debug.LogError($"[Finish] 맵에 FinishLine 마커가 정확히 하나 있어야 한다 (발견: {markers.Length}개). " +
                    "도착을 세지 못하므로 이 판은 등수를 매길 수 없다.");
                return;
            }

            //  판이 이미 굴러가는 중이라 던지면 방 전체가 죽는다. 크게 알리고, 게임이 알려 준
            //  좌표(시뮬이 실제로 쓰는 값)를 두께 0인 선으로 삼아 판은 끝나게 둔다.
            Debug.LogError($"[Finish] 맵에 FinishLine 마커가 정확히 하나 있어야 한다 (발견: {markers.Length}개). " +
                $"결승선을 {axis}={fallbackCoordinate.Value}로 대체한다.");
            lineBounds = FallbackBounds(fallbackCoordinate.Value);
        }

        //  보이는 판이 곧 결승선이다. 렌더러가 없으면(마커만 찍어 둔 맵) 좌표를 두께 0인 선으로 쓴다.
        private Bounds MarkerBounds(FinishLine marker)
        {
            var renderer = marker.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                return renderer.bounds;
            }
            Vector3 position = marker.transform.position;
            float coordinate = axis == FinishAxis.X ? position.x
                             : axis == FinishAxis.Y ? position.y
                             : position.z;
            return FallbackBounds(coordinate);
        }

        private Bounds FallbackBounds(float coordinate)
        {
            Vector3 center = axis == FinishAxis.X ? new Vector3(coordinate, 0f, 0f)
                           : axis == FinishAxis.Y ? new Vector3(0f, coordinate, 0f)
                           : new Vector3(0f, 0f, coordinate);
            return new Bounds(center, Vector3.zero);
        }
    }
}
