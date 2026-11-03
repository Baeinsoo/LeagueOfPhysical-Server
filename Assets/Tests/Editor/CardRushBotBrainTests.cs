using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시 봇 두뇌(순수) — 반응 시간이 지난 뒤 요구 색 창의 가운데를 노리고, 가끔 틀린다.
    /// </summary>
    public class CardRushBotBrainTests
    {
        private static CardRushConfig Config(float missChance)
            => new CardRushConfig(matchDurationSeconds: 120f, moveSeconds: 0.3f, stunSeconds: 0.6f, minLanes: 4,
                                  botReactMinSeconds: 0.22f, botReactMaxSeconds: 0.4f, botTimingJitterSeconds: 0.04f,
                                  botMissChance: missChance);

        [Test]
        public void 같은_난수면_같은_계획()
        {
            var config = Config(0f);
            var course = new CardRushCourse(1, 40);
            var a = new CardRushBotBrain(new System.Random(5), config);
            var b = new CardRushBotBrain(new System.Random(5), config);
            var s = new CardRushRunnerState();

            a.ShouldGrab(s, 1000, 1000, course, config.Timing);
            b.ShouldGrab(s, 1000, 1000, course, config.Timing);

            Assert.AreEqual(a.PlannedTick, b.PlannedTick);
        }

        [Test]
        public void 반응_전에는_누르지_않는다()
        {
            var config = Config(0f);   // 실수가 섞이면 창 밖으로 더 밀릴 수 있어 여기선 뺀다.
            const long gameplayStart = 1000;
            int jitterTicks = (int)System.Math.Round(config.BotTimingJitterSeconds / CardRushTiming.TickInterval);

            for (int seed = 0; seed < 30; seed++)
            {
                var course = new CardRushCourse((ulong)seed, 40);

                //  두뇌 생성자가 가장 먼저 뽑는 난수와 똑같은 걸 똑같은 씨앗으로 미리 뽑아,
                //  이 두뇌의 실제 반응 틱을 안다(두뇌 안의 private 값이라 밖에서 다시 계산해야 한다).
                var rngForReact = new System.Random(seed);
                float react = config.BotReactMinSeconds
                    + (float)rngForReact.NextDouble() * (config.BotReactMaxSeconds - config.BotReactMinSeconds);
                int reactTicks = CardRushTiming.Ticks(react);

                var brain = new CardRushBotBrain(new System.Random(seed), config);
                var s = new CardRushRunnerState();

                brain.ShouldGrab(s, gameplayStart, gameplayStart, course, config.Timing);

                //  정의대로: 노리는 창의 가운데는 반응 시간이 지난 뒤여야 한다 — 흔들림만큼의 여유만 둔다.
                Assert.GreaterOrEqual(brain.PlannedTick, gameplayStart + reactTicks - jitterTicks,
                    $"seed={seed}");
            }
        }

        [Test]
        public void 실수_0이면_요구_색_창_안()
        {
            var config = Config(0f);
            const long gameplayStart = 1000;

            for (int seed = 0; seed < 50; seed++)
            {
                var course = new CardRushCourse((ulong)seed, 40);
                var brain = new CardRushBotBrain(new System.Random(seed), config);
                var s = new CardRushRunnerState();

                brain.ShouldGrab(s, gameplayStart, gameplayStart, course, config.Timing);

                bool shown = CardRushCard.TryColorAt(s, brain.PlannedTick, course, out var color);
                Assert.IsTrue(shown, $"seed={seed}");
                Assert.AreEqual(course.RequiredColor(s.Step), color, $"seed={seed}");
            }
        }

        [Test]
        public void 실수_1이면_옆_색()
        {
            var config = Config(1f);
            const long gameplayStart = 1000;

            for (int seed = 0; seed < 50; seed++)
            {
                var course = new CardRushCourse((ulong)seed, 40);
                var brain = new CardRushBotBrain(new System.Random(seed), config);
                var s = new CardRushRunnerState();

                brain.ShouldGrab(s, gameplayStart, gameplayStart, course, config.Timing);

                bool shown = CardRushCard.TryColorAt(s, brain.PlannedTick, course, out var color);
                Assert.IsTrue(shown, $"seed={seed}");
                Assert.AreNotEqual(course.RequiredColor(s.Step), color, $"seed={seed}");
            }
        }
    }
}
