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
            Assert.AreEqual(2, got.Count);   // 첫 종류는 링 — 조준 연사가 같이 나온다
            Assert.AreEqual(got[0].StartTick, got[1].StartTick);
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
            Assert.AreEqual(DodgeReferee.Spot, new Vector2(got[0].P0, got[0].P1));   // 가운데 투척기에서 쏜다
        }

        // 탄막은 가운데 투척기에서 — 링은 엇갈리는 N개, 나선은 갈래·방향이 판마다 다르다.
        [Test]
        public void 링은_가운데에서_정해진_개수로_퍼진다()
        {
            var got = Run(new DodgeDirector(1UL, Config((int)DodgePatternKind.Ring), Flat()), 0, Config().FirstPatternDelayTicks, 0);
            Assert.AreEqual(DodgePatternKind.Ring, got[0].Kind);   // 둘째는 함께 나오는 조준 연사
            Assert.AreEqual(DodgeReferee.Spot, new Vector2(got[0].P0, got[0].P1));
            Assert.AreEqual(DodgeDirector.RingCount(1f), (int)got[0].P2);
            Assert.AreEqual((ulong)DodgeDirector.RingGap, got[0].Seed);   // 부채꼴 틈
        }

        [Test]
        public void 나선은_가운데에서_돌아간다()
        {
            bool cw = false, ccw = false;
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var got = Run(new DodgeDirector(seed, Config((int)DodgePatternKind.Spiral), Flat()), 0, Config().FirstPatternDelayTicks, 0);
                Assert.AreEqual(DodgeReferee.Spot, new Vector2(got[0].P0, got[0].P1));
                Assert.GreaterOrEqual(got[0].P2, 2f);
                cw |= got[0].P3 < 0f; ccw |= got[0].P3 > 0f;
            }
            Assert.IsTrue(cw && ccw, "도는 방향이 판마다 달라야 한다");
        }

        [Test]
        public void 링_개수는_세기에_따라_늘고_위가_막혀_있다()
        {
            Assert.Greater(DodgeDirector.RingCount(2f), DodgeDirector.RingCount(1f));
            Assert.AreEqual(DodgeDirector.RingCount(100f), DodgeDirector.RingCount(1000f));
        }

        [Test]
        public void 종류를_돌아가며_내고_아이디는_겹치지_않는다()
        {
            var got = Run(new DodgeDirector(3UL, Config(), Flat()), 0, 2000, 0);
            var kinds = new HashSet<DodgePatternKind>();
            var ids = new HashSet<int>();
            foreach (var p in got) { kinds.Add(p.Kind); Assert.IsTrue(ids.Add(p.Id)); }
            Assert.AreEqual(8, kinds.Count);   // 7종 + 링·나선에 붙는 조준 연사
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
            Assert.AreEqual(8, kinds.Count);   // 7종 + 링·나선에 붙는 조준 연사
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
            Assert.AreEqual(DodgeDirector.WarnTicksAt(1f, c), got[0].WarnTicks);
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

        // 수박 예고는 따로 하한이 없다 — 세기가 오르면 공통 최소 예고(0.8초)까지 줄어든다(처음 버전, 10-04 되돌림).
        [Test]
        public void 폭탄_예고는_공통_하한까지_줄어든다()
        {
            var c = Config((int)DodgePatternKind.Bomb);
            var stages = new DodgeStageTable(new[] { new DodgeStage("수박", 60f, new[] { DodgePatternKind.Bomb }, 3f, 0f, 1.6f) });
            var got = Run(new DodgeDirector(1UL, c, stages), 0, c.FirstPatternDelayTicks, 0);
            Assert.AreEqual(c.MinWarnTicks, got[0].WarnTicks);
        }

        // ── 패턴 v2(업계 대조 — 조사 보고: 온돌 겹침, 조준 없음, 고정+조준 동시, 추격 장판) ──

        static DodgeStageTable Only(DodgePatternKind k, float intensity) =>
            new DodgeStageTable(new[] { new DodgeStage("한 종류", 600f, new[] { k }, intensity, 0f, 1.6f) });

        static bool Passes(in DodgePattern laser, Vector2 p) =>
            DodgeGeometry.SegmentDistance(p, p, new Vector2(laser.P0, laser.P1), new Vector2(laser.P2, laser.P3)) < 1e-3f;

        // 바닥 경고가 겹치면 바닥 전체가 경고색이 돼 못 읽는다 — 앞 판이 꺼진 뒤에만 다음 예고.
        [Test]
        public void 온돌은_한_번에_한_판만()
        {
            var c = Config();
            var got = Run(new DodgeDirector(3UL, c, Only(DodgePatternKind.Tiles, 3f)), 0, 3000, 0);
            Assert.Greater(got.Count, 3);
            for (int i = 1; i < got.Count; i++)
            {
                long prevEnd = got[i - 1].StartTick + DodgeHazards.LifetimeTicks(got[i - 1], c);
                Assert.Greater(got[i].StartTick, prevEnd, $"{i}번째 온돌이 앞 판과 겹친다");
            }
        }

        // 체크무늬(반) 대신 "안전 칸 N개만 남김" — 세기가 오르면 N이 준다.
        [Test]
        public void 온돌은_안전_칸만_남기고_세기에_따라_준다()
        {
            var c = Config();
            var got = Run(new DodgeDirector(3UL, c, Only(DodgePatternKind.Tiles, 1f)), 0, c.FirstPatternDelayTicks, 0);
            int hot = 0; for (int i = 0; i < 36; i++) if ((got[0].Seed & (1UL << i)) != 0) hot++;
            Assert.AreEqual(36 - DodgeDirector.SafeTiles(1f), hot);
            Assert.Less(DodgeDirector.SafeTiles(2f), DodgeDirector.SafeTiles(1f));
        }

        // "나를 노리는 게 없다" — 줄 하나는 반드시 산 사람 자리를 지난다.
        [Test]
        public void 줄넘기_첫_줄은_사람을_지난다()
        {
            for (ulong seed = 1; seed <= 6; seed++)
            {
                var got = Run(new DodgeDirector(seed, Config((int)DodgePatternKind.Laser), Flat()), 0, Config().FirstPatternDelayTicks, 0);
                Assert.IsTrue(Passes(got[0], Two[0]) || Passes(got[0], Two[1]), $"seed {seed}");
            }
        }

        // 장독은 사람 쪽으로 굴러오고, 둘째는 맞은편 벽에서 와 교차한다.
        [Test]
        public void 장독은_사람을_겨누고_둘째는_맞은편에서()
        {
            var c = Config();
            var got = Run(new DodgeDirector(5UL, c, Only(DodgePatternKind.Rock, 2f)), 0, c.FirstPatternDelayTicks, 0);
            Assert.AreEqual(2, got.Count);
            Assert.AreEqual(((int)got[0].P0 + 2) % 4, (int)got[1].P0);
            foreach (var r in got)
            {
                Vector2 start = DodgeHazards.EdgePoint((int)r.P0, r.P1, c.EdgeDistance);
                float best = float.MaxValue;
                foreach (var t in Two) best = Mathf.Min(best, Vector2.Angle(DodgeHazards.RockDirection(r), t - start));
                Assert.Less(best, 1f, "장독이 아무도 안 겨눈다");
            }
        }

        // 고정 탄막(링·나선) 위에 조준 연사를 겹친다 — 고정은 장애물, 조준은 압박.
        [Test]
        public void 링과_나선에는_조준_연사가_함께_나온다()
        {
            foreach (var k in new[] { DodgePatternKind.Ring, DodgePatternKind.Spiral })
            {
                var got = Run(new DodgeDirector(1UL, Config((int)k), Flat()), 0, Config().FirstPatternDelayTicks, 0);
                Assert.AreEqual(2, got.Count, k.ToString());
                Assert.AreEqual(DodgePatternKind.BulletStream, got[1].Kind);
                Assert.AreEqual(got[0].StartTick, got[1].StartTick);
                var target = new Vector2(got[1].P2, got[1].P3);
                Assert.IsTrue(target == Two[0] || target == Two[1]);
            }
        }

        // 수박은 처음 버전 — 산 사람마다 한 개, 정확히 발밑. 근처(0.5~1.8m)·하한 1.2~1.5초·추격 3연발을 차례로 해 봤지만
        // 사람 판에서 "여유가 없던" 처음 것이 낫다는 결론(10-04).
        [Test]
        public void 수박은_사람마다_발밑에_하나씩이다()
        {
            var c = Config((int)DodgePatternKind.Bomb);
            var got = Run(new DodgeDirector(1UL, c, Flat()), 0, c.FirstPatternDelayTicks + 60, 0);
            Assert.AreEqual(2, got.Count);
            Assert.AreEqual(Two[0], new Vector2(got[0].P0, got[0].P1));
            Assert.AreEqual(Two[1], new Vector2(got[1].P0, got[1].P1));
        }

        // ── 인원 비례(한 사람이 받는 압박을 인원과 무관하게) ──

        static readonly Vector2[] Eight =
        {
            new Vector2(4, 0), new Vector2(2.8f, 2.8f), new Vector2(0, 4), new Vector2(-2.8f, 2.8f),
            new Vector2(-4, 0), new Vector2(-2.8f, -2.8f), new Vector2(0, -4), new Vector2(2.8f, -2.8f),
        };

        static List<DodgePattern> RunWith(DodgeDirector d, Vector2[] alive, long to)
        {
            var all = new List<DodgePattern>();
            for (long t = 0; t <= to; t++) d.Next(t, 0, alive, all);
            return all;
        }

        [Test]
        public void 노리는_사람_수는_네_명당_한_명()
        {
            Assert.AreEqual(1, DodgeDirector.TargetCount(1));
            Assert.AreEqual(1, DodgeDirector.TargetCount(4));
            Assert.AreEqual(2, DodgeDirector.TargetCount(5));
            Assert.AreEqual(2, DodgeDirector.TargetCount(8));
        }

        // 8명이면 조준 연사 두 줄, 서로 다른 사람에게 — 각자 노려지는 빈도가 혼자일 때와 비슷하게.
        [Test]
        public void 여덟_명이면_조준_연사가_두_사람을_노린다()
        {
            var got = RunWith(new DodgeDirector(1UL, Config((int)DodgePatternKind.Ring), Flat()), Eight, Config().FirstPatternDelayTicks);
            var streams = got.FindAll(p => p.Kind == DodgePatternKind.BulletStream);
            Assert.AreEqual(2, streams.Count);
            Assert.AreNotEqual(new Vector2(streams[0].P2, streams[0].P3), new Vector2(streams[1].P2, streams[1].P3));
        }

        [Test]
        public void 여덟_명이면_줄넘기_두_줄이_서로_다른_사람을_지난다()
        {
            var got = RunWith(new DodgeDirector(2UL, Config((int)DodgePatternKind.Laser), Flat()), Eight, Config().FirstPatternDelayTicks);
            Assert.GreaterOrEqual(got.Count, 2);
            int a = -1, b = -1;
            for (int i = 0; i < Eight.Length; i++) { if (Passes(got[0], Eight[i]) && a < 0) a = i; }
            for (int i = 0; i < Eight.Length; i++) { if (Passes(got[1], Eight[i]) && i != a && b < 0) b = i; }
            Assert.IsTrue(a >= 0 && b >= 0, "두 줄이 서로 다른 사람을 지나야 한다");
        }

        // 수박은 한 번에 최대 MaxBombs개 — 8명 모두에게 떨어뜨리면 경기장이 덮인다. 대상은 돌아가며 모두에게.
        [Test]
        public void 수박은_한_번에_셋까지_돌아가며_모두에게()
        {
            var c = Config((int)DodgePatternKind.Bomb);
            var d = new DodgeDirector(1UL, c, Flat());
            var got = RunWith(d, Eight, c.FirstPatternDelayTicks + 3 * DodgeDirector.IntervalTicksAt(Flat().At(0, 0, c), c));
            var byStart = new Dictionary<long, int>();
            var hit = new HashSet<Vector2>();
            foreach (var p in got) { byStart[p.StartTick] = byStart.TryGetValue(p.StartTick, out var n) ? n + 1 : 1; hit.Add(new Vector2(p.P0, p.P1)); }
            foreach (var kv in byStart) Assert.LessOrEqual(kv.Value, DodgeDirector.MaxBombs);
            Assert.AreEqual(8, hit.Count, "세 번 고르면(3×3) 여덟 명 모두가 한 번씩은 노려진다");
        }

        // 무작위 안전 칸은 내 근처에 하나도 없을 수 있다(스테이지 5 끝 16%, 서든데스 25% — 예고 본 뒤엔 못 닿음).
        // 검사기는 다음 안전 칸을 미리 아는 사람이라 이걸 못 잡는다. 산 사람마다 닿는 거리 안에 안전 칸을 하나 보장한다
        // — 지금 선 칸은 빼서 매번 한 칸은 움직이게.
        [Test]
        public void 온돌은_사람마다_닿는_거리_안에_안전_칸이_있다()
        {
            var c = Config();
            int n = c.TileCount; float size = c.ArenaHalf * 2f / n;
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var got = RunWith(new DodgeDirector(seed, c, Only(DodgePatternKind.Tiles, 3f)), Eight, 2000);
                foreach (var t in got)
                {
                    float reach = DodgeDirector.TileReach(t.WarnTicks);
                    foreach (var p in Eight)
                    {
                        int own = Mathf.Clamp((int)((p.x + c.ArenaHalf) / size), 0, n - 1) + Mathf.Clamp((int)((p.y + c.ArenaHalf) / size), 0, n - 1) * n;
                        bool ok = false;
                        for (int i = 0; i < n * n && !ok; i++)
                        {
                            if ((t.Seed & (1UL << i)) != 0 || i == own) continue;   // 뜨거운 칸, 지금 선 칸은 보장으로 안 친다
                            float x0 = -c.ArenaHalf + (i % n) * size, z0 = -c.ArenaHalf + (i / n) * size;
                            float dx = Mathf.Max(x0 - p.x, 0f, p.x - (x0 + size)), dz = Mathf.Max(z0 - p.y, 0f, p.y - (z0 + size));
                            ok = Mathf.Sqrt(dx * dx + dz * dz) <= reach;
                        }
                        Assert.IsTrue(ok, $"seed {seed} tick {t.StartTick}: {p} 근처에 닿는 안전 칸이 없다");
                    }
                }
            }
        }

        // ── 예측 조준(가끔) — 계속 뛰면 고른 순간 자리를 노리는 패턴은 늘 뒤에 떨어진다(사람 판 소감) ──

        static readonly Vector2[] TwoVel = { new Vector2(3f, 0f), new Vector2(0f, -3f) };

        static List<DodgePattern> RunV(DodgeDirector d, long to)
        {
            var all = new List<DodgePattern>();
            for (long t = 0; t <= to; t++) d.Next(t, 0, Two, TwoVel, all);
            return all;
        }

        static Vector2 Clamp(Vector2 v, float h) => new Vector2(Mathf.Clamp(v.x, -h, h), Mathf.Clamp(v.y, -h, h));

        // LeadEvery번에 한 번은 그 사람이 지금 속도로 가면 터질 때 있을 자리에 떨어진다. 나머지는 지금 자리.
        [Test]
        public void 수박은_가끔_가는_쪽을_내다보고_떨어진다()
        {
            var c = Config((int)DodgePatternKind.Bomb);
            int interval = DodgeDirector.IntervalTicksAt(Flat().At(0, 0, c), c);
            var got = RunV(new DodgeDirector(1UL, c, Flat()), c.FirstPatternDelayTicks + interval * (DodgeDirector.LeadEvery - 1));
            int predicted = 0, plain = 0;
            foreach (var b in got)
            {
                var at = new Vector2(b.P0, b.P1);
                for (int i = 0; i < 2; i++)
                {
                    float lead = (c.LeadTicks + b.WarnTicks) / (float)DodgeConfig.TicksPerSecond;
                    if ((at - Two[i]).sqrMagnitude < 1e-6f) plain++;
                    else if ((at - Clamp(Two[i] + TwoVel[i] * lead, c.ArenaHalf - 0.5f)).sqrMagnitude < 1e-4f) predicted++;
                }
            }
            Assert.AreEqual(2, predicted, "한 번 고를 때 두 사람 모두 내다본다");
            Assert.AreEqual(2 * (DodgeDirector.LeadEvery - 1), plain);
        }

        [Test]
        public void 멈춰_있으면_내다봐도_지금_자리다()
        {
            var c = Config((int)DodgePatternKind.Bomb);
            int interval = DodgeDirector.IntervalTicksAt(Flat().At(0, 0, c), c);
            var got = Run(new DodgeDirector(1UL, c, Flat()), 0, c.FirstPatternDelayTicks + interval * DodgeDirector.LeadEvery, 0);
            foreach (var b in got) Assert.IsTrue(new Vector2(b.P0, b.P1) == Two[0] || new Vector2(b.P0, b.P1) == Two[1]);
        }

        // 장독 둘째는 첫째를 피한 뒤 다시 피할 시간을 둔다(7.6% 못 닿음 → 교차가 너무 빠듯했다).
        [Test]
        public void 장독_둘째는_넉넉히_뒤에_온다()
        {
            var c = Config();
            var got = Run(new DodgeDirector(5UL, c, Only(DodgePatternKind.Rock, 2f)), 0, c.FirstPatternDelayTicks, 0);
            Assert.AreEqual(2, got.Count);
            Assert.GreaterOrEqual(got[1].StartTick - got[0].StartTick, 50);
            Assert.GreaterOrEqual(got[0].WarnTicks, DodgeDirector.RockMinWarnTicks);   // 세기 2라도 예고 1초 — 반경 1.51m를 빠져나갈 시간
        }
    }
}
