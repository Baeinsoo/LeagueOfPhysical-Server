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

        [Test]
        public void 결과는_연습이_아니다()
        {
            var cfg = new KnockbackConfig(120f, 6f, 10f, 12f, 2f, 0.5f, 5f, 11f, 30f, 18f, 0.3f, 0.55f, 3, 0.95f, 0.26f, 1.2f, 1, 6f, 0.5f, 0.5f, 0.3f, 0.1f, 0.2f, 0.8f, 5f, new System.Collections.Generic.List<MountedArcheryZoneStageConfig>());
            var rule = new KnockbackRuleSystem(null, null, new KnockbackMatchState(), cfg);
            Assert.IsFalse(rule.ResolveOutcome().practice);
        }

        [Test]
        public void 판_길이는_120초()
        {
            var cfg = new KnockbackConfig(120f, 6f, 10f, 12f, 2f, 0.5f, 5f, 11f, 30f, 18f, 0.3f, 0.55f, 3, 0.95f, 0.26f, 1.2f, 1, 6f, 0.5f, 0.5f, 0.3f, 0.1f, 0.2f, 0.8f, 5f, new System.Collections.Generic.List<MountedArcheryZoneStageConfig>());
            Assert.AreEqual(6000L, cfg.MatchDurationTicks(0.02f));
        }
    }
}
