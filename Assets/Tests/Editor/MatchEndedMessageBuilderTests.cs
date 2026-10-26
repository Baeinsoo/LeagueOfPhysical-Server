using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>결과 메시지 — 등수마다 그 선수의 룩·이름·레벨을 싣는다(결과 단계 시상대). 점수 변화는 본인 것만.</summary>
    public class MatchEndedMessageBuilderTests
    {
        private static ConfirmedParticipantDto P(string id, int place) =>
            new ConfirmedParticipantDto { userId = id, placement = place, mmrBefore = 1000, mmrAfter = 1010 };

        [Test]
        public void 등수마다_룩_이름_레벨을_싣는다()
        {
            var looks = new Dictionary<string, PlayerLookDto>
            {
                ["a"] = new PlayerLookDto { slots = new Dictionary<string, string> { ["hat"] = "hat_01" }, displayName = "가나#1", level = 7 },
            };
            var msg = MatchEndedMessageBuilder.Build(new[] { P("a", 1), P("b", 2) }, "a", looks, new[] { "a", "b" });

            Assert.AreEqual("가나#1", msg.Placements[0].DisplayName);
            Assert.AreEqual(7, msg.Placements[0].AccountLevel);
            Assert.AreEqual("hat_01", msg.Placements[0].Look["hat"]);
            //  조회에 없는 사람은 기본값 — 명단 순번으로 "플레이어 N"
            Assert.AreEqual("플레이어 2", msg.Placements[1].DisplayName);
            Assert.AreEqual(1, msg.Placements[1].AccountLevel);
            Assert.AreEqual(0, msg.Placements[1].Look.Count);
        }

        [Test]
        public void 룰이_연습이라_하면_연습이다()
        {
            var outcome = new MatchOutcome { practice = true };
            outcome.placements.Add(new MatchPlacement { userId = "a", placement = 1 });
            Assert.IsTrue(MatchEndedMessageBuilder.IsPractice(outcome));

            outcome.practice = false;
            Assert.IsFalse(MatchEndedMessageBuilder.IsPractice(outcome));
        }

        [Test]
        public void 마무리를_거쳐도_연습_표시가_남는다()
        {
            var raw = new MatchOutcome { practice = true };
            raw.placements.Add(new MatchPlacement { userId = "a", placement = 1 });
            var done = MatchOutcomeFinalizer.Finalize(raw, new string[0], new string[0], null, 100, 0, 0.02);
            Assert.IsTrue(done.practice);
        }

        [Test]
        public void 룩_조회가_실패해도_이름은_채운다()
        {
            var msg = MatchEndedMessageBuilder.Build(new[] { P("a", 1) }, "a", null, null);
            Assert.AreEqual("플레이어 1", msg.Placements[0].DisplayName);
        }

        [Test]
        public void 점수_변화는_본인_것만()
        {
            var msg = MatchEndedMessageBuilder.Build(new[] { P("a", 1), P("b", 2) }, "b", null, new[] { "a", "b" });
            Assert.IsTrue(msg.HasRatingChange);
            Assert.AreEqual(1000, msg.MyMmrBefore);
        }

        [Test]
        public void 보고_실패면_빈_메시지()
        {
            Assert.AreEqual(0, MatchEndedMessageBuilder.Build(null, "a", null, null).Placements.Count);
        }

        [Test]
        public void 등수_0이_섞이면_빈_메시지()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Assert.AreEqual(0, MatchEndedMessageBuilder.Build(new[] { P("a", 0) }, "a", null, null).Placements.Count);
        }

        [Test]
        public void 등수마다_캐릭터_외형을_싣는다()
        {
            var looks = new Dictionary<string, PlayerLookDto>
            {
                ["a"] = new PlayerLookDto
                {
                    displayName = "가나", level = 1,
                    characterAppearance = new CharacterAppearanceDto { skinColorId = 105, hairColorId = 204 },
                },
            };
            var msg = MatchEndedMessageBuilder.Build(new[] { P("a", 1), P("b", 2) }, "a", looks, new[] { "a", "b" });

            Assert.AreEqual(105, msg.Placements[0].CharacterAppearance.SkinColorId);
            Assert.AreEqual(204, msg.Placements[0].CharacterAppearance.HairColorId);
            //  조회에 없는 사람은 0(모름) — 클라가 기본값으로 그린다.
            Assert.AreEqual(0, msg.Placements[1].CharacterAppearance?.SkinColorId ?? 0);
        }
    }
}
