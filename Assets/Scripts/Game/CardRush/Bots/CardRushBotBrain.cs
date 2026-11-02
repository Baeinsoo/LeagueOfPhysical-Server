namespace LOP
{
    /// <summary>
    /// 카드 러시 봇의 판단 — 카드가 새로 시작될 때마다 "몇 틱에 누를지" 하나를 정한다.
    /// 사람처럼: 카드를 보고 반응 시간이 지나야 노릴 수 있고, 요구 색 창의 가운데를 노리되 조금 흔들린다.
    /// </summary>
    public sealed class CardRushBotBrain
    {
        private readonly System.Random rng;
        private readonly int reactTicks;
        private readonly int jitterTicks;
        private readonly double missChance;
        private (int step, int attempt, long cardStart) plannedFor = (-1, -1, -1);
        private long plannedTick = long.MaxValue;

        public CardRushBotBrain(System.Random rng, CardRushConfig config)
        {
            this.rng = rng;
            float react = config.BotReactMinSeconds + (float)rng.NextDouble() * (config.BotReactMaxSeconds - config.BotReactMinSeconds);
            reactTicks = CardRushTiming.Ticks(react);
            jitterTicks = (int)System.Math.Round(config.BotTimingJitterSeconds / CardRushTiming.TickInterval);
            missChance = config.BotMissChance;
        }

        public long PlannedTick => plannedTick;

        /// <summary>이번 틱에 누르나. 카드가 바뀌었으면(칸·시도·시작 틱) 먼저 새로 계획한다.</summary>
        public bool ShouldGrab(in CardRushRunnerState s, long tick, long gameplayStartTick, CardRushCourse course, in CardRushTiming timing)
        {
            if (course == null || s.IsFinished(course.StepCount) || gameplayStartTick == long.MaxValue) return false;
            long cardStart = CardRushCard.CardStartTick(s, gameplayStartTick, timing.MoveTicks);
            var key = (s.Step, s.Attempt, cardStart);
            if (key != plannedFor)
            {
                plannedFor = key;
                plannedTick = Plan(s, cardStart, course);
            }
            return tick == plannedTick;
        }

        private long Plan(in CardRushRunnerState s, long cardStart, CardRushCourse course)
        {
            int dwell = course.DwellTicks(s.Step);
            int k = ((int)course.RequiredColor(s.Step) - (int)course.StartColor(s.Step, s.Attempt) + CardColors.Count) % CardColors.Count;
            long windowStart = cardStart + (long)k * dwell;
            //  카드를 보고 반응 시간이 지나기 전에 가운데가 오는 창은 못 노린다 — 한 바퀴 뒤 창으로.
            while (windowStart + dwell / 2 < cardStart + reactTicks) windowStart += (long)CardColors.Count * dwell;
            long aim = windowStart + dwell / 2 + rng.Next(-jitterTicks, jitterTicks + 1);
            if (rng.NextDouble() < missChance) aim += rng.Next(0, 2) == 0 ? -dwell : dwell;
            return System.Math.Max(aim, cardStart);
        }
    }
}
