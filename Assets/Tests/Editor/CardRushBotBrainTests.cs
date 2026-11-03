using NUnit.Framework;

namespace LOP.Tests
{
    public class CardRushBotBrainTests
    {
        private const long Start = 1000;
        private static readonly CardRushCourse course = new CardRushCourse(7, 40);
        private static readonly CardRushTiming timing = new CardRushTiming(15, 30);

        private static CardRushConfig Config(float miss = 0f)
            => new CardRushConfig(120f, 0.3f, 0.6f, 4, 0.22f, 0.40f, 0.04f, miss);

        private static long FirstTap(CardRushBotBrain brain, CardRushRunnerState s, long from)
        {
            for (long t = from; t < from + 2000; t++)
                if (brain.ShouldGrab(s, t, Start, course, timing)) return t;
            return -1;
        }

        [Test]
        public void 대기면_반응_뒤에_시작한다()
        {
            var brain = new CardRushBotBrain(new System.Random(3), Config());
            var s = new CardRushRunnerState { Step = 2, MoveStartTick = 2000 };   // 준비 2015
            long t = FirstTap(brain, s, 2000);
            //  반응 최소 0.22초(11틱) − 흔들림 2틱 ≤ 시작 − 준비 ≤ 반응 최대 20틱 + 흔들림 2 + 늦은 시작 최대 30
            Assert.GreaterOrEqual(t - 2015, 9); Assert.LessOrEqual(t - 2015, 52);
        }

        [Test]
        public void 가끔_늦게_시작한다()
        {
            var brain = new CardRushBotBrain(new System.Random(5), Config());
            int late = 0;
            for (int i = 0; i < 400; i++)
            {
                var s = new CardRushRunnerState { Step = i % 40, Attempt = i / 40, MoveStartTick = 2000 + i * 1000 };
                FirstTap(brain, s, s.MoveStartTick);
                if (brain.LastPlanWasLateStart) late++;
            }
            //  0.15 × 400 = 60, 이항 분포 ±4σ(≈29)
            Assert.That(late, Is.InRange(31, 89));
        }

        [Test]
        public void 돌면_요구_색_창_가운데를_노린다()
        {
            var brain = new CardRushBotBrain(new System.Random(9), Config(miss: 0f));
            for (int step = 0; step < 40; step++)
            {
                var s = new CardRushRunnerState { Step = step, MoveStartTick = 2000, SpinStartTick = 2015 };
                long t = FirstTap(brain, s, 2016);
                Assert.IsTrue(CardRushCard.TryColorAt(s, t, course, out var c));
                Assert.AreEqual(course.RequiredColor(step), c, $"칸 {step} 틱 {t}");
            }
        }

        [Test]
        public void 실수_1이면_옆_색()
        {
            var brain = new CardRushBotBrain(new System.Random(1), Config(miss: 1f));
            for (int step = 0; step < 40; step++)
            {
                var s = new CardRushRunnerState { Step = step, MoveStartTick = 2000, SpinStartTick = 2015 };
                long t = FirstTap(brain, s, 2016);
                Assert.IsTrue(CardRushCard.TryColorAt(s, t, course, out var c));
                Assert.AreNotEqual(course.RequiredColor(step), c, $"칸 {step}");
            }
        }

        [Test]
        public void 던질_틱은_시작_뒤_마감_안()
        {
            foreach (float miss in new[] { 0f, 1f })
            {
                var brain = new CardRushBotBrain(new System.Random(1), Config(miss));
                for (int step = 0; step < 40; step++)
                {
                    var s = new CardRushRunnerState { Step = step, MoveStartTick = 2000, SpinStartTick = 2015 };
                    long t = FirstTap(brain, s, 2016);
                    Assert.Greater(t, 2015); Assert.LessOrEqual(t, CardRushCard.DeadlineTick(s));
                }
            }
        }

        [Test]
        public void 같은_난수면_같은_계획()
        {
            var a = new CardRushBotBrain(new System.Random(42), Config(0.06f));
            var b = new CardRushBotBrain(new System.Random(42), Config(0.06f));
            var s = new CardRushRunnerState { Step = 5, MoveStartTick = 2000 };
            Assert.AreEqual(FirstTap(a, s, 2000), FirstTap(b, s, 2000));
        }
    }
}
