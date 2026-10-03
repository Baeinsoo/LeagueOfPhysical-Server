using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class DodgeHumanBotTests
    {
        static readonly DodgeConfig C = DodgeSimTables.Config();
        static readonly DodgeStageTable S = DodgeSimTables.Stages();
        static readonly long Stage1 = S.At(0, 0, C).EndTick;
        sealed class Stand : IDodgeSimMover { public Vector2 Want(DodgeSimMatch m, int p) => m.Positions[p]; }

        static int HitsIn(IDodgeSimMover mover, ulong seed, long ticks)
        {
            var m = new DodgeSimMatch(seed, C, S, 1);
            m.Run(mover, ticks);
            return m.Hits.Count;
        }

        // 스테이지 1(25초) — 목숨(5)이 바닥나기 전이라 맞은 횟수로 갈린다. 봇은 가만히 선 사람보다 확실히 덜 맞는다.
        [Test]
        public void 봇은_가만히_서_있기보다_덜_맞는다()
        {
            int bot = 0, stand = 0;
            for (ulong s = 1; s <= 5; s++) { bot += HitsIn(new DodgeHumanBot(s), s, Stage1); stand += HitsIn(new Stand(), s, Stage1); }
            Assert.Less(bot * 2, stand);
        }

        // 반응이 빠르면 덜 맞는다 — 반응 지연이 실제로 "모른다"로 작동하는지.
        [Test]
        public void 반응이_빠르면_덜_맞는다()
        {
            int fast = 0, slow = 0;
            for (ulong s = 1; s <= 8; s++)
            {
                fast += HitsIn(new DodgeHumanBot(s, reactionSeconds: 0f), s, Stage1);
                slow += HitsIn(new DodgeHumanBot(s, reactionSeconds: 0.6f), s, Stage1);
            }
            Assert.Less(fast, slow);
        }

        // 화면에 뜨기 전 패턴은 모른다 — 반응 지연이 패턴 길이보다 길면 피하지 못한다.
        [Test]
        public void 모르는_패턴은_안_본다()
        {
            var blind = new DodgeHumanBot(1, reactionSeconds: 30f);
            int blindHits = HitsIn(blind, 1, Stage1), standHits = HitsIn(new Stand(), 1, Stage1);
            Assert.GreaterOrEqual(blindHits * 2, standHits);
        }

        [Test]
        public void 같은_시드면_같은_결과()
        {
            Assert.AreEqual(HitsIn(new DodgeHumanBot(3), 3, 3000), HitsIn(new DodgeHumanBot(3), 3, 3000));
        }
    }
}
