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

        //  결과가 이미 정해진 사람(완주·탈락)은 나가도 규칙이 매긴 자리 그대로 — 1등으로 들어와 관전 화면에서 나간 사람이 꼴찌가 되면 안 된다.
        [Test]
        public void 결과가_정해진_사람은_나가도_제자리()
        {
            var o = LeaverRanking.Apply(Outcome(("a", 1), ("b", 2), ("c", 3)), new[] { "a", "c" }, None, u => u == "a");
            Assert.AreEqual("a1 b2 c3", Shape(o));
        }

        //  결과 화면·전적이 "나감"이라고 알리게 자루에 표시를 싣는다. 원래 지표는 그대로.
        [Test]
        public void 나간_사람은_자루에_나감_표시()
        {
            var o = LeaverRanking.Apply(Outcome(("a", 1), ("b", 2), ("c", 3)), new[] { "a" }, new[] { "c" });
            Assert.AreEqual(1, o.placements.First(p => p.userId == "a").stats[MatchStatKeys.Left]);
            Assert.AreEqual(1, o.placements.First(p => p.userId == "c").stats[MatchStatKeys.Left]);
            Assert.IsFalse(o.placements.First(p => p.userId == "b").stats.ContainsKey(MatchStatKeys.Left));
            Assert.AreEqual(10, o.placements.First(p => p.userId == "a").stats["score"]);
        }

        [Test]
        public void 지표가_없던_모드도_나감_표시는_실린다()
        {
            var o = new MatchOutcome();
            o.placements.Add(new MatchPlacement { userId = "a", placement = 1, stats = null });
            o.placements.Add(new MatchPlacement { userId = "b", placement = 2, stats = null });
            var r = LeaverRanking.Apply(o, new[] { "a" }, None);
            Assert.AreEqual(1, r.placements.First(p => p.userId == "a").stats[MatchStatKeys.Left]);
            Assert.IsNull(r.placements.First(p => p.userId == "b").stats, "남은 사람 자루는 건드리지 않는다");
        }

        [Test]
        public void 명단_전원이_정확히_한_번_지표도_그대로()
        {
            var o = LeaverRanking.Apply(Outcome(("a", 1), ("b", 2), ("c", 3)), new[] { "a" }, new[] { "c" });
            CollectionAssert.AreEquivalent(new[] { "a", "b", "c" }, o.placements.Select(p => p.userId));
            Assert.AreEqual(10, o.placements.First(p => p.userId == "a").stats["score"]);
        }

        //  Apply가 placement마다 새 객체를 만든다(Copy/CopyLeft) — 그 과정에서 입력에 이미 찍혀 있던
        //  playedSeconds를 날리면, MatchOutcomeFinalizer가 그 뒤에 값을 채우는 순서와 무관하게
        //  누군가 Apply 전에 값을 찍는 경로가 생기는 순간 조용히 0으로 되돌아간다. Copy/CopyLeft가
        //  그 값을 옮기는지 여기서 고정한다(나간 사람 포함).
        [Test]
        public void 입력에_찍힌_플레이시간이_Copy_CopyLeft를_거쳐도_유지된다()
        {
            var o = Outcome(("a", 1), ("b", 2));
            foreach (var placement in o.placements)
            {
                placement.playedSeconds = 37;
            }

            var r = LeaverRanking.Apply(o, new[] { "b" }, None);

            Assert.AreEqual(2, r.placements.Count);
            foreach (var placement in r.placements)
            {
                Assert.AreEqual(37, placement.playedSeconds);
            }
        }
    }
}
