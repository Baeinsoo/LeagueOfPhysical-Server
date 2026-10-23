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
                new MountSettings(12f, 20f, 25f, 180f, 60f, 90f, 0.5f, 0.6f), 100, 34, 0.5f);
            Assert.AreEqual(7500L, cfg.MatchDurationTicks(0.02f));
        }
    }
}
