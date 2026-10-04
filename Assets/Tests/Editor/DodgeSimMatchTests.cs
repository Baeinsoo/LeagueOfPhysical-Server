using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class DodgeSimMatchTests
    {
        static readonly DodgeConfig C = DodgeSimTables.Config();
        static readonly DodgeStageTable S = DodgeSimTables.Stages();

        sealed class Stand : IDodgeSimMover { public Vector2 Want(DodgeSimMatch m, int p) => m.Positions[p]; }
        sealed class Go : IDodgeSimMover
        {
            readonly Vector2 to; public int Calls;
            public Go(Vector2 to) { this.to = to; }
            public Vector2 Want(DodgeSimMatch m, int p) { Calls++; return to; }
        }

        [Test]
        public void 가만히_서_있으면_결국_맞고_탈락한다()
        {
            var m = new DodgeSimMatch(1, C, S, 1);
            m.Run(new Stand(), 15000);
            Assert.Greater(m.Hits.Count, 0);
            Assert.IsTrue(m.Over);
            Assert.AreEqual(1, m.Eliminations.Count);
        }

        [Test]
        public void 같은_시드는_같은_판이다()
        {
            var a = new DodgeSimMatch(7, C, S, 2); a.Run(new Stand(), 15000);
            var b = new DodgeSimMatch(7, C, S, 2); b.Run(new Stand(), 15000);
            CollectionAssert.AreEqual(a.Hits, b.Hits);
        }

        [Test]
        public void 한_틱에_최고_속도만큼만_간다()
        {
            var m = new DodgeSimMatch(1, C, S, 1);
            var start = m.Positions[0];
            m.Step(new Go(start + new Vector2(0f, -5f)));
            Assert.AreEqual(DodgeSimMatch.MoveSpeed / DodgeConfig.TicksPerSecond, (m.Positions[0] - start).magnitude, 1e-4f);
        }

        [Test]
        public void 벽_안쪽에서_몸_반지름만큼_떨어져_선다()
        {
            var m = new DodgeSimMatch(1, C, S, 1);
            for (int i = 0; i < 200; i++) m.Step(new Go(new Vector2(30f, 0f)));
            Assert.AreEqual(C.ArenaHalf - DodgeSimMatch.BodyRadius, m.Positions[0].x, 1e-4f);
        }

        // 캐릭터끼리는 단단한 벽 — 겹치게 되면 그 틱은 멈춘다.
        [Test]
        public void 다른_선수와_겹치면_멈춘다()
        {
            var m = new DodgeSimMatch(1, C, S, 2);
            var other = m.Positions[1];
            for (int i = 0; i < 300; i++) m.Step(new Go(other));
            Assert.GreaterOrEqual((m.Positions[0] - m.Positions[1]).magnitude, 2f * DodgeSimMatch.BodyRadius - 1e-4f);
        }

        [Test]
        public void 충돌을_끄면_지나갈_수_있다()
        {
            var m = new DodgeSimMatch(1, C, S, 2, collide: false);
            var other = m.Positions[1];
            for (int i = 0; i < 300; i++) m.Step(new Go(other));
            Assert.Less((m.Positions[0] - other).magnitude, 0.1f);
        }

        // 탈락한 몸은 레지스트리에서 빠지고 이동기를 더 부르지 않는다 — 진행기가 유령을 노리면 안 된다.
        [Test]
        public void 탈락하면_더_움직이지_않는다()
        {
            var m = new DodgeSimMatch(1, C, S, 1);
            m.Run(new Stand(), 15000);
            var go = new Go(Vector2.zero);
            m.Step(go);
            Assert.AreEqual(0, go.Calls);
            Assert.IsFalse(m.Alive[0]);
        }

        // 서버 규칙: 혼자면 탈락해야 끝, 둘 이상이면 한 명 남으면 끝.
        [Test]
        public void 둘이면_한_명_남을_때_끝난다()
        {
            var m = new DodgeSimMatch(3, C, S, 2);
            m.Run(new Stand(), 15000);
            Assert.IsTrue(m.Over);
            Assert.AreEqual(1, m.Eliminations.Count);
        }

        // 가운데 투척 심판은 맵 충돌체다 — 선수는 뚫고 지나가지 못한다.
        [Test]
        public void 가운데_심판은_못_지나간다()
        {
            var m = new DodgeSimMatch(1, C, S, 1);
            int walkIn = DodgeConfig.Ticks(DodgeReferee.WalkSeconds);
            for (int i = 0; i <= walkIn; i++) m.Step(new Stand());   // 심판이 가운데에 설 때까지
            for (int i = 0; i < 300; i++) m.Step(new Go(new Vector2(-4f, 0f)));   // (4,0)에서 가운데를 가로질러
            Assert.GreaterOrEqual((m.Positions[0] - DodgeReferee.Spot).magnitude,
                                  DodgeSimMatch.RefereeRadius + DodgeSimMatch.BodyRadius - 1e-3f);
        }
    }
}
