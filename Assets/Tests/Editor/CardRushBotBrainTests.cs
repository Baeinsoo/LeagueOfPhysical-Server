using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 4차 봇 두뇌(spec §7) — 잡기·쓰기·버리기. 반응은 12틱 고정·흔들림 0·망설임 0으로 두고,
    /// 상태를 손으로 세워 "어떤 색을 잡나", "쥔 카드를 언제 쓰나", "안 맞는 카드를 언제 버리나"를 본다.
    /// </summary>
    public class CardRushBotBrainTests
    {
        private const long Start = 1000;
        private const int Lane = 0;
        private const ulong Seed = 7;
        private const int React = 12;   // 0.24초

        private static CardRushConfig Config(float hesitate = 0f, float freeDash = 0.5f, float reactMin = 0.24f, float reactMax = 0.24f, float extra = 0f)
            => new CardRushConfig(120f, 4, 140f, 11f, 19f, 0.35f, 28f, 48f, 2f, 1.39f, 1f, 2.77f, 1.5f,
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

        //  그 색을 쥔 상태 — 지금 바로 앞에 그 색을 잡았다(5초가 넉넉히 남게).
        private static CardRushRunnerState Holding(CardRushRunnerState s, CardColor color, long now, CardRushTrack track)
        {
            long lockTick = now - 1;
            //  거꾸로 찾는 데 끝을 둔다 — 못 찾으면 끝없이 돌지 않고 아래 단정에서 실패한다.
            while (lockTick > now - 3 * CardRushRules.CardDwellTicks && !(CardRushCard.CardAt(s, lockTick, Start, Lane, track, out var c) && c == color)) lockTick--;
            Assert.Greater(lockTick, now - 3 * CardRushRules.CardDwellTicks);
            s.LockTick = lockTick;
            Assert.AreEqual(color, CardAt(s, now, track));
            Assert.IsTrue(CardRushCard.CanUse(s, now, Start, Lane, track));
            return s;
        }

        //  계획을 세운 틱 + 반응 = 그 색이 보이는 틱이 되게 계획 틱을 고르고, 그 틱에 탭하나.
        private static bool TapsWhenShowing(CardRushBotBrain brain, CardRushRunnerState s, long from, CardColor color, CardRushTrack track, CardRushTuning tuning)
        {
            long show = FirstTickShowing(s, from + React, color, track);
            long plan = show - React;
            Assert.IsFalse(brain.ShouldTap(s, plan, Start, Lane, track, tuning), "반응 전");
            for (long t = plan + 1; t < show; t++)
                Assert.IsFalse(brain.ShouldTap(s, t, Start, Lane, track, tuning), $"반응 전 +{t - plan}");
            return brain.ShouldTap(s, show, Start, Lane, track, tuning);
        }

        private static readonly CardRushZone ZoneAhead = new CardRushZone(triggerMm: 10_000, fromMm: 11_000, toMm: 14_000, warnTicks: 60);

        [Test]
        public void 구역이_켜지면_파랑을_잡고_바로_쓴다()
        {
            var config = Config();
            var track = Track(zones: new[] { ZoneAhead });
            var probe = new CardRushRunnerState { RollStartTick = Start, PosMm = 10_500 };
            long t0 = FirstTickShowing(probe, 3000, CardColor.Blue, track) - React;
            var s = probe; s.ZoneTick = t0;

            var brain = Brain(config);
            long tap = FirstTap(brain, s, t0, t0 + 60, track, config.Tuning);
            Assert.AreEqual(t0 + React, tap, "반응이 끝나는 틱에 보이는 파랑을 잡는다");
            Assert.AreEqual(CardColor.Blue, CardAt(s, tap, track));
            Assert.AreEqual(CardRushBotThreat.Zone, brain.Plan.Threat);

            //  쥔 다음 틱에 바로 쓴다.
            var held = s; held.LockTick = tap;
            Assert.IsTrue(brain.ShouldTap(held, tap + 1, Start, Lane, track, config.Tuning));
        }

        [Test]
        public void 바리케이드는_빨강을_잡고_사거리_안에서만_쓴다()
        {
            var config = Config();
            var track = Track(new[] { new CardRushObstacle(CardRushObstacleKind.Barricade, 20_000, 0) });

            //  앞 8.2 m — 상대할 거리(8.3 m) 안이라 빨강을 잡는다.
            var far = new CardRushRunnerState { RollStartTick = Start, PosMm = 11_800 };
            var brain = Brain(config);
            Assert.IsTrue(TapsWhenShowing(brain, far, 3000, CardColor.Red, track, config.Tuning));
            Assert.AreEqual(CardRushBotThreat.Barricade, brain.Plan.Threat);

            //  쥐었지만 사거리 − 0.5 m(7.5 m) 밖 — 안 쓴다. 7.4 m면 쓴다.
            var brain2 = Brain(config);
            var heldFar = Holding(far, CardColor.Red, 4000, track);
            Assert.AreEqual(-1L, FirstTap(brain2, heldFar, 4000, 4060, track, config.Tuning), "앞면 − 손 7.9 m");
            var near = new CardRushRunnerState { RollStartTick = Start, PosMm = 20_000 - CardRushRules.ShotStartAheadMm - 7_400 };
            Assert.IsTrue(brain2.ShouldTap(Holding(near, CardColor.Red, 4100, track), 4100, Start, Lane, track, config.Tuning), "앞면 − 손 7.4 m");
        }

        //  굴러오는 것이 이 틱에 막 풀렸고 닿기까지 n틱 남은 상태(서로 74 mm/틱씩 다가온다).
        private static CardRushRunnerState RollerIn(int ticksToContact, long tick)
            => new CardRushRunnerState
            {
                PosMm = 30_000 - CardRushRules.RollerContactMm - (CardRushRules.RunMmPerTick + CardRushRules.RollerMmPerTick) * ticksToContact,
                ObstacleTick = tick,
            };

        [Test]
        public void 굴러오는_것은_빨강을_먼저_노리고_닿기_직전에만_노랑()
        {
            var config = Config();
            var track = Track(new[] { new CardRushObstacle(CardRushObstacleKind.Roller, 30_000, 0) });

            //  닿기까지 60틱(> 두 장 46틱) — 노랑이 보여도 안 잡고, 빨강은 잡는다.
            long ty = FirstTickShowing(new CardRushRunnerState { RollStartTick = Start }, 3000 + React, CardColor.Yellow, track);
            var brain = Brain(config);
            Assert.IsFalse(TapsWhenShowing(brain, RollerIn(60, ty - React), ty - React, CardColor.Yellow, track, config.Tuning), "멀면 노랑을 안 잡는다");
            Assert.AreEqual(CardColor.Red, brain.Plan.Want);
            Assert.AreEqual(CardColor.Yellow, brain.Plan.Alt);
            long tr = FirstTickShowing(new CardRushRunnerState { RollStartTick = Start }, 4000 + React, CardColor.Red, track);
            Assert.IsTrue(TapsWhenShowing(Brain(config), RollerIn(60, tr - React), tr - React, CardColor.Red, track, config.Tuning), "빨강은 잡는다");

            //  닿기까지 30틱 — 노랑을 잡는다.
            Assert.IsTrue(TapsWhenShowing(Brain(config), RollerIn(30, ty - React), ty - React, CardColor.Yellow, track, config.Tuning), "가까우면 노랑");

            //  쥔 노랑은 닿기까지 20틱(점프 절반) 안에서만 쓴다 — 공중에 있는 동안 닿아야 넘는다.
            const long T = 5000;
            Assert.IsFalse(Brain(config).ShouldTap(Holding(RollerIn(21, T), CardColor.Yellow, T, track), T, Start, Lane, track, config.Tuning), "21틱");
            Assert.IsTrue(Brain(config).ShouldTap(Holding(RollerIn(20, T), CardColor.Yellow, T, track), T, Start, Lane, track, config.Tuning), "20틱");
            //  쥔 빨강은 사거리 안이면 바로 던진다.
            Assert.IsTrue(Brain(config).ShouldTap(Holding(RollerIn(60, T), CardColor.Red, T, track), T, Start, Lane, track, config.Tuning));
        }

        [Test]
        public void 쥔_노랑으로_굴러오는_것을_넘는다()
        {
            var config = Config();
            var track = Track(new[] { new CardRushObstacle(CardRushObstacleKind.Roller, 30_000, 0) });
            var brain = Brain(config);
            const long T = 5000;
            var s = Holding(RollerIn(60, T), CardColor.Yellow, T, track);
            for (long t = T; t < T + 200 && s.ObstacleIndex == 0; t++)
                CardRushRunnerSim.Step(ref s, t, brain.ShouldTap(s, t, Start, Lane, track, config.Tuning), Start, Lane, track, config.Tuning);

            Assert.AreEqual(1, s.ObstacleIndex);
            Assert.AreEqual((int)CardRushObstacleEvent.Jumped, s.ObstacleEvent, "공중에서 닿아 넘는다");
            Assert.AreEqual(0, s.Hits);
        }

        [Test]
        public void 구덩이는_노랑을_잡고_뛰는_창_가운데서_쓴다()
        {
            var config = Config();
            var pit = new CardRushObstacle(CardRushObstacleKind.Pit, 20_000, 0, 1_200);
            var track = Track(new[] { pit });
            int center = CardRushBotBrain.PitJumpCenter(pit);
            Assert.AreEqual(20_000 - 600, center, "(앞 가장자리 + 폭 − 2.4 m, 앞 가장자리)의 가운데");

            var brain = Brain(config);
            Assert.IsTrue(TapsWhenShowing(brain, new CardRushRunnerState { RollStartTick = Start, PosMm = 15_000 }, 3000, CardColor.Yellow, track, config.Tuning));
            Assert.AreEqual(CardRushBotThreat.Pit, brain.Plan.Threat);

            const long T = 5000;
            Assert.IsFalse(Brain(config).ShouldTap(Holding(new CardRushRunnerState { RollStartTick = Start, PosMm = center - 1 }, CardColor.Yellow, T, track), T, Start, Lane, track, config.Tuning));
            Assert.IsTrue(Brain(config).ShouldTap(Holding(new CardRushRunnerState { RollStartTick = Start, PosMm = center }, CardColor.Yellow, T, track), T, Start, Lane, track, config.Tuning));

            //  실제로 넘는다 — 노랑을 쥔 채 달려와 창 가운데서 뛰면 Jumped, 빠지지 않는다.
            var s = Holding(new CardRushRunnerState { RollStartTick = Start, PosMm = 15_000 }, CardColor.Yellow, T, track);
            var b = Brain(config);
            for (long t = T; t < T + 300 && s.ObstacleIndex == 0; t++)
                CardRushRunnerSim.Step(ref s, t, b.ShouldTap(s, t, Start, Lane, track, config.Tuning), Start, Lane, track, config.Tuning);
            Assert.AreEqual(1, s.ObstacleIndex);
            Assert.AreEqual((int)CardRushObstacleEvent.Jumped, s.ObstacleEvent);
            Assert.AreEqual(0, s.Hits);
        }

        [Test]
        public void 안_맞는_카드는_해가_없을_때_써서_비운다()
        {
            var config = Config();
            const long T = 5000;
            bool Taps(CardRushRunnerState s, CardColor held, CardRushTrack track)
                => Brain(config).ShouldTap(Holding(s, held, T, track), T, Start, Lane, track, config.Tuning);

            //  켜진 구역에 빨강 — 헛방은 해가 없으니 바로 버린다.
            var zone = Track(zones: new[] { ZoneAhead });
            Assert.IsTrue(Taps(new CardRushRunnerState { RollStartTick = Start, PosMm = 10_500, ZoneTick = T - 5 }, CardColor.Red, zone), "빨강은 아무 때나");

            //  굴러오는 것이 닿기 30틱 앞인데 파랑 — 대시하면 들이받는다. 사라지길 기다린다.
            var roller = Track(new[] { new CardRushObstacle(CardRushObstacleKind.Roller, 30_000, 0) });
            Assert.IsFalse(Taps(RollerIn(30, T), CardColor.Blue, roller), "굴러오는 것 앞 파랑");

            //  바리케이드 앞 파랑 — 벽에 일찍 닿을 뿐이라 버린다.
            var barricade = Track(new[] { new CardRushObstacle(CardRushObstacleKind.Barricade, 20_000, 0) });
            Assert.IsTrue(Taps(new CardRushRunnerState { RollStartTick = Start, PosMm = 18_000 }, CardColor.Blue, barricade), "바리케이드 앞 파랑");

            //  바리케이드 1 m 안 노랑은 참고, 3 m 앞이면 뛰어 비운다.
            Assert.IsFalse(Taps(new CardRushRunnerState { RollStartTick = Start, PosMm = 19_200 }, CardColor.Yellow, barricade), "바리케이드 0.8 m 앞 노랑");
            Assert.IsTrue(Taps(new CardRushRunnerState { RollStartTick = Start, PosMm = 17_000 }, CardColor.Yellow, barricade), "바리케이드 3 m 앞 노랑");

            //  빈 길의 파랑은 쓰지만(빈 길 대시), 곧 켜질 구역 앞이면 쥐고 기다린다.
            Assert.IsTrue(Taps(new CardRushRunnerState { RollStartTick = Start, PosMm = 1_000 }, CardColor.Blue, zone), "빈 길 파랑");
            Assert.IsFalse(Taps(new CardRushRunnerState { RollStartTick = Start, PosMm = 9_000 }, CardColor.Blue, zone), "구역 2 m 앞 파랑");
        }

        [Test]
        public void 반응_전엔_안_잡는다()
        {
            var config = Config();
            var track = Track(zones: new[] { ZoneAhead });
            //  파랑이 계획 틱부터 23틱 내내 보인다. 그래도 반응 12틱 전엔 안 잡는다.
            var probe = new CardRushRunnerState { RollStartTick = Start, PosMm = 10_500 };
            long t0 = WindowStart(probe, 3000, CardColor.Blue, track);
            var s = probe; s.ZoneTick = t0;

            var brain = Brain(config);
            for (long t = t0; t < t0 + React; t++)
            {
                Assert.AreEqual(CardColor.Blue, CardAt(s, t, track));
                Assert.IsFalse(brain.ShouldTap(s, t, Start, Lane, track, config.Tuning), $"틱 +{t - t0}");
            }
            Assert.IsTrue(brain.ShouldTap(s, t0 + React, Start, Lane, track, config.Tuning));
        }

        [Test]
        public void 망설임이_1이면_원하는_카드에도_안_잡는다()
        {
            var config = Config(hesitate: 1f);
            var track = Track(zones: new[] { ZoneAhead });
            var s = new CardRushRunnerState { RollStartTick = Start, PosMm = 10_500, ZoneTick = 3000 };

            var brain = Brain(config);
            Assert.AreEqual(-1L, FirstTap(brain, s, 3000, 3300, track, config.Tuning));
            Assert.AreEqual(CardColor.Blue, brain.Plan.Want);

            //  양성 대조: 망설임 0이면 같은 상태에서 잡는다.
            Assert.AreNotEqual(-1L, FirstTap(Brain(Config()), s, 3000, 3300, track, config.Tuning));
        }

        [Test]
        public void 앞이_비면_대시_확률대로()
        {
            var config = Config(freeDash: 0.5f);
            var empty = Track();
            var brain = Brain(config, seed: 11);
            var s = new CardRushRunnerState { RollStartTick = Start, PosMm = 20_000 };
            int dash = 0;
            //  계획은 40틱마다 끝난다 — 50틱 간격으로 물으면 매번 새 계획(반응 20틱이라 그 틱엔 안 잡는다).
            for (int i = 0; i < 400; i++)
            {
                long t = 3000 + i * 50L;
                Assert.IsFalse(brain.ShouldTap(s, t, Start, Lane, empty, config.Tuning));
                Assert.AreEqual(CardRushBotThreat.None, brain.Plan.Threat);
                Assert.AreEqual(t + CardRushBotBrain.FreeReactTicks, brain.Plan.ReactTick);
                if (brain.Plan.Want == CardColor.Blue) dash++;
                else Assert.IsNull(brain.Plan.Want);
            }
            //  0.5 × 400 = 200, 이항 σ = 10 → ±4σ
            Assert.That(dash, Is.InRange(160, 240));

            //  다음 구역 시작이 대시 + 1000(4120) 안 — 비지 않았다.
            var zoneSoon = Track(zones: new[] { new CardRushZone(22_000, 20_000 + 4000, 28_000, 60) });
            var b = Brain(Config(freeDash: 1f));
            for (int i = 0; i < 20; i++)
            {
                b.ShouldTap(s, 3000 + i * 50L, Start, Lane, zoneSoon, config.Tuning);
                Assert.IsTrue(b.Plan.Active);
                Assert.IsNull(b.Plan.Want, "앞이 안 비면 대시 계획이 없다");
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
                var s = new CardRushRunnerState { RollStartTick = Start };
                var taps = new List<long>();
                for (long t = Start; t < Start + 6000 && s.FinishTick == 0; t++)
                {
                    bool tap = brain.ShouldTap(s, t, Start, Lane, track, config.Tuning);
                    if (tap) taps.Add(t);
                    CardRushRunnerSim.Step(ref s, t, tap, Start, Lane, track, config.Tuning);
                }
                Assert.AreNotEqual(0L, s.FinishTick, "판을 끝까지 돈다");
                return taps;
            }

            var a = Play(42);
            var b = Play(42);
            Assert.Greater(a.Count, 10, "판 중에 여러 번 잡고 쓴다");
            CollectionAssert.AreEqual(a, b);
            //  양성 대조: 난수가 다르면 반응·흔들림이 달라 탭 열도 달라진다(난수를 안 쓰면 위 단정은 공짜로 통과한다).
            CollectionAssert.AreNotEqual(a, Play(43), "다른 난수면 다른 탭 열");
        }

        //  4.1차: 평소엔 카드가 멈춰 있다 — 봇은 시작할 수 있게 된 뒤 반응 틱만큼 지나 돌린다(돌리는 데 손해가 없다).
        [Test]
        public void 평소엔_반응_틱_뒤_카드를_돌린다()
        {
            var config = Config();
            var track = Track(zones: new[] { ZoneAhead });
            var brain = Brain(config);
            var idle = new CardRushRunnerState { PosMm = 1_000, SpinStartTick = 3000, UseCount = 1 };
            Assert.IsFalse(brain.ShouldTap(idle, 2999, Start, Lane, track, config.Tuning), "쿨다운 중엔 안 돌린다");
            Assert.AreEqual(3000 + React, FirstTap(brain, idle, 3000, 3100, track, config.Tuning), "시작할 수 있게 된 틱 + 반응");
            var rolled = idle; rolled.RollStartTick = 3000 + React;
            Assert.IsFalse(CardRushCard.CanStart(rolled, 3000 + React + 1, Start), "돌린 뒤엔 시작이 아니라 고르기");
        }

        [Test]
        public void 쓸_수_없으면_생각하지_않는다()
        {
            var config = Config();
            var track = Track(zones: new[] { ZoneAhead });
            var tuning = config.Tuning;

            //  계획이 없을 때: 출발 전·쿨다운·기절·완주엔 계획도 세우지 않는다.
            var brain = Brain(config);
            var inside = new CardRushRunnerState { RollStartTick = Start, PosMm = 10_500, ZoneTick = 3000 };
            var cooling = inside; cooling.SpinStartTick = 3010; cooling.RollStartTick = 0;
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

            //  공중에서 파랑을 쥐고 있으면 쓸 수 없다 — 생각도 안 한다.
            var airborne = Holding(inside, CardColor.Blue, 3100, track);
            airborne.JumpStartTick = 3090;
            Assert.IsFalse(brain.ShouldTap(airborne, 3100, Start, Lane, track, tuning), "공중 파랑");
            Assert.IsFalse(brain.Plan.Active);

            //  계획이 있을 때: 그 계획이 지난 뒤라도 못 쓰는 동안엔 바꾸지 않는다.
            brain.ShouldTap(inside, 3000, Start, Lane, track, tuning);
            var before = brain.Plan;
            Assert.IsTrue(before.Active);
            var lateStun = inside; lateStun.StunEndTick = 3200;
            for (long t = 3001; t < 3200; t++)
                Assert.IsFalse(brain.ShouldTap(lateStun, t, Start, Lane, track, tuning));
            Assert.AreEqual(before.ReactTick, brain.Plan.ReactTick);
            Assert.AreEqual(before.Want, brain.Plan.Want);
        }
    }
}
