using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class DodgeDirectorTests
    {
        static DodgeConfig Config(int onlyKind = 0) => new DodgeConfig(3, 1.5f, 0.16f, 0.5f, 9f, 6, 2f, 1.8f, onlyKind,
                                                                       1.2f, 5f, 0.22f, 2f, 0.25f, 0.7f, 0.45f, 6f, 1.35f, 0.55f,
                                                                       0.5f, 0.8f, 1.5f, 0.02f);
        static readonly Vector2[] Two = { new Vector2(1, 1), new Vector2(-2, 3) };

        // 긴 한 스테이지에 모든 종류, 세기 1 고정 — 슬라이스 2와 같은 조건
        static DodgeStageTable Flat() => new DodgeStageTable(new[]
        {
            new DodgeStage("전부", 600f, DodgeStageTable.AllKinds, 1f, 0f, 1.8f),
        });

        static List<DodgePattern> Run(DodgeDirector d, long from, long to, long start)
        {
            var all = new List<DodgePattern>();
            for (long t = from; t <= to; t++) d.Next(t, start, Two, all);
            return all;
        }

        [Test]
        public void 경기_시작_전에는_아무것도_안_낸다()
        {
            var d = new DodgeDirector(1UL, Config(), Flat());
            Assert.AreEqual(0, Run(d, 0, 500, long.MaxValue).Count);
        }

        [Test]
        public void 시작_뒤_첫_지연이_지나야_내고_예약_시간만큼_뒤에_시작한다()
        {
            var c = Config();
            var d = new DodgeDirector(1UL, c, Flat());
            var got = Run(d, 100, 100 + c.FirstPatternDelayTicks, 100);
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual(100 + c.FirstPatternDelayTicks + c.LeadTicks, got[0].StartTick);
        }

        [Test]
        public void 같은_씨앗이면_같은_순서로_낸다()
        {
            var a = Run(new DodgeDirector(42UL, Config(), Flat()), 0, 1000, 0);
            var b = Run(new DodgeDirector(42UL, Config(), Flat()), 0, 1000, 0);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Kind, b[i].Kind);
                Assert.AreEqual(a[i].Seed, b[i].Seed);
                Assert.AreEqual(a[i].P0, b[i].P0);
                Assert.AreEqual(a[i].StartTick, b[i].StartTick);
                Assert.AreEqual(a[i].WarnTicks, b[i].WarnTicks);
            }
        }

        // 발밑을 정확히 노리면 예고 1초에 2.2m를 뛰어야 해 보통 사람은 못 피한다(4a 측정: 수박에서 95% 탈락).
        // 사람마다 하나씩, 그 사람 근처(1~1.8m 옆)에 떨어진다 — 여전히 그 사람을 덮지만 빠져나갈 길이 짧다.
        [Test]
        public void 폭탄은_산_사람마다_하나씩_근처에_떨어진다()
        {
            var d = new DodgeDirector(1UL, Config((int)DodgePatternKind.Bomb), Flat());
            var got = Run(d, 0, Config().FirstPatternDelayTicks, 0);
            Assert.AreEqual(2, got.Count);
            var targets = Two;
            for (int i = 0; i < 2; i++)
            {
                float off = (new Vector2(got[i].P0, got[i].P1) - targets[i]).magnitude;
                Assert.GreaterOrEqual(off, DodgeDirector.BombNearMin - 1e-4f);
                Assert.LessOrEqual(off, DodgeDirector.BombNearMax + 1e-4f);
            }
        }

        // 탄 벽은 구멍으로만 지나간다 — 탄 사이가 판정 지름보다 넓으면 정가운데로 빠지는 틈이 생기는데, 그림으로는 몸을 뚫고 지나가 보인다.
        [Test]
        public void 탄_벽은_구멍_말고는_못_지나간다()
        {
            var c = Config((int)DodgePatternKind.BulletWall);
            var got = Run(new DodgeDirector(1UL, c, Flat()), 0, c.FirstPatternDelayTicks, 0);
            Assert.Less(got[0].P3, 2f * (c.BulletRadius + c.HitRadius));
        }

        [Test]
        public void 조준탄은_산_사람_중_하나를_노린다()
        {
            var d = new DodgeDirector(7UL, Config((int)DodgePatternKind.BulletAimed), Flat());
            var got = Run(d, 0, Config().FirstPatternDelayTicks, 0);
            Assert.AreEqual(1, got.Count);
            var target = new Vector2(got[0].P2, got[0].P3);
            Assert.IsTrue(target == Two[0] || target == Two[1]);
        }

        [Test]
        public void 종류를_돌아가며_내고_아이디는_겹치지_않는다()
        {
            var got = Run(new DodgeDirector(3UL, Config(), Flat()), 0, 2000, 0);
            var kinds = new HashSet<DodgePatternKind>();
            var ids = new HashSet<int>();
            foreach (var p in got) { kinds.Add(p.Kind); Assert.IsTrue(ids.Add(p.Id)); }
            Assert.AreEqual(7, kinds.Count);
        }

        [Test]
        public void 스테이지가_정한_종류만_낸다()
        {
            var stages = new DodgeStageTable(new[]
            {
                // 경계 520틱 — 간격 80틱이라 500틱에 고른 패턴이 경계 너머(525틱)에 뜬다
                new DodgeStage("폭탄", 10.4f, new[] { DodgePatternKind.Bomb }, 1f, 0f, 1.6f),
                new DodgeStage("레이저", 10f, new[] { DodgePatternKind.Laser }, 1f, 0f, 1.6f),
            });
            var c = Config();
            var got = Run(new DodgeDirector(5UL, c, stages), 0, 999, 0);
            Assert.Greater(got.Count, 0);
            // 기준은 고른 틱이 아니라 화면에 뜨는 틱이다 — 배너가 레이저로 바뀐 뒤에 폭탄이 뜨면 안 된다.
            foreach (var p in got)
            {
                var want = p.StartTick < 520 ? DodgePatternKind.Bomb : DodgePatternKind.Laser;
                Assert.AreEqual(want, p.Kind, $"starts at {p.StartTick}");
            }
        }

        [Test]
        public void 서든데스에는_모든_종류가_섞여_나온다()
        {
            var got = Run(new DodgeDirector(9UL, Config(), new DodgeStageTable(new DodgeStage[0])), 0, 3000, 0);
            var kinds = new HashSet<DodgePatternKind>();
            foreach (var p in got) kinds.Add(p.Kind);
            Assert.AreEqual(7, kinds.Count);
        }

        [Test]
        public void 한_종류만_보는_설정은_스테이지보다_우선한다()
        {
            var stages = new DodgeStageTable(new[] { new DodgeStage("폭탄", 60f, new[] { DodgePatternKind.Bomb }, 1f, 0f, 1.6f) });
            var got = Run(new DodgeDirector(5UL, Config((int)DodgePatternKind.Tiles), stages), 0, 999, 0);
            Assert.Greater(got.Count, 0);
            foreach (var p in got) Assert.AreEqual(DodgePatternKind.Tiles, p.Kind);
        }

        [Test]
        public void 세기가_오르면_간격이_준다()
        {
            var c = Config();
            var calm = new DodgeStagePoint(0, false, 0, 500, 0f, 1f, DodgeStageTable.AllKinds, 90);
            var hot = new DodgeStagePoint(0, false, 0, 500, 1f, 1.5f, DodgeStageTable.AllKinds, 90);
            Assert.AreEqual(90, DodgeDirector.IntervalTicksAt(calm, c));
            Assert.AreEqual(60, DodgeDirector.IntervalTicksAt(hot, c));
        }

        [Test]
        public void 간격은_하한_아래로_안_간다()
        {
            var c = Config();
            var wild = new DodgeStagePoint(5, true, 0, long.MaxValue, 0f, 100f, DodgeStageTable.AllKinds, 90);
            Assert.AreEqual(c.MinIntervalTicks, DodgeDirector.IntervalTicksAt(wild, c));
        }

        [Test]
        public void 예고는_세기에_따라_짧아지고_하한_아래로_안_간다()
        {
            var c = Config();
            Assert.AreEqual(c.WarnTicks, DodgeDirector.WarnTicksAt(1f, c));
            Assert.Less(DodgeDirector.WarnTicksAt(1.3f, c), c.WarnTicks);
            Assert.AreEqual(c.MinWarnTicks, DodgeDirector.WarnTicksAt(100f, c));
        }

        [Test]
        public void 세기가_오르면_개수가_는다()
        {
            Assert.AreEqual(8, DodgeDirector.RainCount(1f));   // 4b: 슬리퍼가 느려져(4 m/s) 오래 남는 만큼 줄였다
            Assert.Greater(DodgeDirector.RainCount(2f), DodgeDirector.RainCount(1f));
            Assert.AreEqual(40, DodgeDirector.RainCount(100f));
            Assert.AreEqual(3f, DodgeDirector.WallGap(1f), 1e-5f);
            Assert.AreEqual(1.8f, DodgeDirector.WallGap(100f), 1e-5f);
            Assert.AreEqual(1, DodgeDirector.LaserCount(1.2f));
            Assert.AreEqual(3, DodgeDirector.LaserCount(100f));
            Assert.AreEqual(1, DodgeDirector.RockCount(1.2f));
            Assert.AreEqual(2, DodgeDirector.RockCount(1.6f));
        }

        [Test]
        public void 예고가_있는_패턴에는_그때의_예고_길이를_싣는다()
        {
            var c = Config((int)DodgePatternKind.Bomb);
            var got = Run(new DodgeDirector(1UL, c, Flat()), 0, c.FirstPatternDelayTicks, 0);
            Assert.AreEqual(Mathf.Max(DodgeDirector.WarnTicksAt(1f, c), DodgeDirector.BombMinWarnTicks), got[0].WarnTicks);
        }

        // 온돌 예고는 1.2초 밑으로 안 내려간다 — 0.8초로는 반응 후 옆 칸(최대 ~1.5m)까지 못 간다(4b 측정: 온돌에서 53% 탈락).
        [Test]
        public void 온돌_예고는_1점2초_밑으로_안_내려간다()
        {
            var c = Config((int)DodgePatternKind.Tiles);
            var stages = new DodgeStageTable(new[] { new DodgeStage("온돌", 60f, new[] { DodgePatternKind.Tiles }, 3f, 0f, 1.6f) });
            var got = Run(new DodgeDirector(1UL, c, stages), 0, c.FirstPatternDelayTicks, 0);
            Assert.AreEqual(60, DodgeDirector.TileMinWarnTicks);
            Assert.GreaterOrEqual(got[0].WarnTicks, DodgeDirector.TileMinWarnTicks);
        }

        // 수박 예고는 세기가 올라도 1.5초 밑으로 안 내려간다 — 0.8초 하한으로는 반응 + 2m 탈출이 안 된다.
        [Test]
        public void 폭탄_예고는_1점5초_밑으로_안_내려간다()
        {
            var c = Config((int)DodgePatternKind.Bomb);
            var stages = new DodgeStageTable(new[] { new DodgeStage("수박", 60f, new[] { DodgePatternKind.Bomb }, 3f, 0f, 1.6f) });
            var got = Run(new DodgeDirector(1UL, c, stages), 0, c.FirstPatternDelayTicks, 0);
            Assert.AreEqual(75, DodgeDirector.BombMinWarnTicks);
            Assert.GreaterOrEqual(got[0].WarnTicks, DodgeDirector.BombMinWarnTicks);
        }
    }
}
