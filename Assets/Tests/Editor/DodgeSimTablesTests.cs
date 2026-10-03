using NUnit.Framework;

namespace LOP.Tests
{
    public class DodgeSimTablesTests
    {
        // 시뮬은 서버가 쓰는 표를 그대로 읽어야 한다 — 표를 고치면 측정도 따라간다.
        [Test]
        public void 실제_표를_읽는다()
        {
            var c = DodgeSimTables.Config();
            var s = DodgeSimTables.Stages();
            Assert.AreEqual(5, c.Lives);
            Assert.AreEqual(5, s.Count);
            Assert.AreEqual("슬리퍼", s[0].Name);
        }
    }
}
