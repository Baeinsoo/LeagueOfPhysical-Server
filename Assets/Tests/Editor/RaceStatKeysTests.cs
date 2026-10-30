using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>레이스형 공통 "완주" 지표(spec 2026-10-30 §4) — 로비 퀘스트 "완주하기"·"기절 없이 완주"가 읽는다.</summary>
    public class RaceStatKeysTests
    {
        [Test]
        public void 참가자마다_완주면_1_아니면_0을_싣고_다른_키는_둔다()
        {
            var outcome = new MatchOutcome();
            outcome.placements.Add(new MatchPlacement { userId = "a", placement = 1 });
            outcome.placements.Add(new MatchPlacement { userId = "b", placement = 2, stats = new Dictionary<string, int> { [MatchStatKeys.Left] = 1 } });
            outcome.placements.Add(new MatchPlacement { userId = "x", placement = 3 });

            RaceStatKeys.AttachFinished(outcome, new[] { "a", "b" }, new HashSet<string> { "a" });

            Assert.AreEqual(1, outcome.placements[0].stats["finished"]);
            Assert.AreEqual(0, outcome.placements[1].stats["finished"], "0도 싣는다 — 옛 서버(키 없음)와 갈리게");
            Assert.AreEqual(1, outcome.placements[1].stats[MatchStatKeys.Left]);
            Assert.IsNull(outcome.placements[2].stats, "참가자가 아닌 줄은 건드리지 않는다");
        }
    }
}
