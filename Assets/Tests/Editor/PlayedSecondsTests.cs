using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>경과 틱을 초로 바꾼다. 로비가 이 값으로 보상을 계산하므로 정수·0 이상이어야 한다.</summary>
    public class PlayedSecondsTests
    {
        [Test]
        public void 시작부터_끝까지_틱수를_초로_바꾼다()
        {
            Assert.AreEqual(20, PlayedSeconds.Compute(1000, 0, 0.02));
        }

        [Test]
        public void 매치가_시작하지_않았으면_0()
        {
            Assert.AreEqual(0, PlayedSeconds.Compute(1000, long.MaxValue, 0.02));
        }

        [Test]
        public void 끝틱이_시작틱보다_앞이면_0()
        {
            Assert.AreEqual(0, PlayedSeconds.Compute(10, 20, 0.02));
        }

        [Test]
        public void 소수는_내림()
        {
            Assert.AreEqual(20, PlayedSeconds.Compute(1001, 0, 0.02));
        }

        [Test]
        public void 시작틱이_0이_아니어도_차이만큼_계산()
        {
            Assert.AreEqual(20, PlayedSeconds.Compute(1050, 50, 0.02));
        }
    }
}
