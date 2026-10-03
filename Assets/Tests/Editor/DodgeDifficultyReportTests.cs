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
    }
}
