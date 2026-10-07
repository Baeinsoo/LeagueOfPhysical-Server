using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>끊긴 사람 목록을 클라가 아는 말(엔티티 id)로 옮긴다 — 몸이 없는 사람은 뺀다.</summary>
    public class PresenceBroadcastSystemTests
    {
        [Test]
        public void 끊긴_사람을_엔티티와_순번으로()
        {
            var p = new PlayerPresence();
            p.Begin(new[] { "a", "b" });
            p.MarkMatchStarted();
            p.MarkJoined("a"); p.MarkJoined("b");
            p.MarkLeft("b");

            var wire = PresenceBroadcastSystem.ToWire(p, user => user == "b" ? "e2" : "e1");

            Assert.AreEqual(p.Version, wire.Version);
            Assert.AreEqual(1, wire.Away.Count);
            Assert.AreEqual("e2", wire.Away[0].EntityId);
            Assert.AreEqual(2, wire.Away[0].Slot);
        }

        [Test]
        public void 몸이_없는_사람은_뺀다()
        {
            var p = new PlayerPresence();
            p.Begin(new[] { "a" });
            p.MarkMatchStarted();
            p.MarkJoined("a");
            p.MarkLeft("a");

            Assert.AreEqual(0, PresenceBroadcastSystem.ToWire(p, _ => null).Away.Count);
        }
    }
}
