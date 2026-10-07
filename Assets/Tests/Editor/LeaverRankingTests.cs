using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>끝까지 안 돌아온 사람은 꼴찌 — 남은 사람 등수는 규칙 그대로(동점 유지), 나간 사람은 그 아래 늦게 나간 순.</summary>
    public class LeaverRankingTests
    {
        static MatchOutcome Outcome(params (string user, int place)[] rows)
        {
            var o = new MatchOutcome();
            foreach (var (user, place) in rows)
            {
                o.placements.Add(new MatchPlacement { userId = user, placement = place, stats = new Dictionary<string, int> { ["score"] = place * 10 } });
            }
            return o;
        }

        static string Shape(MatchOutcome o) => string.Join(" ", o.placements.Select(p => $"{p.userId}{p.placement}"));

        static readonly string[] None = new string[0];

        [Test]
        public void 나간_사람이_없으면_그대로()
        {
            var o = LeaverRanking.Apply(Outcome(("a", 1), ("b", 2)), None, None);
            Assert.AreEqual("a1 b2", Shape(o));
        }

        [Test]
        public void 일등이_나가면_아래로_나머지는_당겨진다()
        {
            var o = LeaverRanking.Apply(Outcome(("a", 1), ("b", 2), ("c", 3)), new[] { "a" }, None);
            Assert.AreEqual("b1 c2 a3", Shape(o));
        }

        [Test]
        public void 동점_그룹에서_하나_빠지면_다시_매긴다()
        {
            var o = LeaverRanking.Apply(Outcome(("a", 1), ("b", 1), ("c", 3)), new[] { "a" }, None);
            Assert.AreEqual("b1 c2 a3", Shape(o));
        }

        [Test]
        public void 남은_사람끼리의_동점은_유지()
        {
            var o = LeaverRanking.Apply(Outcome(("a", 1), ("b", 1), ("c", 3), ("d", 4)), new[] { "c" }, None);
            Assert.AreEqual("a1 b1 d3 c4", Shape(o));
        }

        [Test]
        public void 늦게_나간_쪽이_위()
        {
            var o = LeaverRanking.Apply(Outcome(("a", 1), ("b", 2), ("c", 3)), new[] { "b", "a" }, None);
            Assert.AreEqual("c1 b2 a3", Shape(o));
        }

        [Test]
        public void 한_번도_안_들어온_사람은_맨_아래_동점()
        {
            var o = LeaverRanking.Apply(Outcome(("a", 1), ("b", 2), ("c", 3), ("d", 4)), new[] { "a" }, new[] { "b", "c" });
            Assert.AreEqual("d1 a2 b3 c3", Shape(o));
        }

        [Test]
        public void 전원_나가면_나간_순서대로()
        {
            var o = LeaverRanking.Apply(Outcome(("a", 1), ("b", 2)), new[] { "a", "b" }, None);
            Assert.AreEqual("a1 b2", Shape(o));
        }

        [Test]
        public void 명단_전원이_정확히_한_번_지표도_그대로()
        {
            var o = LeaverRanking.Apply(Outcome(("a", 1), ("b", 2), ("c", 3)), new[] { "a" }, new[] { "c" });
            CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, o.placements.Select(p => p.userId));
            Assert.AreEqual(10, o.placements.First(p => p.userId == "a").stats["score"]);
        }
    }
}
