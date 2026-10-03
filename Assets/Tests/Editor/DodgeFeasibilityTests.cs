using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class DodgeFeasibilityTests
    {
        static readonly DodgeConfig C = DodgeSimTables.Config();

        [Test]
        public void 범위_밖_칸은_처음부터_없다()
        {
            var g = new DodgeReachGrid(C.ArenaHalf - DodgeSimMatch.BodyRadius);
            for (int x = 0; x < g.N; x++)
            for (int z = 0; z < g.N; z++)
            {
                var c = g.Center(x, z);
                Assert.LessOrEqual(Mathf.Max(Mathf.Abs(c.x), Mathf.Abs(c.y)), C.ArenaHalf - DodgeSimMatch.BodyRadius + 1e-4f);
            }
        }

        [Test]
        public void 넓히면_한_칸씩_퍼진다()
        {
            var g = new DodgeReachGrid(8.63f);
            g.Only(g.CellOf(Vector2.zero));
            Assert.AreEqual(1, g.Count);
            g.Dilate(diagonal: false);
            Assert.AreEqual(5, g.Count);
            g.Dilate(diagonal: true);
            Assert.AreEqual(21, g.Count);   // 십자 ⊕ 3×3 = 5×5에서 네 모서리 빠짐(팔각형)
        }

        // 경기장 전체를 덮는 바닥 칸만 계속 켜면 피할 곳이 없다.
        [Test]
        public void 피할_곳이_없으면_빈_틱을_찾는다()
        {
            var g = new DodgeReachGrid(8.63f);
            g.FillAll();
            var shapes = new System.Collections.Generic.List<DodgeShape>
            {
                new DodgeShape { Type = DodgeShapeType.Rect, Active = true, X0 = -20f, Z0 = -20f, X1 = 20f, Z1 = 20f },
            };
            DodgeFeasibility.Carve(g, shapes, C);
            Assert.AreEqual(0, g.Count);
        }

        [Test]
        public void 원_밖은_남긴다()
        {
            var g = new DodgeReachGrid(8.63f);
            g.FillAll();
            var shapes = new System.Collections.Generic.List<DodgeShape>
            {
                new DodgeShape { Type = DodgeShapeType.Circle, Active = true, Radius = 2f },
            };
            DodgeFeasibility.Carve(g, shapes, C);
            Assert.IsFalse(g.Get(g.CellOf(Vector2.zero)));
            Assert.IsTrue(g.Get(g.CellOf(new Vector2(5f, 5f))));
        }

        // 칸 크기 때문에 탄을 놓치면 안 된다 — 탄 반지름 + 판정 반지름이 칸 반대각선보다 크다.
        [Test]
        public void 칸이_탄보다_작다()
        {
            Assert.Greater(C.BulletRadius + C.HitRadius, DodgeReachGrid.Cell * 0.7072f);
        }

        // 시드 하나로 스테이지 1~5를 끝까지 — 빈 틱이 없고 스테이지마다 최소 칸 수를 남긴다.
        [Test]
        public void 한_판을_재면_스테이지별_여유가_나온다()
        {
            var S = DodgeSimTables.Stages();
            var r = DodgeFeasibility.Run(1, C, S, DodgeFeasibility.StagesEndTick(S, C));
            Assert.AreEqual(-1, r.DeadTick, "시드 1이 스테이지 1~5에서 못 피하는 판이다");
            Assert.AreEqual(S.Count + 1, r.MinCellsByStage.Length);
            for (int i = 0; i < S.Count; i++) Assert.Greater(r.MinCellsByStage[i], 0);
        }
    }
}
