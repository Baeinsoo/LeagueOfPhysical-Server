namespace LOP
{
    /// <summary>
    /// 카드 러시 봇의 판단 — 사람처럼 두 번 누른다. 뒷면 카드가 뜨면 반응 시간 뒤에 시작하고(가끔 한 박자 늦게),
    /// 돌기 시작하면 요구 색 창의 가운데를 노려 던진다(조금 흔들리고, 가끔 일부러 옆 색).
    /// 계획은 카드가 바뀔 때(칸·시도·준비 틱·시작 틱)마다 하나 — 그 틱이 오면 누른다.
    /// </summary>
    public sealed class CardRushBotBrain
    {
        /// <summary>사람도 가끔 한 박자 늦게 시작한다 — 봇 레인이 기계처럼 일정하지 않게.</summary>
        public const double LateStartChance = 0.15;
        public const int LateStartMinTicks = 10;
        public const int LateStartMaxTicks = 30;

        private readonly System.Random rng;
        private readonly int reactTicks;
        private readonly int jitterTicks;
        private readonly double missChance;
        private (int step, int attempt, long ready, long spin) plannedFor = (-1, -1, -1, -1);
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

        /// <summary>마지막 시작 계획이 "늦은 시작"이었나 — 시험이 비율을 본다.</summary>
        public bool LastPlanWasLateStart { get; private set; }

        public bool ShouldGrab(in CardRushRunnerState s, long tick, long gameplayStartTick, CardRushCourse course, in CardRushTiming timing)
        {
            if (course == null || s.IsFinished(course.StepCount) || gameplayStartTick == long.MaxValue) return false;
            long ready = CardRushCard.ReadyTick(s, gameplayStartTick, timing.MoveTicks);
            var key = (s.Step, s.Attempt, ready, s.SpinStartTick);
            if (key != plannedFor)
            {
                plannedFor = key;
                plannedTick = s.SpinStartTick == 0 ? PlanStart(ready) : PlanThrow(s, course);
            }
            return tick == plannedTick;
        }

        private long PlanStart(long ready)
        {
            long aim = ready + reactTicks + rng.Next(-jitterTicks, jitterTicks + 1);
            LastPlanWasLateStart = rng.NextDouble() < LateStartChance;
            if (LastPlanWasLateStart) aim += rng.Next(LateStartMinTicks, LateStartMaxTicks + 1);
            return System.Math.Max(aim, ready);
        }

        private long PlanThrow(in CardRushRunnerState s, CardRushCourse course)
        {
            const int dwell = CardRushCard.DwellTicks;
            long spin = s.SpinStartTick;
            int k = ((int)course.RequiredColor(s.Step) - (int)course.StartColor(s.Step, s.Attempt) + CardColors.Count) % CardColors.Count;
            long windowStart = spin + (long)k * dwell;
            //  첫 장을 보고 반응 시간이 지나기 전에 가운데가 오는 창은 못 노린다 — 한 바퀴 뒤 창으로.
            while (windowStart + dwell / 2 < spin + reactTicks) windowStart += (long)CardColors.Count * dwell;
            long aim = windowStart + dwell / 2 + rng.Next(-jitterTicks, jitterTicks + 1);
            if (rng.NextDouble() < missChance) aim += rng.Next(0, 2) == 0 ? -dwell : dwell;
            //  첫 창에서 한 장 앞(-dwell)은 시작 전이라 잘리면 첫 장(= 요구 색)이 된다 — 같은 바퀴의 다른 옆 색으로 돌린다.
            if (aim < spin + 1) aim += 2L * dwell;
            //  시작 틱엔 못 던지고(같은 틱 시작·던짐 없음), 마감 뒤엔 이미 저절로 던졌다.
            return System.Math.Clamp(aim, spin + 1, CardRushCard.DeadlineTick(s));
        }
    }
}
