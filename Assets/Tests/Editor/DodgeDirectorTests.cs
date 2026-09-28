using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class DodgeDirectorTests
    {
        static DodgeConfig Config(int onlyKind = 0) => new DodgeConfig(3, 1.5f, 0.16f, 0.5f, 9f, 6, 2f, 1.8f, onlyKind,
                                                                       1.2f, 5f, 0.22f, 2f, 0.25f, 0.7f, 0.45f, 6f, 1.35f, 0.55f);
        static readonly Vector2[] Two = { new Vector2(1, 1), new Vector2(-2, 3) };

        static List<DodgePattern> Run(DodgeDirector d, long from, long to, long start)
        {
            var all = new List<DodgePattern>();
            for (long t = from; t <= to; t++) d.Next(t, start, Two, all);
            return all;
        }

        [Test]
        public void 경기_시작_전에는_아무것도_안_낸다()
        {
            var d = new DodgeDirector(1UL, Config());
            Assert.AreEqual(0, Run(d, 0, 500, long.MaxValue).Count);
        }

        [Test]
        public void 시작_뒤_첫_지연이_지나야_내고_예약_시간만큼_뒤에_시작한다()
        {
            var c = Config();
            var d = new DodgeDirector(1UL, c);
            var got = Run(d, 100, 100 + c.FirstPatternDelayTicks, 100);
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual(100 + c.FirstPatternDelayTicks + c.LeadTicks, got[0].StartTick);
        }

        [Test]
        public void 같은_씨앗이면_같은_순서로_낸다()
        {
            var a = Run(new DodgeDirector(42UL, Config()), 0, 1000, 0);
            var b = Run(new DodgeDirector(42UL, Config()), 0, 1000, 0);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Kind, b[i].Kind);
                Assert.AreEqual(a[i].Seed, b[i].Seed);
                Assert.AreEqual(a[i].P0, b[i].P0);
                Assert.AreEqual(a[i].StartTick, b[i].StartTick);
            }
        }

        [Test]
        public void 폭탄은_산_사람_발밑마다_하나씩이다()
        {
            var d = new DodgeDirector(1UL, Config((int)DodgePatternKind.Bomb));
            var got = Run(d, 0, Config().FirstPatternDelayTicks, 0);
            Assert.AreEqual(2, got.Count);
            Assert.AreEqual(1f, got[0].P0);
            Assert.AreEqual(1f, got[0].P1);
            Assert.AreEqual(-2f, got[1].P0);
        }

        [Test]
        public void 조준탄은_산_사람_중_하나를_노린다()
        {
            var d = new DodgeDirector(7UL, Config((int)DodgePatternKind.BulletAimed));
            var got = Run(d, 0, Config().FirstPatternDelayTicks, 0);
            Assert.AreEqual(1, got.Count);
            var target = new Vector2(got[0].P2, got[0].P3);
            Assert.IsTrue(target == Two[0] || target == Two[1]);
        }

        [Test]
        public void 종류를_돌아가며_내고_아이디는_겹치지_않는다()
        {
            var got = Run(new DodgeDirector(3UL, Config()), 0, 2000, 0);
            var kinds = new HashSet<DodgePatternKind>();
            var ids = new HashSet<int>();
            foreach (var p in got) { kinds.Add(p.Kind); Assert.IsTrue(ids.Add(p.Id)); }
            Assert.AreEqual(7, kinds.Count);
        }
    }
}
