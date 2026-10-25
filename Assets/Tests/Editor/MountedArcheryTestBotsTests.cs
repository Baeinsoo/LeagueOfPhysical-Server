using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>시험 봇 스위치 값을 읽는 규칙. 에디터 메뉴 값이 env보다 먼저다.</summary>
    public class MountedArcheryTestBotsTests
    {
        [Test]
        public void 스위치가_없으면_0()
        {
            Assert.AreEqual(0, MountedArcheryTestBots.Resolve(null, null));
            Assert.AreEqual(0, MountedArcheryTestBots.Resolve("", null));
        }

        [Test]
        public void env_8이면_8()
        {
            Assert.AreEqual(8, MountedArcheryTestBots.Resolve("8", null));
        }

        [Test]
        public void 숫자가_아니면_0()
        {
            Assert.AreEqual(0, MountedArcheryTestBots.Resolve("many", null));
            Assert.AreEqual(0, MountedArcheryTestBots.Resolve("-3", null));
            Assert.AreEqual(0, MountedArcheryTestBots.Resolve("0", null));
        }

        [Test]
        public void 에디터_값이_env보다_먼저()
        {
            Assert.AreEqual(5, MountedArcheryTestBots.Resolve("8", 5));
            Assert.AreEqual(8, MountedArcheryTestBots.Resolve(null, 8));
        }

        [Test]
        public void 에디터_0이면_꺼짐()
        {
            Assert.AreEqual(0, MountedArcheryTestBots.Resolve("8", 0));
            Assert.AreEqual(0, MountedArcheryTestBots.Resolve("8", -2));
        }
    
        [Test]
        public void 에디터_저장_키는_프로젝트마다_다르다()
        {
            Assert.AreNotEqual(MountedArcheryTestBots.ScopedKey("C:/a/Assets"), MountedArcheryTestBots.ScopedKey("C:/b/Assets"));
            Assert.AreEqual(MountedArcheryTestBots.ScopedKey("C:/a/Assets"), MountedArcheryTestBots.ScopedKey("C:/a/Assets"));
        }
    }
}
