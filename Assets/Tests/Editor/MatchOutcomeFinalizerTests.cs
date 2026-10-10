using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// LOPRunner.EndMatch가 실제로 거치는 단일 경로 — 등수 확정(LeaverRanking)과 플레이 시간
    /// 기록(PlayedSeconds)이 한 호출에 같이 일어나는지. 이게 비어 있으면(둘 중 하나를 빼먹으면)
    /// 보상 보고에 실리는 playedSeconds가 조용히 0이 된다.
    /// </summary>
    public class MatchOutcomeFinalizerTests
    {
        static MatchOutcome Outcome(params string[] users)
        {
            var o = new MatchOutcome();
            int place = 1;
            foreach (var user in users)
            {
                o.placements.Add(new MatchPlacement { userId = user, placement = place++ });
            }
            return o;
        }

        static readonly string[] None = new string[0];

        [Test]
        public void 나간_사람_포함_전원에게_같은_플레이시간이_실린다()
        {
            var raw = Outcome("a", "b", "c");

            var result = MatchOutcomeFinalizer.Finalize(raw, new[] { "b" }, new[] { "c" }, null,
                endTick: 1000, startTick: 0, interval: 0.02);

            Assert.AreEqual(3, result.placements.Count);
            foreach (var placement in result.placements)
            {
                Assert.AreEqual(20, placement.playedSeconds);
            }
        }

        //  매치가 출발도 안 했는데(StartTick 미확정) 끝나는 이례적 호출 순서에서는 음수·소수 대신 0.
        [Test]
        public void 시작하지_않은_판은_전원_0초()
        {
            var raw = Outcome("a", "b");

            var result = MatchOutcomeFinalizer.Finalize(raw, None, None, null,
                endTick: 1000, startTick: long.MaxValue, interval: 0.02);

            Assert.AreEqual(2, result.placements.Count);
            foreach (var placement in result.placements)
            {
                Assert.AreEqual(0, placement.playedSeconds);
            }
        }
    }
}
