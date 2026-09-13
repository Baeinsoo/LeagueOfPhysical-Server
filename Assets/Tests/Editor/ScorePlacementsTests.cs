using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    public class ScorePlacementsTests
    {
        [Test]
        public void 점수가_높은_사람이_앞이다()
        {
            var outcome = ScorePlacements.Resolve(new[] { ("a", 3, NoStats), ("b", 9, NoStats), ("c", 5, NoStats) });
            Assert.AreEqual(1, Placement(outcome, "b"));
            Assert.AreEqual(2, Placement(outcome, "c"));
            Assert.AreEqual(3, Placement(outcome, "a"));
        }

        [Test]
        public void 동점은_같은_등수이고_다음_등수를_건너뛴다()
        {
            var outcome = ScorePlacements.Resolve(new[] { ("a", 5, NoStats), ("b", 5, NoStats), ("c", 1, NoStats) });
            Assert.AreEqual(1, Placement(outcome, "a"));
            Assert.AreEqual(1, Placement(outcome, "b"));
            Assert.AreEqual(3, Placement(outcome, "c"));   // 2위는 없다 — 스포츠 표준
        }

        [Test]
        public void 아무도_못_맞히면_전원_공동_1등이다()
        {
            var outcome = ScorePlacements.Resolve(new[] { ("a", 0, NoStats), ("b", 0, NoStats) });
            Assert.AreEqual(1, Placement(outcome, "a"));
            Assert.AreEqual(1, Placement(outcome, "b"));
        }

        [Test]
        public void 사람이_없어도_죽지_않는다()
        {
            Assert.AreEqual(0, ScorePlacements.Resolve(new (string, int, Dictionary<string, int>)[0]).placements.Count);
        }

        [Test]
        public void 같은_점수끼리의_순서는_사람_id로_못박는다()
        {
            //  동점끼리도 목록에 담기는 차례가 매번 같아야 결과 화면이 흔들리지 않는다.
            var first = ScorePlacements.Resolve(new[] { ("b", 5, NoStats), ("a", 5, NoStats) });
            var second = ScorePlacements.Resolve(new[] { ("a", 5, NoStats), ("b", 5, NoStats) });
            Assert.AreEqual(first.placements[0].userId, second.placements[0].userId);
        }

        [Test]
        public void 자루는_등수_계산에_끼지_않고_같은_사람에게_그대로_붙는다()
        {
            var aStats = new Dictionary<string, int> { [ArcheryStatKeys.Score] = 9, [ArcheryStatKeys.Gained] = 9 };
            var bStats = new Dictionary<string, int> { [ArcheryStatKeys.Score] = 3, [ArcheryStatKeys.Gained] = 3 };

            var outcome = ScorePlacements.Resolve(new[] { ("a", 9, aStats), ("b", 3, bStats) });

            Assert.AreSame(aStats, StatsOf(outcome, "a"));
            Assert.AreSame(bStats, StatsOf(outcome, "b"));
            Assert.AreEqual(1, Placement(outcome, "a"));
            Assert.AreEqual(2, Placement(outcome, "b"));
        }

        [Test]
        public void 자루가_없어도_죽지_않는다()
        {
            var outcome = ScorePlacements.Resolve(new[] { ("a", 5, NoStats) });
            Assert.IsNull(StatsOf(outcome, "a"));
        }

        private static readonly Dictionary<string, int> NoStats = null;

        private static Dictionary<string, int> StatsOf(MatchOutcome outcome, string userId)
        {
            foreach (var p in outcome.placements)
            {
                if (p.userId == userId) { return p.stats; }
            }
            return null;
        }

        private static int Placement(MatchOutcome outcome, string userId)
        {
            foreach (var p in outcome.placements)
            {
                if (p.userId == userId) { return p.placement; }
            }
            return 0;
        }
    }
}
