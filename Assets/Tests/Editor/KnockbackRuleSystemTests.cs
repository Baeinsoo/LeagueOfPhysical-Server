using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class KnockbackRuleSystemTests
    {
        [Test]
        public void 자리는_원_위에서_가운데를_본다()
        {
            var p0 = KnockbackSpawn.Position(0, 8, 11f);
            Assert.AreEqual(0f, p0.x, 1e-4f);
            Assert.AreEqual(11f, p0.z, 1e-4f);

            var p2 = KnockbackSpawn.Position(2, 8, 11f);
            Assert.AreEqual(11f, p2.x, 1e-4f);
            Assert.AreEqual(0f, p2.z, 1e-4f);

            Assert.AreEqual(0f, Mathf.DeltaAngle(180f, KnockbackSpawn.YawFacingCenter(new Vector3(0, 0, 11))), 0.01f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(-90f, KnockbackSpawn.YawFacingCenter(new Vector3(11, 0, 0))), 0.01f);
        }

        [Test]
        public void 혼자면_떨어져야_끝난다()
        {
            Assert.IsFalse(DodgeRuleSystem.MatchOver(1, 1, -1, 1000));
            Assert.IsTrue(DodgeRuleSystem.MatchOver(1, 0, 100, 100 + 150));
        }

        [Test]
        public void 둘이면_한_명_남으면_끝난다()
        {
            Assert.IsTrue(DodgeRuleSystem.MatchOver(2, 1, 100, 250));
            Assert.IsFalse(DodgeRuleSystem.MatchOver(2, 2, -1, 9999));
        }

        //  사람이 혼자인 판은 겨룬 상대가 없어 보상을 주지 않는다(사용자 결정 10-31). 봇이 낀 판은
        //  MatchEndedMessageBuilder.IsPractice가 따로 연습으로 본다.
        [Test]
        public void 사람이_둘_이상이어야_보상_판이다()
        {
            Assert.IsTrue(KnockbackRuleSystem.IsPractice(0));
            Assert.IsTrue(KnockbackRuleSystem.IsPractice(1));
            Assert.IsFalse(KnockbackRuleSystem.IsPractice(2));
            Assert.IsFalse(KnockbackRuleSystem.IsPractice(8));
        }

        [Test]
        public void 사람이_없는_판의_결과는_연습이다()
        {
            var cfg = new KnockbackConfig(120f, 6f, 10f, 12f, 2f, 0.5f, 5f, 11f, 30f, 18f, 0.3f, 0.55f, 3, 0.95f, 0.26f, 1.2f, 1, 6f, 0.5f, 0.5f, 0.3f, 0.1f, 0.2f, 0.8f, 5f, new System.Collections.Generic.List<MountedArcheryZoneStageConfig>());
            var rule = new KnockbackRuleSystem(null, null, new KnockbackMatchState(), cfg);
            Assert.IsTrue(rule.ResolveOutcome().practice);
        }

        [Test]
        public void 판_길이는_120초()
        {
            var cfg = new KnockbackConfig(120f, 6f, 10f, 12f, 2f, 0.5f, 5f, 11f, 30f, 18f, 0.3f, 0.55f, 3, 0.95f, 0.26f, 1.2f, 1, 6f, 0.5f, 0.5f, 0.3f, 0.1f, 0.2f, 0.8f, 5f, new System.Collections.Generic.List<MountedArcheryZoneStageConfig>());
            Assert.AreEqual(6000L, cfg.MatchDurationTicks(0.02f));
        }

        static System.Collections.Generic.Dictionary<string, int> FourPlayers() => new System.Collections.Generic.Dictionary<string, int>
        {
            ["a"] = 1, ["b"] = 2, ["c"] = 1, ["d"] = 2,
        };

        static int PlacementOf(MatchOutcome o, string user)
        {
            foreach (var p in o.placements) if (p.userId == user) return p.placement;
            return -1;
        }

        [Test]
        public void 팀전은_남은_팀_전원이_1위_나머지는_2위()
        {
            var o = KnockbackRuleSystem.TeamOutcome(FourPlayers(), new[] { "a", "c" });
            Assert.AreEqual(1, PlacementOf(o, "a"));
            Assert.AreEqual(1, PlacementOf(o, "c"));
            Assert.AreEqual(2, PlacementOf(o, "b"));
            Assert.AreEqual(2, PlacementOf(o, "d"));
        }

        [Test]
        public void 팀전에서_동시에_다_떨어지면_전원_1위()
        {
            var o = KnockbackRuleSystem.TeamOutcome(FourPlayers(), new string[0]);
            foreach (var id in new[] { "a", "b", "c", "d" }) Assert.AreEqual(1, PlacementOf(o, id));
        }

        [Test]
        public void 사람1_봇7이면_4대4로_나뉜다()
        {
            int one = 0, two = 0;
            for (int seat = 0; seat < 8; seat++)
            {
                if (KnockbackTeams.TeamForSeat(seat, 2) == 1) one++; else two++;
            }
            Assert.AreEqual(4, one);
            Assert.AreEqual(4, two);
        }

        [Test]
        public void 내가_떨어져도_우리_팀이_살아_있으면_안_끝난다()
        {
            Assert.IsFalse(KnockbackTeams.TeamMatchOver(FourPlayers(), new[] { "c", "b", "d" }));
        }
    }
}
