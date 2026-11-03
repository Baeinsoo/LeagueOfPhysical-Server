using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시 봇 채우기 목표 — 최소 레인과 시험 스위치 중 더 큰 쪽을 사람 수~8명으로 자른다.
    /// </summary>
    public class CardRushBotFillTests
    {
        [Test]
        public void 사람_1이면_3명_채움()
        {
            var fill = new CardRushBotFill(() => 0, minLanes: 4);
            Assert.AreEqual(3, fill.BotsToAdd(1));
        }

        [Test]
        public void 사람_5면_0()
        {
            var fill = new CardRushBotFill(() => 0, minLanes: 4);
            Assert.AreEqual(0, fill.BotsToAdd(5));
        }

        [Test]
        public void 스위치_8이면_사람_1에_7()
        {
            var fill = new CardRushBotFill(() => 8, minLanes: 4);
            Assert.AreEqual(7, fill.BotsToAdd(1));
        }

        [Test]
        public void 여덟명_넘게는_안_채움()
        {
            //  스위치가 8보다 커도(오입력 등) 8명(MaxLanes)을 넘겨 채우지 않는다.
            var fill = new CardRushBotFill(() => 20, minLanes: 4);
            Assert.AreEqual(7, fill.BotsToAdd(1));
            Assert.AreEqual(0, fill.BotsToAdd(8));
        }
    }
}
