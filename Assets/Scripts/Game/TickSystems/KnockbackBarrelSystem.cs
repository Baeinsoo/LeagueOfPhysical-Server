using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 드럼통 굴림(서버 End 페이즈, 맞음 판정 뒤). 마찰로 서고, 기둥·서로 튕기고, 바닥이 없으면 떨어져 사라지고,
    /// 서 있는 몸과 부딪히면 그 몸을 날린다(위험도는 올리지 않는다).
    /// 드럼통은 유니티 몸이 없어 위치·속도를 여기서 직접 쓴다. 클라는 이 결과를 스냅샷으로 받아 보간해 그린다.
    /// </summary>
    public sealed class KnockbackBarrelSystem : GameFramework.Runner.ITickSystem
    {
        //  이보다 낮으면 바닥 밖으로 빠져 떨어지는 중이다(몸과 같은 기준).
        private const float StandingMinY = -0.05f;
        //  몸·KinematicMoveSystem과 같은 중력.
        private const float Gravity = -9.81f * 2f;

        private readonly KnockbackMatchState state;
        private readonly KnockbackConfig config;
        private readonly KnockbackWorld world;
        private readonly GameFramework.World.EntityRegistry registry;
        private readonly KnockbackPillars pillars;
        private readonly System.Action<string> despawn;
        private readonly float tickInterval;

        private readonly List<GameFramework.World.Entity> barrels = new List<GameFramework.World.Entity>();
        private readonly List<Vector2> standingPillars = new List<Vector2>();
        private readonly HashSet<string> despawned = new HashSet<string>();

        public KnockbackBarrelSystem(KnockbackMatchState state, KnockbackConfig config, KnockbackWorld world,
                                     GameFramework.World.EntityRegistry registry, KnockbackPillars pillars,
                                     System.Action<string> despawn, float tickInterval = 0.02f)
        {
            this.state = state;
            this.config = config;
            this.world = world;
            this.registry = registry;
            this.pillars = pillars;
            this.despawn = despawn;
            this.tickInterval = tickInterval;
        }

        /// <summary>떨어지는 중이 아닌 드럼통을 id 순으로 into에 담는다. 맞음 판정도 같은 목록을 쓴다.</summary>
        public static void CollectRolling(GameFramework.World.EntityRegistry registry, List<GameFramework.World.Entity> into)
        {
            into.Clear();
            foreach (var entity in registry.All)
            {
                if (entity.Has<KnockbackBarrel>() == false) continue;
                var t = entity.Get<GameFramework.World.Transform>();
                if (t == null || t.Position.Y < StandingMinY) continue;
                into.Add(entity);
            }
            into.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        }

        public void Tick(long tick, float deltaTime)
        {
            if (config.Barrels.Count <= 0 || tick < world.GameplayStartTick)
            {
                return;
            }

            //  떨어지는 중인 것도 포함해 전부 돌려야 깊이를 넘으면 치운다.
            barrels.Clear();
            foreach (var entity in registry.All)
            {
                if (entity.Has<KnockbackBarrel>()) barrels.Add(entity);
            }
            if (barrels.Count == 0) return;
            barrels.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            var layout = config.Barrels;
            pillars.CollectStanding(tick, standingPillars);
            float pillarReach = pillars.Radius + layout.Radius;
            float pairReach = layout.Radius * 2f;

            for (int i = 0; i < barrels.Count; i++)
            {
                Move(barrels[i], tick, layout, pillarReach);
            }

            for (int i = 0; i < barrels.Count; i++)
            {
                if (IsFalling(barrels[i])) continue;
                for (int j = i + 1; j < barrels.Count; j++)
                {
                    if (IsFalling(barrels[j])) continue;
                    PairBounce(barrels[i], barrels[j], pairReach, layout.Restitution);
                }
            }

            HitBodies(tick, layout);
        }

        private static bool IsFalling(GameFramework.World.Entity barrel)
            => barrel.Get<GameFramework.World.Transform>().Position.Y < StandingMinY;

        private void Move(GameFramework.World.Entity barrel, long tick, KnockbackBarrelLayout layout, float pillarReach)
        {
            var transform = barrel.Get<GameFramework.World.Transform>();
            var velocity = barrel.Get<GameFramework.World.Velocity>();
            var p = transform.Position;
            var v = velocity.Linear;
            Vector2 position = new Vector2(p.X, p.Z);
            Vector2 horizontal = new Vector2(v.X, v.Z);
            float y = p.Y;
            float vy = v.Y;

            bool falling = y < 0f || world.Floor.IsSolid(position.x, position.y, tick) == false;
            if (falling)
            {
                //  떨어지는 동안은 아무것도 안 부딪히고 옆 속도도 그대로다.
                vy += Gravity * tickInterval;
                y += vy * tickInterval;
                position += horizontal * tickInterval;
            }
            else
            {
                horizontal = KnockbackBarrelPhysics.ApplyFriction(horizontal, layout.Friction, tickInterval);
                position += horizontal * tickInterval;
                for (int k = 0; k < standingPillars.Count; k++)
                {
                    KnockbackBarrelPhysics.BounceOffFixedCircle(ref position, ref horizontal, standingPillars[k], pillarReach, layout.Restitution);
                }
                y = 0f;
                vy = 0f;
            }

            transform.Position = new System.Numerics.Vector3(position.x, y, position.y);
            velocity.Linear = new System.Numerics.Vector3(horizontal.x, vy, horizontal.y);

            if (y < -config.FallDepth && despawned.Add(barrel.Id))
            {
                despawn(barrel.Id);
            }
        }

        private static void PairBounce(GameFramework.World.Entity a, GameFramework.World.Entity b, float reach, float restitution)
        {
            var ta = a.Get<GameFramework.World.Transform>();
            var tb = b.Get<GameFramework.World.Transform>();
            var va = a.Get<GameFramework.World.Velocity>();
            var vb = b.Get<GameFramework.World.Velocity>();
            Vector2 pa = new Vector2(ta.Position.X, ta.Position.Z);
            Vector2 pb = new Vector2(tb.Position.X, tb.Position.Z);
            Vector2 ha = new Vector2(va.Linear.X, va.Linear.Z);
            Vector2 hb = new Vector2(vb.Linear.X, vb.Linear.Z);
            if ((pb - pa).sqrMagnitude >= reach * reach) return;

            KnockbackBarrelPhysics.BouncePair(ref pa, ref ha, ref pb, ref hb, reach, restitution);
            ta.Position = new System.Numerics.Vector3(pa.x, ta.Position.Y, pa.y);
            tb.Position = new System.Numerics.Vector3(pb.x, tb.Position.Y, pb.y);
            va.Linear = new System.Numerics.Vector3(ha.x, va.Linear.Y, ha.y);
            vb.Linear = new System.Numerics.Vector3(hb.x, vb.Linear.Y, hb.y);
        }

        private void HitBodies(long tick, KnockbackBarrelLayout layout)
        {
            float reach = layout.Radius + config.HitRadius;
            foreach (var id in state.Players)
            {
                if (state.EliminatedTick.ContainsKey(id) || registry.TryGet(id, out var entity) == false)
                {
                    continue;
                }
                var t = entity.Get<GameFramework.World.Transform>();
                var body = entity.Get<KnockbackBody>();
                var velocity = entity.Get<GameFramework.World.Velocity>();
                if (t == null || body == null || velocity == null || t.Position.Y < StandingMinY)
                {
                    continue;
                }

                for (int i = 0; i < barrels.Count; i++)
                {
                    var barrel = barrels[i];
                    if (IsFalling(barrel)) continue;
                    var bt = barrel.Get<GameFramework.World.Transform>();
                    var bv = barrel.Get<GameFramework.World.Velocity>();
                    Vector2 barrelPos = new Vector2(bt.Position.X, bt.Position.Z);
                    Vector2 barrelVel = new Vector2(bv.Linear.X, bv.Linear.Z);
                    Vector2 bodyPos = new Vector2(t.Position.X, t.Position.Z);
                    Vector2 bodyVel = new Vector2(velocity.Linear.X, velocity.Linear.Z);
                    if (KnockbackBarrelPhysics.TryHitBody(barrelPos, barrelVel, bodyPos, bodyVel, reach, layout.HitScale,
                                                          out var direction, out float strength) == false)
                    {
                        continue;
                    }

                    KnockbackRules.ApplyPush(body, velocity, direction, strength, tick, config);

                    string pusher = barrel.Get<KnockbackBarrel>().PusherId;
                    if (pusher != null && pusher != id)
                    {
                        state.Team.TryGetValue(pusher, out int pusherTeam);
                        state.Team.TryGetValue(id, out int bodyTeam);
                        if (KnockbackTeams.SameTeam(pusherTeam, bodyTeam) == false)
                        {
                            state.Credit.RecordShotHit(id, pusher, tick);
                        }
                    }

                    //  친 드럼통은 몸의 새 속도보다 빠르게 몸 쪽으로 가지 않는다 - 같은 몸을 매 틱 다시 치지 않게.
                    float bodyAfter = Vector2.Dot(new Vector2(velocity.Linear.X, velocity.Linear.Z), direction);
                    barrelVel = KnockbackBarrelPhysics.YieldToBody(barrelVel, direction, bodyAfter);
                    bv.Linear = new System.Numerics.Vector3(barrelVel.x, bv.Linear.Y, barrelVel.y);
                }
            }
        }
    }
}
