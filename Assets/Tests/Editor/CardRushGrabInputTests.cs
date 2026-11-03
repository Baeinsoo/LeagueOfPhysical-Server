using System.Reflection;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 와이어 grab → 도메인 InputCommand.Grab. 이 줄이 빠지면 서버는 탭을 한 번도 못 보고 판이 조용히 멈춰 있다
    /// (클라 예측만 앞으로 가다 매 스냅에 되돌려진다). 변환은 수신 어댑터의 비공개 함수라 리플렉션으로 부른다.
    /// </summary>
    public class CardRushGrabInputTests
    {
        private static InputCommand Convert(global::InputCommand wire)
        {
            var method = typeof(GameInputMessageHandler).GetMethod("ToInputCommand", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "GameInputMessageHandler.ToInputCommand가 없다 — 이름이 바뀌었으면 시험도 고친다");
            return (InputCommand)method.Invoke(null, new object[] { wire });
        }

        [Test]
        public void 잡기_입력을_옮긴다()
        {
            Assert.IsTrue(Convert(new global::InputCommand { Grab = true }).Grab);
            Assert.IsFalse(Convert(new global::InputCommand { Grab = false }).Grab);
        }
    }
}
