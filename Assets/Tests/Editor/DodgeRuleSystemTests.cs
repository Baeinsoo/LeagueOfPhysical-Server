using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    //  몸을 어디에 세우나. 맵이 자리를 정하고, 자리가 없거나 모자라도 판이 멈추면 안 된다.
    public class DodgeRuleSystemTests
    {
        [Test]
        public void 자리가_있으면_순서대로_쓴다()
        {
            var slots = new List<Vector3> { new Vector3(1, 0, 0), new Vector3(2, 0, 0) };
            Assert.AreEqual(slots[0], DodgeRuleSystem.SpawnPositionFor(slots, 0));
            Assert.AreEqual(slots[1], DodgeRuleSystem.SpawnPositionFor(slots, 1));
        }

        [Test]
        public void 사람이_자리보다_많으면_돌려_쓴다()
        {
            var slots = new List<Vector3> { new Vector3(1, 0, 0), new Vector3(2, 0, 0) };
            Assert.AreEqual(slots[0], DodgeRuleSystem.SpawnPositionFor(slots, 2));
        }

        [Test]
        public void 자리가_없으면_바닥에_겹치지_않게_줄세운다()
        {
            var slots = new List<Vector3>();
            Vector3 a = DodgeRuleSystem.SpawnPositionFor(slots, 0);
            Vector3 b = DodgeRuleSystem.SpawnPositionFor(slots, 1);
            Assert.AreEqual(0f, a.y);
            Assert.AreEqual(0f, b.y);
            Assert.Greater(Vector3.Distance(a, b), 1.5f);
        }
    }
}
