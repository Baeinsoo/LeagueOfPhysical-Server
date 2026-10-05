using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    // 예고를 본 뒤 닿을 수 있나 — 검사기(미래를 아는 사람)가 못 잡는 공정성. 반응 0.35초 뒤 4 m/s로 갈 수 있는 원 안에
    // 그 패턴이 켜져 있는 동안 내내 안전한 자리가 있으면 닿는다.
    public class DodgeFairnessTests
    {
        static readonly DodgeConfig C = DodgeSimTables.Config();

        [Test]
        public void 닿는_거리는_예고에서_반응을_뺀_만큼이다()
        {
            Assert.AreEqual(4f * (1.2f - 0.35f), DodgeFairness.Reach(60), 1e-4f);
            Assert.AreEqual(0f, DodgeFairness.Reach(10), 1e-4f);
        }

        // 발밑 수박(반지름 2 + 판정 0.16): 예고 1.2초면 3.4m라 빠져나가고, 0.8초면 1.8m라 못 빠져나간다.
        [Test]
        public void 발밑_수박은_예고가_짧으면_못_닿는다()
        {
            var slow = new DodgePattern(1, DodgePatternKind.Bomb, 0, 0, 0f, 0f, 2f, 0f, 60);
            var fast = new DodgePattern(2, DodgePatternKind.Bomb, 0, 0, 0f, 0f, 2f, 0f, 40);
            Assert.IsTrue(DodgeFairness.Reachable(slow, Vector2.zero, C, 9f - 0.37f));
            Assert.IsFalse(DodgeFairness.Reachable(fast, Vector2.zero, C, 9f - 0.37f));
        }

        [Test]
        public void 바닥이_다_뜨거우면_못_닿는다()
        {
            var all = new DodgePattern(1, DodgePatternKind.Tiles, 0, ulong.MaxValue >> (64 - 36), 0f, 0f, 0f, 0f, 60);
            Assert.IsFalse(DodgeFairness.Reachable(all, new Vector2(1f, 1f), C, 9f - 0.37f));
            var half = new DodgePattern(2, DodgePatternKind.Tiles, 0, 0x5555_5555_5UL, 0f, 0f, 0f, 0f, 60);
            Assert.IsTrue(DodgeFairness.Reachable(half, new Vector2(1f, 1f), C, 9f - 0.37f));
        }

        [Test]
        public void 나를_지나는_줄은_비키면_된다()
        {
            var laser = new DodgePattern(1, DodgePatternKind.Laser, 0, 0, 1f, -9f, 1f, 9f, 50);
            Assert.IsTrue(DodgeFairness.Reachable(laser, new Vector2(1f, 0f), C, 9f - 0.37f));
        }
    }
}
