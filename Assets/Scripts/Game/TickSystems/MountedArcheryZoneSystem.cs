using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 말 타고 활쏘기 원(서버 End 페이즈, 적중 판정 뒤·방송 앞). 출발 틱 기준으로 때가 되면 다음 원을 정해 공개하고,
    /// 원 밖에 있는 살아 있는 몸에 초당 피해를 준다. 체력이 0이 되면 처치자 없이 탈락이다.
    /// 다음 원의 자리는 공개하는 이 순간에야 뽑는다 — 미리 계산해 두면 클라가 미래를 알 수 있다.
    /// </summary>
    public sealed class MountedArcheryZoneSystem : GameFramework.Runner.ITickSystem
    {
        //  맵 한가운데. 스폰 시스템도 원점을 바라보게 기수를 세운다(MountedArcheryRuleSystem.YawFacingCenter).
        private static readonly Vector2 ArenaCenter = Vector2.zero;
        //  소수를 모아 정수로 깎을 때 float 오차(3.9999처럼 모자라게 나오는 것)로 한 틱 늦지 않게 하는 여유.
        private const double Epsilon = 1e-6;

        private readonly MountedArcheryMatchState state;
        private readonly MountedArcheryConfig config;
        private readonly GameFramework.World.EntityRegistry registry;
        private readonly GameFramework.World.HealthSystem health;
        private readonly GameFramework.World.IWorld world;
        private readonly System.Random random;
        private readonly System.Action<string> despawn;
        private readonly float tickInterval;

        // 몸마다 아직 깎지 못한 소수 피해.
        private readonly Dictionary<string, double> pending = new Dictionary<string, double>();
        private readonly List<string> ids = new List<string>();

        public MountedArcheryZoneSystem(MountedArcheryMatchState state, MountedArcheryConfig config,
                                        GameFramework.World.EntityRegistry registry, GameFramework.World.HealthSystem health,
                                        GameFramework.World.IWorld world, System.Random random,
                                        System.Action<string> despawn = null, float tickInterval = 0.02f)
        {
            this.state = state;
            this.config = config;
            this.registry = registry;
            this.health = health;
            this.world = world;
            this.random = random;
            this.despawn = despawn;
            this.tickInterval = tickInterval;
        }

        public void Tick(long tick, float deltaTime)
        {
            long start = world.GameplayStartTick;
            if (start == long.MaxValue || tick < start)
            {
                return;
            }

            RevealDue(tick, start);
            DamageOutside(tick, deltaTime);
        }

        private void RevealDue(long tick, long start)
        {
            while (state.ZoneStages.Count < config.ZoneStages.Count)
            {
                int index = state.ZoneStages.Count;
                long revealTick = index == 0
                    ? start + Ticks(config.ZoneFirstRevealSeconds)
                    : state.ZoneStages[index - 1].shrinkEndTick;
                if (tick < revealTick)
                {
                    return;
                }

                var row = config.ZoneStages[index];
                Vector2 fromCenter = index == 0 ? ArenaCenter : state.ZoneStages[index - 1].toCenter;
                float fromRadius = index == 0 ? config.ZoneInitialRadius : state.ZoneStages[index - 1].toRadius;
                Vector2 toCenter = MountedArcheryZone.PickNextCenter(
                    fromCenter, fromRadius, row.endRadius, (float)random.NextDouble(), (float)random.NextDouble());
                long shrinkStart = revealTick + Ticks(row.restSeconds);
                long shrinkEnd = shrinkStart + Ticks(row.shrinkSeconds);

                state.AddZoneStage(new ZoneStage(index + 1, revealTick, shrinkStart, shrinkEnd,
                    fromCenter, fromRadius, toCenter, row.endRadius, row.damagePerSecond));
            }
        }

        private void DamageOutside(long tick, float deltaTime)
        {
            float dps = MountedArcheryZone.DamagePerSecondAt(state.ZoneStages, tick);
            if (dps <= 0f)
            {
                return;
            }
            var circle = MountedArcheryZone.CircleAt(state.ZoneStages, tick, ArenaCenter, config.ZoneInitialRadius);

            //  순서를 고정해 처리 순서가 딕셔너리 순서를 안 타게 한다.
            ids.Clear();
            foreach (var pair in state.Players)
            {
                if (pair.Value.Alive)
                {
                    ids.Add(pair.Key);
                }
            }
            ids.Sort(string.CompareOrdinal);

            foreach (var id in ids)
            {
                if (registry.TryGet(id, out var entity) == false)
                {
                    continue;
                }
                var hp = entity.Get<GameFramework.World.Health>();
                var transform = entity.Get<GameFramework.World.Transform>();
                if (hp == null || transform == null)
                {
                    continue;
                }

                var position = new Vector2(transform.Position.X, transform.Position.Z);
                if (MountedArcheryZone.IsOutside(position, circle) == false)
                {
                    continue;
                }

                pending.TryGetValue(id, out double owed);
                owed += (double)dps * deltaTime;
                int whole = (int)System.Math.Floor(owed + Epsilon);
                pending[id] = owed - whole;
                if (whole < 1)
                {
                    continue;
                }

                health.TakeDamage(hp, whole);
                if (hp.Current <= 0 && state.Eliminate(id, "", tick))
                {
                    despawn?.Invoke(id);
                }
            }
        }

        private long Ticks(float seconds) => (long)System.Math.Round(seconds / tickInterval);
    }
}
