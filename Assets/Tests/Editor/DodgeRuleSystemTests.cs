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

        // 한 발 승부와 같은 치비 — 클라가 같은 키로 원격 그룹에서 받는다.
        [Test]
        public void 선수는_한_발_승부와_같은_치비다() =>
            Assert.AreEqual("Assets/Characters/Chibi/Chibi.prefab", DodgeRuleSystem.BodyVisualId);

        // 마지막 탈락 뒤 여운 — 자막(2.5초)과 들것(2초)이 다 보인 뒤에 결과로 넘어간다. 바로 끝내면 2인 판에서는 탈락 연출을 못 본다.
        [Test]
        public void 마지막_탈락_뒤_여운이_지나야_끝난다()
        {
            Assert.IsFalse(DodgeRuleSystem.MatchOver(2, 1, 100, 100));
            Assert.IsFalse(DodgeRuleSystem.MatchOver(2, 1, 100, 100 + DodgeRuleSystem.EndGraceTicks - 1));
            Assert.IsTrue(DodgeRuleSystem.MatchOver(2, 1, 100, 100 + DodgeRuleSystem.EndGraceTicks));
            Assert.GreaterOrEqual(DodgeRuleSystem.EndGraceTicks, 125);   // 자막 한 줄 2.5초(50Hz)
        }

        [Test]
        public void 둘_이상_살아_있거나_혼자_들어온_판은_안_끝난다()
        {
            Assert.IsFalse(DodgeRuleSystem.MatchOver(2, 2, -1, 10000));
            Assert.IsFalse(DodgeRuleSystem.MatchOver(1, 0, 100, 10000));
        }
    }
}
