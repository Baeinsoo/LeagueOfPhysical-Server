using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 상자(서버 End 페이즈). 출발 뒤 첫 상자를 예고하고 일정 간격마다 새 상자를 예고한다.
    /// 상자는 떨어지는 틱 뒤에 가장 가까운 선 몸이 주우면 사라지고 그 몸이 충격파 폭탄을 든다.
    /// 아무도 안 주운 상자가 있으면 새 상자는 건너뛴다 — 상자가 쌓이지 않는다.
    /// </summary>
    public sealed class KnockbackCrateSystem : GameFramework.Runner.ITickSystem
    {
        //  원 경계에서 이만큼 안쪽에 둔다 — 상자를 주우려다 바로 떨어지지 않게.
        private const float EdgeInset = 1.5f;
        //  이보다 낮으면 바닥 밖으로 빠져 떨어지는 중이다.
        private const float StandingMinY = -0.05f;
        private const int PickTries = 20;
        //  기둥 가장자리에서 몸이 줍기 반경 안으로 들어올 수 있도록 두는 여유(m).
        private const float PillarMargin = 0.3f;

        private readonly KnockbackMatchState state;
        private readonly KnockbackConfig config;
        private readonly KnockbackWorld world;
        private readonly GameFramework.World.EntityRegistry registry;
        private readonly System.Random random;
        private readonly KnockbackPillars pillars;
        private readonly System.Collections.Generic.List<Vector2> standingPillars = new System.Collections.Generic.List<Vector2>();

        private long nextAnnounceTick = long.MinValue;

        public KnockbackCrateSystem(KnockbackMatchState state, KnockbackConfig config, KnockbackWorld world,
                                    GameFramework.World.EntityRegistry registry, System.Random random,
                                    KnockbackPillars pillars = null)
        {
            this.pillars = pillars;
            this.state = state;
            this.config = config;
            this.world = world;
            this.registry = registry;
            this.random = random;
        }

        public void Tick(long tick, float deltaTime)
        {
            long start = world.GameplayStartTick;
            if (start == long.MaxValue || tick < start)
            {
                return;
            }
            if (nextAnnounceTick == long.MinValue)
            {
                nextAnnounceTick = start + config.CrateFirstTicks;
            }

            //  줍기가 먼저다 — 같은 틱에 새 상자를 예고해도 막 주운 상자와 겹치지 않는다.
            TryPickUp(tick);
            ClearIfFloorGone(tick);

            if (tick >= nextAnnounceTick)
            {
                //  간격이 0 이하여도 같은 틱에 되풀이하지 않게 한 틱은 민다.
                long interval = System.Math.Max(1, config.CrateIntervalTicks);
                while (nextAnnounceTick <= tick)
                {
                    nextAnnounceTick += interval;
                }
                if (state.Crate.HasValue == false)
                {
                    long land = tick + config.CrateWarnTicks;
                    //  설 자리를 못 찾으면 이번 상자는 건너뛴다. 다음 간격에 다시 시도한다.
                    if (TryPickPosition(land, out var position))
                    {
                        state.AnnounceCrate(land, position);
                    }
                }
            }
        }

        //  줍을 수 없는 자리에 남은 상자가 다음 상자를 영영 막지 않게, 칸이 사라지면 치운다.
        private void ClearIfFloorGone(long tick)
        {
            if (state.Crate.HasValue == false || tick < state.Crate.Value.LandTick)
            {
                return;
            }
            var crate = state.Crate.Value;
            if (world.Floor.IsSolid(crate.Position.x, crate.Position.y, tick) == false)
            {
                state.ClearCrate();
            }
        }

        private bool TryPickPosition(long landTick, out Vector2 position)
        {
            //  다음 원이 공개돼 있으면 그 안에 둔다 — 곧 사라질 자리에 떨구지 않는다.
            Vector2 center = Vector2.zero;
            float radius = world.Floor.ZoneStartRadius;
            if (state.ZoneStages.Count > 0)
            {
                var latest = state.ZoneStages[state.ZoneStages.Count - 1];
                center = latest.toCenter;
                radius = latest.toRadius;
            }

            //  서 있는 기둥 곁은 몸이 닿지 못해 주울 수 없다 - 위험 지대 방식에선 상자가 남아 이후 상자를 막는다.
            standingPillars.Clear();
            float clearance = 0f;
            if (pillars != null)
            {
                pillars.CollectStanding(landTick, standingPillars);
                clearance = pillars.Radius + config.CratePickupRadius + PillarMargin;
            }

            float reach = radius - EdgeInset;
            if (reach > 0f)
            {
                for (int i = 0; i < PickTries; i++)
                {
                    //  거리에 제곱근을 써서 면적으로 고르게 퍼뜨린다.
                    float distance = reach * Mathf.Sqrt((float)random.NextDouble());
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                    if (world.Floor.IsSolid(point.x, point.y, landTick) && NearPillar(point, clearance) == false)
                    {
                        position = point;
                        return true;
                    }
                }
            }
            //  마지막 수단은 원 중심 - 그곳도 설 수 있는 칸일 때만.
            position = center;
            return world.Floor.IsSolid(center.x, center.y, landTick) && NearPillar(center, clearance) == false;
        }

        private bool NearPillar(Vector2 point, float clearance)
        {
            float sqr = clearance * clearance;
            for (int i = 0; i < standingPillars.Count; i++)
            {
                if ((standingPillars[i] - point).sqrMagnitude <= sqr) return true;
            }
            return false;
        }

        private void TryPickUp(long tick)
        {
            if (state.Crate.HasValue == false || tick < state.Crate.Value.LandTick)
            {
                return;
            }

            var crate = state.Crate.Value;
            float reachSqr = config.CratePickupRadius * config.CratePickupRadius;
            string best = null;
            float bestSqr = float.MaxValue;
            foreach (var id in state.Players)
            {
                if (state.EliminatedTick.ContainsKey(id) || registry.TryGet(id, out var entity) == false)
                {
                    continue;
                }
                var transform = entity.Get<GameFramework.World.Transform>();
                var body = entity.Get<KnockbackBody>();
                var gun = entity.Get<KnockbackGun>();
                if (transform == null || body == null || gun == null || transform.Position.Y < StandingMinY)
                {
                    continue;
                }
                //  날아가는 중엔 못 줍는다. 이미 폭탄을 든 몸은 줍지 않는다 — 상자만 낭비된다.
                if (tick < body.KnockbackEndTick || gun.HasBomb)
                {
                    continue;
                }

                float sqr = (new Vector2(transform.Position.X, transform.Position.Z) - crate.Position).sqrMagnitude;
                //  거리가 같으면 id가 앞선 쪽(Players 순서가 아니라 id 순)이 가진다.
                if (sqr > reachSqr || sqr > bestSqr || (sqr == bestSqr && string.CompareOrdinal(id, best) >= 0))
                {
                    continue;
                }
                best = id;
                bestSqr = sqr;
            }

            if (best == null)
            {
                return;
            }
            registry.Get(best).Get<KnockbackGun>().HasBomb = true;
            state.ClearCrate();
        }
    }
}
