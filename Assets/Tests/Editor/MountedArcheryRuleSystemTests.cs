using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class MountedArcheryRuleSystemTests
    {
        [Test]
        public void 가운데를_본다()
        {
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, MountedArcheryRuleSystem.YawFacingCenter(new Vector3(0, 0, -10))), 0.01f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(-90f, MountedArcheryRuleSystem.YawFacingCenter(new Vector3(10, 0, 0))), 0.01f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(180f, MountedArcheryRuleSystem.YawFacingCenter(new Vector3(0, 0, 10))), 0.01f);
        }

        [Test]
        public void 판_길이는_150초()
        {
            var cfg = new MountedArcheryConfig(150f, 10f, 30f, 2.5f,
                new MountSettings(12f, 20f, 25f, 180f, 60f, 90f, 0.5f, 0.6f, 3f), 100, 34, 0.5f, 100f, 30f, null, 4f, 90f, 0.6f, 0.5f, 15f, 2.5f, 0.2f, 0.4f, 60f);
            Assert.AreEqual(7500L, cfg.MatchDurationTicks(0.02f));
        }

        private static MountedArcheryMatchState TwoPlayers()
        {
            var st = new MountedArcheryMatchState();
            st.Add("e1", "u1", "하나");
            st.Add("e2", "u2", "둘");
            return st;
        }

        private static MountedArcheryRuleSystem RuleOf(MountedArcheryMatchState st)
            => new MountedArcheryRuleSystem(null, null, null, st);

        [Test]
        public void 한_명_남고_유예가_지나면_끝난다()
        {
            var st = TwoPlayers();
            var rule = RuleOf(st);
            st.Eliminate("e2", "e1", 100);
            st.LastTick = 100 + 149;
            Assert.IsFalse(rule.IsMatchOver);
            st.LastTick = 100 + 150;
            Assert.IsTrue(rule.IsMatchOver);
        }

        [Test]
        public void 순위는_탈락_역순_같은_틱은_공동()
        {
            var st = new MountedArcheryMatchState();
            st.Add("e1", "u1", "a"); st.Add("e2", "u2", "b"); st.Add("e3", "u3", "c"); st.Add("e4", "u4", "d");
            st.Eliminate("e1", "e4", 10);
            st.Eliminate("e2", "e4", 20);
            st.Eliminate("e3", "e4", 20);
            var rank = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var p in RuleOf(st).ResolveOutcome().placements) rank[p.userId] = p.placement;
            Assert.AreEqual(1, rank["u4"]);
            Assert.AreEqual(2, rank["u2"]);
            Assert.AreEqual(2, rank["u3"]);
            Assert.AreEqual(4, rank["u1"]);
        }

        [Test]
        public void 탈락하면_결과가_정해진다()
        {
            var st = TwoPlayers();
            var rule = RuleOf(st);
            Assert.IsFalse(rule.IsResultSettled("u2"));
            st.Eliminate("e2", "e1", 5);
            Assert.IsTrue(rule.IsResultSettled("u2"));
            Assert.IsFalse(rule.IsResultSettled("u1"));
            Assert.IsFalse(rule.IsResultSettled("nobody"));
        }

        [Test]
        public void ToWire는_살아_있으면_탈락틱_마이너스1()
        {
            var st = TwoPlayers();
            st.Eliminate("e2", "e1", 7);
            var wire = MountedArcheryStateBroadcastSystem.ToWire(st);
            Assert.AreEqual(st.Version, wire.Version);
            foreach (var p in wire.Players)
            {
                if (p.EntityId == "e1") { Assert.AreEqual(-1L, p.EliminatedTick); Assert.AreEqual("하나", p.DisplayName); }
                else { Assert.AreEqual(7L, p.EliminatedTick); Assert.AreEqual("e1", p.KillerEntityId); }
            }
        }
    }
}
