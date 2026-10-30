using System.Collections.Generic;
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 연속 날갯짓 고비 코인(spec 2026-10-28 §2·§3)의 근거 — 서버가 새마다 연속이 고비를 넘은 횟수를 세어 판 끝 자루에 싣는다.
    /// 키 문자열은 로비 경제 표(combo_coins)와의 계약이라 여기서 고정한다.
    /// </summary>
    public class FlappyComboTallyTests
    {
        private static int[] Observe(params (int last, int now)[] steps)
        {
            var crossings = new int[FlappyComboStatKeys.Thresholds.Length];
            foreach (var (last, now) in steps)
            {
                FlappyComboTally.Observe(last, now, FlappyComboStatKeys.Thresholds, crossings);
            }
            return crossings;
        }

        private static Entity Bird(EntityRegistry registry, string id)
        {
            var e = new Entity(id);
            e.Add(new FlappyFlapCombo());
            e.Add(new FlappyStun());
            registry.Add(e);
            return e;
        }

        private static void ClimbTo(FlappyComboTallySystem tally, Entity bird, int from, int to, ref long tick)
        {
            for (int n = from; n <= to; n++)
            {
                bird.Get<FlappyFlapCombo>().Count = n;
                tally.Tick(++tick, 0.02f);
            }
        }

        [Test]
        public void 키_문자열은_로비_경제_표와_같다()
        {
            CollectionAssert.AreEqual(new[] { 10, 25, 50, 100 }, FlappyComboStatKeys.Thresholds);
            Assert.AreEqual("flapCombo10", FlappyComboStatKeys.For(10));
            Assert.AreEqual("flapCombo25", FlappyComboStatKeys.For(25));
            Assert.AreEqual("flapCombo50", FlappyComboStatKeys.For(50));
            Assert.AreEqual("flapCombo100", FlappyComboStatKeys.For(100));
        }

        [Test]
        public void 한_칸씩_오르면_고비마다_한_번씩()
        {
            var crossings = new int[4];
            for (int n = 1; n <= 57; n++)
            {
                FlappyComboTally.Observe(n - 1, n, FlappyComboStatKeys.Thresholds, crossings);
            }
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 0 }, crossings);
        }

        [Test]
        public void 같은_값이_이어지면_다시_안_센다()
        {
            CollectionAssert.AreEqual(new[] { 1, 0, 0, 0 }, Observe((9, 10), (10, 10), (10, 10)));
        }

        [Test]
        public void 끊긴_뒤_새_연속이_다시_넘으면_또_센다()
        {
            //  57까지 이어 박고(0), 다시 12까지 → 10은 두 번, 25·50은 한 번(spec §1의 예).
            CollectionAssert.AreEqual(new[] { 2, 1, 1, 0 }, Observe((0, 57), (57, 0), (0, 12)));
        }

        [Test]
        public void 한_번에_여러_칸_올라도_사이_고비를_다_센다()
        {
            CollectionAssert.AreEqual(new[] { 1, 1, 0, 0 }, Observe((0, 30)));
        }

        [Test]
        public void 줄어든_뒤_한_관측에서_다시_넘었으면_센다()
        {
            //  15 → (기절 → 다시 12) 를 한 번에 본 경우. 서버는 매 틱 보지만 거기 기대지 않는다.
            CollectionAssert.AreEqual(new[] { 1, 0, 0, 0 }, Observe((15, 12)));
        }

        [Test]
        public void 지켜본_새만_세고_사람별로_자루에_싣는다()
        {
            var registry = new EntityRegistry();
            var mine = Bird(registry, "e-a");
            var other = Bird(registry, "e-x");
            var tally = new FlappyComboTallySystem(registry);
            tally.Watch("e-a", "u-a");

            long tick = 0;
            for (int n = 1; n <= 26; n++)
            {
                mine.Get<FlappyFlapCombo>().Count = n;
                other.Get<FlappyFlapCombo>().Count = n;
                tally.Tick(++tick, 0.02f);
            }

            var outcome = new MatchOutcome();
            outcome.placements.Add(new MatchPlacement { userId = "u-a", placement = 1 });
            outcome.placements.Add(new MatchPlacement { userId = "u-x", placement = 2 });
            tally.AttachTo(outcome);

            var stats = outcome.placements[0].stats;
            Assert.AreEqual(6, stats.Count, "고비 넷 + 가장 긴 연속 + 기절 — 0도 싣는다(옛 서버와 갈리게)");
            Assert.AreEqual(1, stats["flapCombo10"]);
            Assert.AreEqual(1, stats["flapCombo25"]);
            Assert.AreEqual(0, stats["flapCombo50"]);
            Assert.AreEqual(0, stats["flapCombo100"]);
            Assert.AreEqual(26, stats["flapComboBest"]);
            Assert.AreEqual(0, stats["flapStuns"]);
            Assert.IsNull(outcome.placements[1].stats, "안 지켜본 사람의 줄은 건드리지 않는다");
        }

        [Test]
        public void 몸이_사라져도_센_것은_남는다()
        {
            var registry = new EntityRegistry();
            var bird = Bird(registry, "e-a");
            var tally = new FlappyComboTallySystem(registry);
            tally.Watch("e-a", "u-a");
            long tick = 0;
            ClimbTo(tally, bird, 1, 10, ref tick);

            registry.Remove("e-a");   //  추격자에게 잡혀 몸이 사라졌다
            tally.Tick(++tick, 0.02f);

            var outcome = new MatchOutcome();
            outcome.placements.Add(new MatchPlacement { userId = "u-a", placement = 2 });
            tally.AttachTo(outcome);
            Assert.AreEqual(1, outcome.placements[0].stats["flapCombo10"]);
        }

        [Test]
        public void 고비를_넘긴_그_틱에_잡혀도_센다()
        {
            //  배선 리뷰(batchB-task-review.md #1) — End 페이즈에서 FlappyComboTallySystem을 FlappyChaserSystem보다
            //  앞에 등록해야 한다(FlappyRaceLifetimeScope). 월드는 이미 끝난 틱이라 ComboTally가 추격자보다 먼저
            //  관찰해도 이번 틱 값은 확정돼 있고, 추격자가 그 뒤 같은 틱에 몸을 지워도 이미 센 고비는 남는다.
            //  여기서는 그 순서(먼저 Tick, 그 다음 같은 틱에 Despawn)를 그대로 흉내 낸다.
            var registry = new EntityRegistry();
            var bird = Bird(registry, "e-a");
            var tally = new FlappyComboTallySystem(registry);
            tally.Watch("e-a", "u-a");
            long tick = 0;
            ClimbTo(tally, bird, 1, 9, ref tick);   //  아직 10 미만

            bird.Get<FlappyFlapCombo>().Count = 10;   //  마지막 날갯짓으로 고비(10)를 넘는다
            tally.Tick(++tick, 0.02f);                //  ComboTally가 추격자보다 앞이라 이번 틱 값을 먼저 본다
            registry.Remove("e-a");                   //  같은 틱, 그 뒤 추격자가 몸을 지운다(End 페이즈 뒷부분)

            var outcome = new MatchOutcome();
            outcome.placements.Add(new MatchPlacement { userId = "u-a", placement = 2 });
            tally.AttachTo(outcome);
            Assert.AreEqual(1, outcome.placements[0].stats["flapCombo10"], "등록 순서가 지켜지면 잡힌 그 틱의 고비도 놓치지 않는다");
        }

        [Test]
        public void 기존_자루에_합친다()
        {
            var registry = new EntityRegistry();
            var bird = Bird(registry, "e-a");
            var tally = new FlappyComboTallySystem(registry);
            tally.Watch("e-a", "u-a");
            long tick = 0;
            ClimbTo(tally, bird, 1, 10, ref tick);

            var outcome = new MatchOutcome();
            outcome.placements.Add(new MatchPlacement { userId = "u-a", placement = 1, stats = new Dictionary<string, int> { [MatchStatKeys.Left] = 1 } });
            tally.AttachTo(outcome);

            Assert.AreEqual(1, outcome.placements[0].stats[MatchStatKeys.Left], "다른 키는 그대로");
            Assert.AreEqual(1, outcome.placements[0].stats["flapCombo10"]);
        }

        [Test]
        public void 리셋하면_지난_판을_잊는다()
        {
            var registry = new EntityRegistry();
            var bird = Bird(registry, "e-a");
            var tally = new FlappyComboTallySystem(registry);
            tally.Watch("e-a", "u-a");
            long tick = 0;
            ClimbTo(tally, bird, 1, 10, ref tick);

            tally.Reset();

            var outcome = new MatchOutcome();
            outcome.placements.Add(new MatchPlacement { userId = "u-a", placement = 1 });
            tally.AttachTo(outcome);
            Assert.IsNull(outcome.placements[0].stats);
        }

        //  퀘스트 조건(spec 2026-10-30 §4) — 로비 퀘스트 표의 clauses가 같은 문자열로 찾는다.
        [Test]
        public void 퀘스트_키_문자열은_로비_퀘스트_표와_같다()
        {
            Assert.AreEqual("flapComboBest", FlappyComboStatKeys.Best);
            Assert.AreEqual("flapStuns", FlappyComboStatKeys.Stuns);
            Assert.AreEqual("finished", RaceStatKeys.Finished);
        }

        [Test]
        public void 가장_긴_연속은_끊긴_뒤에도_최댓값이다()
        {
            var registry = new EntityRegistry();
            var bird = Bird(registry, "e-a");
            var tally = new FlappyComboTallySystem(registry);
            tally.Watch("e-a", "u-a");
            long tick = 0;
            ClimbTo(tally, bird, 1, 30, ref tick);
            bird.Get<FlappyFlapCombo>().Count = 0;
            tally.Tick(++tick, 0.02f);
            ClimbTo(tally, bird, 1, 12, ref tick);

            var outcome = new MatchOutcome();
            outcome.placements.Add(new MatchPlacement { userId = "u-a", placement = 1 });
            tally.AttachTo(outcome);
            Assert.AreEqual(30, outcome.placements[0].stats["flapComboBest"]);
        }

        [Test]
        public void 기절은_들어간_틱만_센다_연속이_0이어도()
        {
            var registry = new EntityRegistry();
            var bird = Bird(registry, "e-a");
            var tally = new FlappyComboTallySystem(registry);
            tally.Watch("e-a", "u-a");
            var stun = bird.Get<FlappyStun>();
            long tick = 0;

            //  날갯짓 없이(연속 0) 바로 기절 — 연속이 줄지 않아도 센다.
            stun.StunRemaining = 1.2f;
            for (int i = 0; i < 3; i++) tally.Tick(++tick, 0.02f);   //  기절 중 여러 틱은 한 번
            stun.StunRemaining = 0f;
            stun.InvulnRemaining = 0.5f;
            tally.Tick(++tick, 0.02f);                                 //  무적은 기절이 아니다
            stun.InvulnRemaining = 0f;
            stun.StunRemaining = 1.2f;
            tally.Tick(++tick, 0.02f);

            var outcome = new MatchOutcome();
            outcome.placements.Add(new MatchPlacement { userId = "u-a", placement = 1 });
            tally.AttachTo(outcome);
            Assert.AreEqual(2, outcome.placements[0].stats["flapStuns"]);
        }

        [Test]
        public void 몸이_사라져도_가장_긴_연속과_기절은_남는다()
        {
            var registry = new EntityRegistry();
            var bird = Bird(registry, "e-a");
            var tally = new FlappyComboTallySystem(registry);
            tally.Watch("e-a", "u-a");
            long tick = 0;
            ClimbTo(tally, bird, 1, 7, ref tick);
            bird.Get<FlappyStun>().StunRemaining = 1.2f;
            tally.Tick(++tick, 0.02f);
            registry.Remove("e-a");
            tally.Tick(++tick, 0.02f);

            var outcome = new MatchOutcome();
            outcome.placements.Add(new MatchPlacement { userId = "u-a", placement = 3 });
            tally.AttachTo(outcome);
            Assert.AreEqual(7, outcome.placements[0].stats["flapComboBest"]);
            Assert.AreEqual(1, outcome.placements[0].stats["flapStuns"]);
        }
    }
}
