using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 3차 봇 두뇌(spec §8, 시안 botThink 이식). 반응은 12틱 고정·흔들림 0·망설임 0으로 두고,
    /// 카드가 원하는 색을 보이는 틱을 골라 계획을 세워 "어떤 색을 언제 쓰나"를 본다.
    /// </summary>
    public class CardRushBotBrainTests
    {
        private const long Start = 1000;
        private const int Lane = 0;
        private const ulong Seed = 7;
        private const int React = 12;   // 0.24초

        private static CardRushConfig Config(float hesitate = 0f, float freeDash = 0.5f, float reactMin = 0.24f, float reactMax = 0.24f, float extra = 0f)
            => new CardRushConfig(120f, 4, 140f, 11f, 19f, 0.5f, 28f, 48f, 1.2f, 1.39f, 0.7f, 2.77f, 1f,
                                  reactMin, reactMax, extra, hesitate, freeDash, 0.35f, 0.8f);

        private static CardRushTrack Track(CardRushObstacle[] obstacles = null, CardRushZone[] zones = null)
            => CardRushTrack.ForTests(140_000, obstacles ?? new CardRushObstacle[0], zones ?? new CardRushZone[0], Seed);

        private static CardRushBotBrain Brain(CardRushConfig config, int seed = 1) => new CardRushBotBrain(new System.Random(seed), config);

        //  from 이후 그 색이 처음 보이는 틱(그 장의 첫 틱이 아닐 수도 있다).
        private static long FirstTickShowing(CardRushRunnerState s, long from, CardColor color, CardRushTrack track)
        {
            for (long t = from; t < from + 200; t++)
                if (CardRushCard.CardAt(s, t, Start, Lane, track, out var c) && c == color) return t;
            Assert.Fail($"{color}이 안 보인다");
            return -1;
        }

        //  그 장이 막 뜬 틱 — 앞 틱엔 다른 색.
        private static long WindowStart(CardRushRunnerState s, long from, CardColor color, CardRushTrack track)
        {
            long t = FirstTickShowing(s, from, color, track);
            while (CardRushCard.CardAt(s, t - 1, Start, Lane, track, out var c) && c == color) t--;
            if (t < from) t += 3 * CardRushRules.CardDwellTicks;
            return t;
        }

        private static CardColor CardAt(CardRushRunnerState s, long tick, CardRushTrack track)
        {
            Assert.IsTrue(CardRushCard.CardAt(s, tick, Start, Lane, track, out var c));
            return c;
        }

        //  상태를 고정한 채 틱을 넘기며 첫 탭 틱. 없으면 −1.
        private static long FirstTap(CardRushBotBrain brain, CardRushRunnerState s, long from, long to, CardRushTrack track, CardRushTuning tuning)
        {
            for (long t = from; t <= to; t++)
                if (brain.ShouldTap(s, t, Start, Lane, track, tuning)) return t;
            return -1;
        }

        private static readonly CardRushZone ZoneAhead = new CardRushZone(triggerMm: 10_000, fromMm: 11_000, toMm: 14_000, warnTicks: 60);

        [Test]
        public void 구역_안에서_끝이_가까우면_파랑을_쓴다()
        {
            var config = Config();
            var track = Track(zones: new[] { ZoneAhead });
            //  끝까지 2000 mm < 대시 3120 × 0.85 — 빠져나간다. 반응이 끝나는 틱에 파랑이 보이게 계획 틱을 고른다.
            var probe = new CardRushRunnerState { PosMm = 12_000 };
            long t0 = FirstTickShowing(probe, 3000, CardColor.Blue, track) - React;
            var s = probe; s.ZoneTick = t0;

            var brain = Brain(config);
            long tap = FirstTap(brain, s, t0, t0 + 60, track, config.Tuning);

            Assert.AreEqual(t0 + React, tap, "반응이 끝나는 틱에 보이는 파랑을 쓴다");
            Assert.AreEqual(CardColor.Blue, CardAt(s, tap, track));
        }

        [Test]
        public void 구역_안에서_끝이_멀면_노랑을_쓴다()
        {
            var config = Config();
            var track = Track(zones: new[] { ZoneAhead });
            //  끝까지 3000 mm ≥ 2652 — 버틴다.
            var probe = new CardRushRunnerState { PosMm = 11_000 };
            long t0 = FirstTickShowing(probe, 3000, CardColor.Blue, track) - React;
            var s = probe; s.ZoneTick = t0;

            var brain = Brain(config);
            long tap = FirstTap(brain, s, t0, t0 + 60, track, config.Tuning);

            Assert.Greater(tap, t0 + React, "보이는 파랑은 지나친다");
            Assert.AreEqual(CardColor.Yellow, CardAt(s, tap, track));
        }

        [Test]
        public void 바리케이드는_손이_닿을_때만_빨강()
        {
            var config = Config();
            var track = Track(new[] { new CardRushObstacle(CardRushObstacleKind.Barricade, 20_000, 0) });

            //  앞 3000 mm — 상대는 하지만(빨강 계획) 손이 안 닿아 몇 바퀴를 돌아도 안 쓴다.
            var far = new CardRushRunnerState { PosMm = 17_000 };
            var brain = Brain(config);
            Assert.AreEqual(-1L, FirstTap(brain, far, 3000, 3300, track, config.Tuning), "손이 안 닿으면 빨강도 안 쓴다");
            Assert.AreEqual(CardColor.Red, brain.Plan.Want);

            //  막혀 서 있다(앞 490 mm) — 반응 뒤 보이는 빨강을 쓴다.
            var near = new CardRushRunnerState { PosMm = 20_000 - CardRushRules.BarricadeStopMm };
            long t0 = FirstTickShowing(near, 4000, CardColor.Red, track) - React;
            near.ObstacleTick = t0;
            var brain2 = Brain(config);
            long tap = FirstTap(brain2, near, t0, t0 + 100, track, config.Tuning);
            Assert.AreEqual(t0 + React, tap);
            Assert.AreEqual(CardColor.Red, CardAt(near, tap, track));
        }

        [Test]
        public void 굴러오는_것은_빨강을_노리고_마감이_가까우면_노랑()
        {
            var config = Config();
            var track = Track(new[] { new CardRushObstacle(CardRushObstacleKind.Roller, 20_000, 0) });

            //  손이 닿는 거리(1500 mm)에서 굴러온다 — 빨강이 보이면 부순다.
            var near = new CardRushRunnerState { PosMm = 18_500 };
            long t0 = FirstTickShowing(near, 3000, CardColor.Red, track) - React;
            near.ObstacleTick = t0;
            var brain = Brain(config);
            long tap = FirstTap(brain, near, t0, t0 + 20, track, config.Tuning);
            Assert.AreEqual(t0 + React, tap);
            Assert.AreEqual(CardColor.Red, CardAt(near, tap, track));

            //  4000 mm 앞에서 굴러온다(상태 고정이라 손은 계획이 끝날 때까지 안 닿는다): 만남 54틱, 마감 50틱.
            //  빨강은 반응 직후 보여도 손이 안 닿아 넘기고, 노랑이 15틱째부터 보여도 마감까지 한 장 × 1.2(27.6틱)가
            //  남기 전엔 참는다 → 23틱째에 노랑.
            var far = new CardRushRunnerState { PosMm = 16_000 };
            long t1 = WindowStart(far, 5000, CardColor.Yellow, track) - 15;
            far.ObstacleTick = t1;
            var brain2 = Brain(config);
            Assert.IsFalse(brain2.ShouldTap(far, t1, Start, Lane, track, config.Tuning));
            Assert.AreEqual(CardColor.Red, brain2.Plan.Want);
            Assert.AreEqual(CardColor.Yellow, brain2.Plan.Alt);
            Assert.AreEqual(t1 + 50, brain2.Plan.Deadline);
            long tap2 = FirstTap(brain2, far, t1 + 1, t1 + 54, track, config.Tuning);
            Assert.AreEqual(t1 + 23, tap2, "대안은 마감이 가까울 때만");
            Assert.AreEqual(CardColor.Yellow, CardAt(far, tap2, track));
        }

        [Test]
        public void 반응_전엔_안_누른다()
        {
            var config = Config();
            var track = Track(zones: new[] { ZoneAhead });
            //  버티는 계획(노랑) — 노랑이 계획 틱부터 23틱 내내 보인다. 그래도 반응 12틱 전엔 안 누른다.
            var probe = new CardRushRunnerState { PosMm = 11_000 };
            long t0 = WindowStart(probe, 3000, CardColor.Yellow, track);
            var s = probe; s.ZoneTick = t0;

            var brain = Brain(config);
            for (long t = t0; t < t0 + React; t++)
            {
                Assert.AreEqual(CardColor.Yellow, CardAt(s, t, track));
                Assert.IsFalse(brain.ShouldTap(s, t, Start, Lane, track, config.Tuning), $"틱 +{t - t0}");
            }
            Assert.IsTrue(brain.ShouldTap(s, t0 + React, Start, Lane, track, config.Tuning));
        }

        [Test]
        public void 망설임이_1이면_원하는_카드에도_안_누른다()
        {
            var config = Config(hesitate: 1f);
            var track = Track(zones: new[] { ZoneAhead });
            var s = new CardRushRunnerState { PosMm = 11_000, ZoneTick = 3000 };

            var brain = Brain(config);
            Assert.AreEqual(-1L, FirstTap(brain, s, 3000, 3300, track, config.Tuning));
            Assert.AreEqual(CardColor.Yellow, brain.Plan.Want);
        }

        [Test]
        public void 앞이_비면_대시_확률대로()
        {
            var config = Config(freeDash: 0.5f);
            var empty = Track();
            var brain = Brain(config, seed: 11);
            var s = new CardRushRunnerState { PosMm = 20_000 };
            int dash = 0;
            //  계획은 40틱마다 끝난다 — 50틱 간격으로 물으면 매번 새 계획(반응 20틱이라 그 틱엔 안 누른다).
            for (int i = 0; i < 400; i++)
            {
                long t = 3000 + i * 50L;
                Assert.IsFalse(brain.ShouldTap(s, t, Start, Lane, empty, config.Tuning));
                Assert.AreEqual(t + CardRushBotBrain.FreeReactTicks, brain.Plan.ReactTick);
                if (brain.Plan.Want == CardColor.Blue) dash++;
                else Assert.IsNull(brain.Plan.Want);
            }
            //  0.5 × 400 = 200, 이항 σ = 10 → ±4σ
            Assert.That(dash, Is.InRange(160, 240));

            //  앞 장애물이 상대할 거리(4160) 밖이지만 대시 + 1400(4520) 안 — 비지 않았다.
            var blocked = Track(new[] { new CardRushObstacle(CardRushObstacleKind.Barricade, 20_000 + 4300, 0) });
            //  다음 구역 시작이 대시 + 1000(4120) 안 — 비지 않았다.
            var zoneSoon = Track(zones: new[] { new CardRushZone(22_000, 20_000 + 4000, 28_000, 60) });
            foreach (var track in new[] { blocked, zoneSoon })
            {
                var b = Brain(Config(freeDash: 1f));
                for (int i = 0; i < 20; i++)
                {
                    b.ShouldTap(s, 3000 + i * 50L, Start, Lane, track, config.Tuning);
                    Assert.IsTrue(b.Plan.Active);
                    Assert.IsNull(b.Plan.Want, "앞이 안 비면 대시 계획이 없다");
                }
            }
        }

        [Test]
        public void 같은_난수면_같은_탭_열()
        {
            var config = Config(reactMin: 0.22f, reactMax: 0.40f, extra: 0.2f, hesitate: 0.02f);
            var track = new CardRushTrack(99, config.Tuning);

            List<long> Play(int seed)
            {
                var brain = Brain(config, seed);
                var s = new CardRushRunnerState();
                var taps = new List<long>();
                for (long t = Start; t < Start + 3000 && s.FinishTick == 0; t++)
                {
                    bool tap = brain.ShouldTap(s, t, Start, Lane, track, config.Tuning);
                    if (tap) taps.Add(t);
                    CardRushRunnerSim.Step(ref s, t, tap, Start, Lane, track, config.Tuning);
                }
                return taps;
            }

            var a = Play(42);
            var b = Play(42);
            Assert.Greater(a.Count, 10, "판 중에 여러 번 쓴다");
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void 쓸_수_없으면_생각하지_않는다()
        {
            var config = Config();
            var track = Track(zones: new[] { ZoneAhead });
            var tuning = config.Tuning;

            //  계획이 없을 때: 출발 전·쿨다운·기절·완주엔 계획도 세우지 않는다.
            var brain = Brain(config);
            var inside = new CardRushRunnerState { PosMm = 11_000, ZoneTick = 3000 };
            var cooling = inside; cooling.SpinStartTick = 3010;
            var stunned = inside; stunned.StunEndTick = 3010;
            var finished = inside; finished.FinishTick = 2900;
            for (long t = 3000; t < 3010; t++)
            {
                Assert.IsFalse(brain.ShouldTap(inside, Start - 1, Start, Lane, track, tuning), "출발 전");
                Assert.IsFalse(brain.ShouldTap(cooling, t, Start, Lane, track, tuning), "쿨다운");
                Assert.IsFalse(brain.ShouldTap(stunned, t, Start, Lane, track, tuning), "기절");
                Assert.IsFalse(brain.ShouldTap(finished, t, Start, Lane, track, tuning), "완주");
            }
            Assert.IsFalse(brain.Plan.Active, "못 쓰는 틱엔 계획을 세우지 않는다");

            //  계획이 있을 때: 그 계획이 지난 뒤라도 못 쓰는 동안엔 바꾸지 않는다.
            brain.ShouldTap(inside, 3000, Start, Lane, track, tuning);
            var before = brain.Plan;
            Assert.IsTrue(before.Active);
            var lateStun = inside; lateStun.StunEndTick = 3200;
            for (long t = 3001; t < 3200; t++)
                Assert.IsFalse(brain.ShouldTap(lateStun, t, Start, Lane, track, tuning));
            Assert.AreEqual(before.Until, brain.Plan.Until);
            Assert.AreEqual(before.ReactTick, brain.Plan.ReactTick);
            Assert.AreEqual(before.Want, brain.Plan.Want);
        }
    }
}
