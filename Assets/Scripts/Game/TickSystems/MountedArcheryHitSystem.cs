using System.Collections.Generic;
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 말 타고 활쏘기 화살 판정(서버 End 페이즈, <see cref="MotionHistorySystem"/> 바로 뒤).
    /// 화살은 지금 자리로, 상대 몸은 <b>쏜 사람 화면에 보이던 틱</b>의 자리로 잰다(래그 보상).
    /// 바위·나무·테두리는 되감지 않는다 — 지금 지형이 먼저 막으면 안 맞는다.
    /// 같은 틱의 적중을 다 깎은 뒤에 쓰러짐을 세므로, 서로 맞혀 함께 쓰러지면 둘 다 탈락이다.
    /// </summary>
    public sealed class MountedArcheryHitSystem : GameFramework.Runner.ITickSystem
    {
        private readonly MountedArcheryWorld world;
        private readonly GameFramework.World.EntityRegistry registry;
        private readonly MountedArcheryMatchState state;
        private readonly MountedArcheryConfig config;
        private readonly GameFramework.World.HealthSystem health;
        private readonly GameFramework.World.WorldEventBuffer events;
        private readonly System.Action<string> despawn;
        private readonly System.Func<Vector3, Vector3, float?> obstacleAlong;
        private readonly float tickInterval;

        // 화살 하나의 되감기 폭은 쏜 틱에 한 번 정하고 날아가는 내내 쓴다.
        private readonly Dictionary<(string shooterId, long fireTick), (int rewind, long viewTick)> rewinds
            = new Dictionary<(string, long), (int, long)>();
        // 누구를 맞혔거나 지형에 꽂힌 화살 — 다시 재지 않는다.
        private readonly HashSet<(string shooterId, long fireTick)> spent = new HashSet<(string, long)>();

        private readonly List<string> candidates = new List<string>();
        private readonly List<(string targetId, string attackerId)> hits = new List<(string, string)>();
        private readonly Dictionary<string, string> lastAttacker = new Dictionary<string, string>();
        private readonly List<(string, long)> expired = new List<(string, long)>();

        public MountedArcheryHitSystem(MountedArcheryWorld world, GameFramework.World.EntityRegistry registry,
                                       MountedArcheryMatchState state, MountedArcheryConfig config,
                                       GameFramework.World.HealthSystem health, GameFramework.World.WorldEventBuffer events,
                                       System.Action<string> despawn, System.Func<Vector3, Vector3, float?> obstacleAlong,
                                       float tickInterval = 0.02f)
        {
            this.world = world;
            this.registry = registry;
            this.state = state;
            this.config = config;
            this.health = health;
            this.events = events;
            this.despawn = despawn;
            this.obstacleAlong = obstacleAlong;
            this.tickInterval = tickInterval;
        }

        public void Tick(long tick, float deltaTime)
        {
            state.LastTick = tick;

            CollectCandidates();
            hits.Clear();

            int maxRewind = config.LagCompMaxTicks(tickInterval);
            var shots = world.Shots;
            for (int i = 0; i < shots.Count; i++)
            {
                var shot = shots[i];
                var key = (shot.ShooterId, shot.FireTick);
                if (rewinds.TryGetValue(key, out var lag) == false)
                {
                    //  쏜 틱을 놓친 화살(이 시스템이 늦게 붙은 경우 등)은 쏜 사람 화면을 모르니 되감지 않는다.
                    lag = shot.FireTick == tick ? RewindFor(shot, maxRewind) : (0, 0L);
                    rewinds[key] = lag;
                    //  되감기 상한을 실측으로 정하려고 남긴다(슬라이스 4). 상한에 잘리기 전 값(raw)도 같이 — 잘리는지가 궁금한 것이다.
                    //  한 발에 한 줄이라 릴리스 빌드에서도 남긴다.
                    Debug.Log($"[LagComp] fire shooter={shot.ShooterId} fireTick={shot.FireTick} viewTick={lag.viewTick} " +
                              $"raw={(lag.viewTick > 0 ? shot.FireTick - lag.viewTick : 0)} rewind={lag.rewind} max={maxRewind}");
                }
                int rewind = lag.rewind;
                if (spent.Contains(key))
                {
                    continue;
                }

                float toSeconds = (tick - shot.FireTick) * tickInterval;
                if (toSeconds <= 0f)
                {
                    continue;   // 쏜 틱엔 아직 지나온 길이 없다 — 다음 틱 선분이 출발점부터 잰다
                }
                float fromSeconds = Mathf.Max(toSeconds - tickInterval, 0f);
                Vector3 from = ArcheryTrajectory.PositionAt(shot, fromSeconds);
                Vector3 to = ArcheryTrajectory.PositionAt(shot, toSeconds);

                float? obstacle = obstacleAlong(from, to);
                long rewoundTick = MountedArcheryLagCompensation.RewindedTick(tick, rewind, world.FirstSavedTick);

                string target = NearestBody(shot.ShooterId, from, to, rewoundTick, out float along);
                if (target != null && (obstacle.HasValue == false || along < obstacle.Value))
                {
                    hits.Add((target, shot.ShooterId));
                    spent.Add(key);
                    Debug.Log($"[LagComp] hit shooter={shot.ShooterId} target={target} fireTick={shot.FireTick} " +
                              $"viewTick={lag.viewTick} rewind={rewind}");
                }
                else if (obstacle.HasValue)
                {
                    spent.Add(key);   // 지형에 꽂혔다
                }
            }

            ApplyHits();
            ResolveEliminations(tick);
            ForgetExpired(tick);
        }

        private (int rewind, long viewTick) RewindFor(in ArcheryShot shot, int maxRewind)
        {
            var command = registry.Get(shot.ShooterId)?.Get<InputBuffer>()?.Current;
            long viewTick = command?.ViewTick ?? 0;
            //  0 이하는 "클라가 화면 틱을 안 보냈다"는 뜻이다. 아주 옛날로 읽어 상한까지 되감으면
            //  0만 보내는 클라가 늘 가장 유리해진다 — 되감지 않는다.
            if (viewTick <= 0)
            {
                return (0, viewTick);
            }
            return (MountedArcheryLagCompensation.RewindTicks(shot.FireTick, viewTick, maxRewind), viewTick);
        }

        // 지금 살아 있는 말 탄 몸. 순서를 고정해야 같은 거리의 두 몸 중 누가 맞나가 딕셔너리 순서를 안 탄다.
        private void CollectCandidates()
        {
            candidates.Clear();
            foreach (var pair in state.Players)
            {
                if (pair.Value.Alive && registry.TryGet(pair.Key, out var entity) && entity.Has<Mount>()
                    && entity.Has<GameFramework.World.Health>())
                {
                    candidates.Add(pair.Key);
                }
            }
            candidates.Sort(string.CompareOrdinal);
        }

        private string NearestBody(string shooterId, Vector3 from, Vector3 to, long rewoundTick, out float bestAlong)
        {
            string best = null;
            bestAlong = float.MaxValue;
            foreach (var id in candidates)
            {
                if (id == shooterId)
                {
                    continue;
                }

                Vector3 position;
                Quaternion rotation;
                if (world.TryGetSavedMotion(rewoundTick, id, out var saved))
                {
                    position = saved.Position.ToUnity();
                    rotation = saved.Rotation.ToUnity();
                }
                else
                {
                    var transform = registry.Get(id).Get<GameFramework.World.Transform>();
                    position = transform.Position.ToUnity();
                    rotation = transform.Rotation.ToUnity();
                }

                float heading = Mathf.DeltaAngle(0f, rotation.eulerAngles.y);
                if (MountedArcheryHitTest.SegmentHits(from, to, position, heading, out float along) && along < bestAlong)
                {
                    bestAlong = along;
                    best = id;
                }
            }
            return best;
        }

        private void ApplyHits()
        {
            foreach (var (targetId, attackerId) in hits)
            {
                health.TakeDamage(registry.Get(targetId).Get<GameFramework.World.Health>(), config.ArrowDamage);
                events.Append(new GameFramework.World.DamageDealtEvent(
                    targetId, attackerId, config.ArrowDamage, false, false));
                lastAttacker[targetId] = attackerId;
            }
        }

        private void ResolveEliminations(long tick)
        {
            if (hits.Count == 0)
            {
                return;
            }

            foreach (var id in candidates)
            {
                if (registry.Get(id).Get<GameFramework.World.Health>().Current > 0)
                {
                    continue;
                }

                lastAttacker.TryGetValue(id, out var killer);
                if (state.Eliminate(id, killer, tick))
                {
                    despawn(id);
                }
            }
        }

        private void ForgetExpired(long tick)
        {
            float lifetimeTicks = ArcheryTrajectory.LifetimeSeconds / tickInterval;
            expired.Clear();
            foreach (var key in rewinds.Keys)
            {
                if (tick - key.fireTick > lifetimeTicks)
                {
                    expired.Add(key);
                }
            }
            foreach (var key in expired)
            {
                rewinds.Remove(key);
                spent.Remove(key);
            }
        }
    }
}
