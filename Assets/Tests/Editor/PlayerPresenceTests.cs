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
    }
}
