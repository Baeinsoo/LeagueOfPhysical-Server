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

        // 반응이 빠르면 더 오래 버틴다 — 반응 지연이 실제로 "모른다"로 작동하는지. 맞은 횟수는 목숨(5)에서 막혀
        // 둘 다 다 잃으면 안 갈리므로(수박 처음 버전) 버틴 시간으로 잰다.
        static long Survive(IDodgeSimMover mover, ulong seed)
        {
            var m = new DodgeSimMatch(seed, C, S, 1);
            m.Run(mover, 15000);
            return m.Tick;
        }

        [Test]
        public void 반응이_빠르면_더_오래_버틴다()
        {
            long fast = 0, slow = 0;
            for (ulong s = 1; s <= 8; s++)
            {
                fast += Survive(new DodgeHumanBot(s, reactionSeconds: 0f), s);
                slow += Survive(new DodgeHumanBot(s, reactionSeconds: 0.6f), s);
            }
            Assert.Greater(fast, slow);
        }

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

        // 탄비는 탄이 하나씩 나타난다 — 사람은 아직 안 나온 탄을 못 본다(검토 I1). 본 틱까지 나온 탄만 남긴 사본을 쓴다.
        [Test]
        public void 아직_안_나온_탄은_모른다()
        {
            var rain = new DodgePattern(1, DodgePatternKind.BulletRain, 100, 7, 0f, 40f, 4f, 0.15f);
            Assert.AreEqual(6f, DodgeHumanBot.Visible(rain, 120).P1);    // 나이 20틱 → 0,4,8,12,16,20틱 탄
            Assert.AreEqual(40f, DodgeHumanBot.Visible(rain, 1000).P1);
            var bomb = new DodgePattern(2, DodgePatternKind.Bomb, 100, 7, 1f, 2f, 2f, 0f);
            Assert.AreEqual(bomb.P1, DodgeHumanBot.Visible(bomb, 120).P1);   // 다른 종류는 그대로
        }

        // 온돌 칸은 발밑 판정이라 판정 반지름이 안 붙는다 — 봇이 칸 경계 바로 안쪽을 고르면 빗나감에 넘어간다.
        // 계획할 때는 칸도 여유만큼 넓혀 본다(실제 판정은 그대로).
        [Test]
        public void 계획할_때_칸도_여유만큼_넓힌다()
        {
            var tile = new DodgeShape { Type = DodgeShapeType.Rect, Active = true, X0 = 0f, Z0 = 0f, X1 = 3f, Z1 = 3f };
            var wide = DodgeHumanBot.PlanShape(tile, 0.45f);
            Assert.AreEqual(-0.45f, wide.X0, 1e-5f); Assert.AreEqual(3.45f, wide.X1, 1e-5f);
            var bomb = new DodgeShape { Type = DodgeShapeType.Circle, Active = true, Radius = 2f };
            Assert.AreEqual(2f, DodgeHumanBot.PlanShape(bomb, 0.45f).Radius, 1e-5f);   // 원은 판정 반지름 쪽에서 이미 넓힌다
        }

        // 가운데 심판(충돌체)을 가로지르는 길은 막혀 그 자리에 선다 — 봇은 그런 길을 고르지 않는다.
        [Test]
        public void 심판을_가로지르는_길은_막힌_길이다()
        {
            Assert.IsTrue(DodgeHumanBot.PathBlocked(new Vector2(4f, 0f), new Vector2(-4f, 0f), DodgeReferee.Spot));
            Assert.IsTrue(DodgeHumanBot.PathBlocked(new Vector2(4f, 0f), new Vector2(0.3f, 0f), DodgeReferee.Spot));    // 끝이 심판 안
            Assert.IsFalse(DodgeHumanBot.PathBlocked(new Vector2(4f, 3f), new Vector2(-4f, 3f), DodgeReferee.Spot));
        }
    }
}
