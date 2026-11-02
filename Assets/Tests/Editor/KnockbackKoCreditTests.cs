using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    public class KnockbackKoCreditTests
    {
        const float Dt = 0.02f;
        static long Ticks(float seconds) => (long)System.Math.Round(seconds / Dt);

        static readonly Dictionary<string, int> NoTeams = new Dictionary<string, int>();

        static string Ko(KnockbackKoCredit credit, string id, long tick, out bool chain)
        {
            credit.Resolve(id, tick, out var by, out chain);
            return by;
        }

        [Test]
        public void 맞고_3초_뒤_떨어지면_쏜_사람()
        {
            var c = new KnockbackKoCredit(Dt);
            c.RecordShotHit("b", "a", 100);
            Assert.AreEqual("a", Ko(c, "b", 100 + Ticks(3f), out var chain));
            Assert.IsFalse(chain);
        }

        [Test]
        public void 맞고_7초_뒤_떨어지면_자멸()
        {
            var c = new KnockbackKoCredit(Dt);
            c.RecordShotHit("b", "a", 100);
            Assert.AreEqual("", Ko(c, "b", 100 + Ticks(7f), out _));
        }

        [Test]
        public void 정확히_6초는_아직_유효하고_한_틱_넘으면_자멸()
        {
            var c = new KnockbackKoCredit(Dt);
            c.RecordShotHit("b", "a", 100);
            Assert.AreEqual("a", Ko(c, "b", 100 + Ticks(6f), out _));
            Assert.AreEqual("", Ko(c, "b", 100 + Ticks(6f) + 1, out _));
        }

        [Test]
        public void 아무도_안_맞혔으면_자멸()
        {
            var c = new KnockbackKoCredit(Dt);
            Assert.AreEqual("", Ko(c, "b", 500, out var chain));
            Assert.IsFalse(chain);
        }

        [Test]
        public void 나중_맞음이_앞_기록을_덮는다()
        {
            var c = new KnockbackKoCredit(Dt);
            c.RecordShotHit("c", "a", 100);
            c.RecordShotHit("c", "b", 200);
            Assert.AreEqual("b", Ko(c, "c", 250, out _));
        }

        [Test]
        public void 연쇄는_출처의_크레딧을_물려받고_연쇄로_표시한다()
        {
            var c = new KnockbackKoCredit(Dt);
            c.RecordShotHit("b", "a", 100);
            c.ApplyChains(new[] { ("c", "b") }, 120, NoTeams);
            Assert.AreEqual("a", Ko(c, "c", 150, out var chain));
            Assert.IsTrue(chain);
        }

        [Test]
        public void 연쇄_출처에_크레딧이_없으면_자멸()
        {
            var c = new KnockbackKoCredit(Dt);
            c.ApplyChains(new[] { ("c", "b") }, 120, NoTeams);
            Assert.AreEqual("", Ko(c, "c", 150, out var chain));
            Assert.IsFalse(chain);
        }

        [Test]
        public void 연쇄_크레딧_시계는_부딪힌_틱부터_다시_센다()
        {
            var c = new KnockbackKoCredit(Dt);
            c.RecordShotHit("b", "a", 100);
            c.ApplyChains(new[] { ("c", "b") }, 100 + Ticks(5f), NoTeams);
            Assert.AreEqual("a", Ko(c, "c", 100 + Ticks(10f), out _), "처음 맞음에서 10초지만 부딪힌 뒤 5초다");
        }

        [Test]
        public void 낡은_크레딧은_물려주지_않는다()
        {
            var c = new KnockbackKoCredit(Dt);
            c.RecordShotHit("b", "a", 100);
            c.ApplyChains(new[] { ("c", "b") }, 100 + Ticks(8f), NoTeams);
            Assert.AreEqual("", Ko(c, "c", 100 + Ticks(9f), out _));
        }

        [Test]
        public void 물려받을_사람이_같은_팀이면_기록하지_않는다()
        {
            var teams = new Dictionary<string, int> { ["a"] = 1, ["c"] = 1, ["b"] = 2 };
            var c = new KnockbackKoCredit(Dt);
            c.RecordShotHit("b", "a", 100);
            c.ApplyChains(new[] { ("c", "b") }, 120, teams);
            Assert.AreEqual("", Ko(c, "c", 150, out _));

            //  양성 대조 - 다른 팀이면 같은 연쇄가 기록된다
            teams["c"] = 2;
            c.ApplyChains(new[] { ("c", "b") }, 120, teams);
            Assert.AreEqual("a", Ko(c, "c", 150, out _));
        }

        [Test]
        public void 자기가_쏜_사람이_자기_연쇄로_되돌아오면_기록하지_않는다()
        {
            var c = new KnockbackKoCredit(Dt);
            c.RecordShotHit("b", "a", 100);
            c.ApplyChains(new[] { ("a", "b") }, 120, NoTeams);
            Assert.AreEqual("", Ko(c, "a", 150, out _));
        }

        [Test]
        public void 연쇄_뒤_탄에_다시_맞으면_탄_기록이_덮는다()
        {
            var c = new KnockbackKoCredit(Dt);
            c.RecordShotHit("b", "a", 100);
            c.ApplyChains(new[] { ("c", "b") }, 120, NoTeams);
            c.RecordShotHit("c", "d", 130);
            Assert.AreEqual("d", Ko(c, "c", 150, out var chain));
            Assert.IsFalse(chain);
        }

        [Test]
        public void 같은_묶음에서_앞_항목이_쓴_장부를_뒤_항목이_출처로_읽지_않는다()
        {
            var c = new KnockbackKoCredit(Dt);
            c.RecordShotHit("b", "a", 100);
            c.ApplyChains(new[] { ("c", "b"), ("d", "c") }, 120, NoTeams);
            Assert.AreEqual("a", Ko(c, "c", 150, out _), "양성 대조 - 앞 항목은 기록된다");
            Assert.AreEqual("", Ko(c, "d", 150, out _), "d의 출처 c는 묶음 시작 때 크레딧이 없었다");
        }

        [Test]
        public void 같은_묶음의_서로_맞물린_연쇄는_둘_다_묶음_시작_장부로_읽는다()
        {
            var c = new KnockbackKoCredit(Dt);
            c.RecordShotHit("b", "x", 100);
            c.RecordShotHit("c", "y", 100);
            c.ApplyChains(new[] { ("b", "c"), ("c", "b") }, 120, NoTeams);
            Assert.AreEqual("y", Ko(c, "b", 150, out _));
            Assert.AreEqual("x", Ko(c, "c", 150, out _));
        }

        [Test]
        public void 상태의_Eliminate가_장외시킨_사람과_연쇄를_남긴다()
        {
            var state = new KnockbackMatchState();
            state.Players.Add("a"); state.Players.Add("b"); state.Players.Add("c");
            state.Credit.RecordShotHit("b", "a", 100);
            state.Credit.ApplyChains(new[] { ("c", "b") }, 120, state.Team);
            state.Eliminate("c", 150);
            state.Eliminate("b", 160);
            state.Eliminate("a", 170);

            Assert.AreEqual("a", state.KoBy["c"]);
            Assert.IsTrue(state.KoChain.Contains("c"));
            Assert.AreEqual("a", state.KoBy["b"]);
            Assert.IsFalse(state.KoChain.Contains("b"));
            Assert.IsFalse(state.KoBy.ContainsKey("a"), "자멸은 항목이 없다");
        }
    }
}
