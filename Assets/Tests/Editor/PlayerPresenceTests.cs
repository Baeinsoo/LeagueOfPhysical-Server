using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>누가 나갔나·돌아왔나·한 번도 안 들어왔나 — 끝날 때 꼴찌로 보낼 사람과, 판이 기다리지 말아야 할 사람.</summary>
    public class PlayerPresenceTests
    {
        static PlayerPresence Begun(params string[] roster)
        {
            var p = new PlayerPresence();
            p.Begin(roster);
            p.MarkMatchStarted();
            return p;
        }

        [Test]
        public void 시작하면_전원_아직_안_들어옴()
        {
            var p = Begun("a", "b");
            CollectionAssert.AreEquivalent(new[] { "a", "b" }, p.NeverJoined);
            Assert.IsEmpty(p.LeftLatestFirst);
            Assert.IsFalse(p.IsAway("a"), "안 들어온 사람은 '나가 있음'이 아니다 — 빼면 시작 전에 판이 끝난다");
        }

        [Test]
        public void 들어왔다가_끊기면_나가_있음()
        {
            var p = Begun("a", "b");
            p.MarkJoined("a");
            p.MarkLeft("a");
            Assert.IsTrue(p.IsAway("a"));
            CollectionAssert.AreEqual(new[] { "a" }, p.LeftLatestFirst);
            CollectionAssert.AreEqual(new[] { "b" }, p.NeverJoined);
        }

        //  판이 시작되기 전엔 기다리지 않기를 안 건다 — 대기 화면에서 잠깐 끊긴 사람 때문에 판이 시작 전에 끝나면 안 된다.
        [Test]
        public void 시작_전엔_나가_있음으로_치지_않는다()
        {
            var p = new PlayerPresence();
            p.Begin(new[] { "a" });
            p.MarkJoined("a");
            p.MarkLeft("a");
            Assert.IsFalse(p.IsAway("a"));
            CollectionAssert.AreEqual(new[] { "a" }, p.LeftLatestFirst, "등수에서는 그대로 나간 사람이다");
            p.MarkMatchStarted();
            Assert.IsTrue(p.IsAway("a"));
        }

        [Test]
        public void 다시_들어오면_기록이_지워진다()
        {
            var p = Begun("a");
            p.MarkJoined("a");
            p.MarkLeft("a");
            p.MarkJoined("a");
            Assert.IsFalse(p.IsAway("a"));
            Assert.IsEmpty(p.LeftLatestFirst);
        }

        [Test]
        public void 늦게_나간_쪽이_앞()
        {
            var p = Begun("a", "b", "c");
            p.MarkJoined("a"); p.MarkJoined("b"); p.MarkJoined("c");
            p.MarkLeft("b");
            p.MarkLeft("a");
            CollectionAssert.AreEqual(new[] { "a", "b" }, p.LeftLatestFirst);
        }

        [Test]
        public void 명단_밖은_무시()
        {
            var p = Begun("a");
            p.MarkJoined("x");
            p.MarkLeft("x");
            Assert.IsFalse(p.IsAway("x"));
            Assert.IsEmpty(p.LeftLatestFirst);
        }

        //  판 도중 "연결 끊김" 표시 — 바뀔 때만 판본이 오르고, 끊긴 사람은 명단 순번과 함께 나온다.
        [Test]
        public void 판본은_바뀔_때만_오른다()
        {
            var p = Begun("a", "b");
            int v0 = p.Version;
            p.MarkJoined("a");
            int v1 = p.Version;
            p.MarkJoined("a");
            Assert.Greater(v1, v0);
            Assert.AreEqual(v1, p.Version, "같은 사람이 또 들어온 건 바뀐 게 아니다");
            p.MarkLeft("a");
            Assert.Greater(p.Version, v1);
        }

        [Test]
        public void 끊긴_사람은_명단_순번과_함께()
        {
            var p = Begun("a", "b", "c");
            p.MarkJoined("a"); p.MarkJoined("b"); p.MarkJoined("c");
            p.MarkLeft("c");
            CollectionAssert.AreEqual(new[] { ("c", 3) }, p.AwaySlots);
        }

        [Test]
        public void 시작_전엔_끊긴_사람이_없다()
        {
            var p = new PlayerPresence();
            p.Begin(new[] { "a" });
            p.MarkJoined("a");
            p.MarkLeft("a");
            Assert.IsEmpty(p.AwaySlots);
        }
    }
}
