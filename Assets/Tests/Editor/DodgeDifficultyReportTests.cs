using NUnit.Framework;

namespace LOP.Tests
{
    public class DodgeDifficultyReportTests
    {
        [Test]
        public void 보고서에_스펙의_표가_다_있다()
        {
            string md = DodgeDifficultyReport.Build(DodgeSimTables.Config(), DodgeSimTables.Stages(), seeds: 2, playerCounts: new[] { 1, 2 });
            StringAssert.Contains("1분당 맞는 횟수", md);
            StringAssert.Contains("탈락 스테이지", md);
            StringAssert.Contains("판 길이", md);
            StringAssert.Contains("가장 좁은 순간", md);
            StringAssert.Contains("서든데스 불가능", md);
            StringAssert.Contains("막혀서 맞은 비율", md);
        }

        // 같은 시드면 같은 숫자 — 4b 튜닝 전후를 비교할 수 있어야 한다.
        [Test]
        public void 같은_입력이면_같은_보고서()
        {
            var c = DodgeSimTables.Config(); var s = DodgeSimTables.Stages();
            Assert.AreEqual(DodgeDifficultyReport.Build(c, s, 2, new[] { 2 }), DodgeDifficultyReport.Build(c, s, 2, new[] { 2 }));
        }

        // 판은 n−1명이 탈락할 때까지 가서 맞은 총수는 충돌과 무관하게 ~5(n−1)로 묶인다 — 막힘은 "같은 시간에 더 많이 맞음"으로 잰다(검토 C1).
        [Test]
        public void 막힘_비율은_살아_있던_시간으로_나눈_비율로_잰다()
        {
            // 둘 다 35번 맞았지만 충돌 판은 더 빨리 탈락했다(700초 vs 1000초) → 같은 시간에 더 많이 맞았다.
            Assert.AreEqual(30.0, DodgeDifficultyReport.BlockedPercent(35, 700.0, 35, 1000.0), 1e-6);
            Assert.AreEqual(0.0, DodgeDifficultyReport.BlockedPercent(0, 700.0, 0, 1000.0), 1e-6);
        }
    }
}
