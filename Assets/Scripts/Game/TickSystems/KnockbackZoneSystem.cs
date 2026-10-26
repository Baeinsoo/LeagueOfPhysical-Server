using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 넉백 원 공개(서버 End 페이즈). 출발 뒤 첫 공개, 이후엔 앞 단계 축소가 끝나는 틱에 다음 원을 정해 공개한다.
    /// 다음 원의 자리는 공개하는 이 순간에야 뽑는다 — 미리 계산해 두면 클라가 미래를 알 수 있다.
    /// 이 모드의 원은 피해를 주지 않고 바닥만 무너뜨린다(원 밖 칸이 사라진다).
    /// </summary>
    public sealed class KnockbackZoneSystem : GameFramework.Runner.ITickSystem
    {
        private static readonly Vector2 ArenaCenter = Vector2.zero;

        private readonly KnockbackMatchState state;
        private readonly KnockbackConfig config;
        private readonly KnockbackWorld world;
        private readonly System.Random random;
        private readonly float tickInterval;

        public KnockbackZoneSystem(KnockbackMatchState state, KnockbackConfig config, KnockbackWorld world,
                                   System.Random random, float tickInterval = 0.02f)
        {
            this.state = state;
            this.config = config;
            this.world = world;
            this.random = random;
            this.tickInterval = tickInterval;
        }

        public void Tick(long tick, float deltaTime)
        {
            long start = world.GameplayStartTick;
            if (start == long.MaxValue || tick < start)
            {
                return;
            }

            bool revealed = false;
            while (state.ZoneStages.Count < config.ZoneStages.Count)
            {
                int index = state.ZoneStages.Count;
                long revealTick = index == 0
                    ? start + Ticks(config.ZoneFirstRevealSeconds)
                    : state.ZoneStages[index - 1].shrinkEndTick;
                if (tick < revealTick)
                {
                    break;
                }

                var row = config.ZoneStages[index];
                Vector2 fromCenter = index == 0 ? ArenaCenter : state.ZoneStages[index - 1].toCenter;
                float fromRadius = index == 0 ? world.Floor.ZoneStartRadius : state.ZoneStages[index - 1].toRadius;
                Vector2 toCenter = MountedArcheryZone.PickNextCenter(
                    fromCenter, fromRadius, row.endRadius, (float)random.NextDouble(), (float)random.NextDouble());
                long shrinkStart = revealTick + Ticks(row.restSeconds);
                long shrinkEnd = shrinkStart + Ticks(row.shrinkSeconds);

                state.AddZoneStage(new ZoneStage(index + 1, revealTick, shrinkStart, shrinkEnd,
                    fromCenter, fromRadius, toCenter, row.endRadius, 0f));
                revealed = true;
            }

            if (revealed)
            {
                world.Floor.SetZoneStages(state.ZoneStages);
            }
        }

        private long Ticks(float seconds) => (long)System.Math.Round(seconds / tickInterval);
    }
}
