using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    public class DodgePlacementsTests
    {
        static int RankOf(MatchOutcome o, string user) => o.placements.Find(p => p.userId == user).placement;

        [Test]
        public void 마지막까지_남은_사람이_1등이고_늦게_탈락할수록_앞선다()
        {
            var o = DodgePlacements.Resolve(new[] { "a" }, new List<(string, long)> { ("b", 100), ("c", 200) });
            Assert.AreEqual(1, RankOf(o, "a"));
            Assert.AreEqual(2, RankOf(o, "c"));
            Assert.AreEqual(3, RankOf(o, "b"));
        }

        [Test]
        public void 같은_틱에_탈락하면_공동_순위고_다음은_건너뛴다()
        {
            var o = DodgePlacements.Resolve(new[] { "a" },
                new List<(string, long)> { ("x", 50), ("b", 100), ("c", 100) });
            Assert.AreEqual(2, RankOf(o, "b"));
            Assert.AreEqual(2, RankOf(o, "c"));
            Assert.AreEqual(4, RankOf(o, "x"));
        }

        [Test]
        public void 전원이_같은_틱에_탈락하면_모두_1등이다()
        {
            var o = DodgePlacements.Resolve(new string[0], new List<(string, long)> { ("a", 7), ("b", 7) });
            Assert.AreEqual(1, RankOf(o, "a"));
            Assert.AreEqual(1, RankOf(o, "b"));
        }
    }
}
