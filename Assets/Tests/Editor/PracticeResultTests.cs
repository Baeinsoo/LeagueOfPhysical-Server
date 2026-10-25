using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>봇이 낀 연습 판 — 백엔드에 보고하지 않고 게임서버가 결과 메시지를 직접 만든다.</summary>
    public class PracticeResultTests
    {
        private static MatchOutcome Outcome(params (string id, int place)[] rows)
        {
            var outcome = new MatchOutcome();
            foreach (var (id, place) in rows)
            {
                outcome.placements.Add(new MatchPlacement { userId = id, placement = place, stats = new Dictionary<string, int> { ["kills"] = place } });
            }
            return outcome;
        }

        private static ReportMatchResultResponse Ok(MatchOutcome outcome)
        {
            var participants = new List<ConfirmedParticipantDto>();
            foreach (var p in outcome.placements)
                participants.Add(new ConfirmedParticipantDto { userId = p.userId, placement = p.placement, mmrBefore = 1000, mmrAfter = 1010 });
            return new ReportMatchResultResponse { code = 200, participants = participants.ToArray() };
        }

        [Test]
        public void 봇이_있으면_백엔드에_보고하지_않는다()
        {
            int calls = 0;
            var outcome = Outcome(("u1", 1), ("bot-1", 2));

            var confirmed = MatchResultConfirmer.ConfirmAsync(outcome, o => { calls++; return UniTask.FromResult(Ok(o)); }).GetAwaiter().GetResult();

            Assert.AreEqual(0, calls);
            Assert.AreEqual(2, confirmed.Length);
        }

        [Test]
        public void 봇이_없으면_지금처럼_보고한다()
        {
            int calls = 0;
            var outcome = Outcome(("u1", 1), ("u2", 2));

            var confirmed = MatchResultConfirmer.ConfirmAsync(outcome, o => { calls++; return UniTask.FromResult(Ok(o)); }).GetAwaiter().GetResult();

            Assert.AreEqual(1, calls);
            Assert.AreEqual(1010, confirmed[0].mmrAfter);
        }

        [Test]
        public void 연습_결과는_봇_포함_등수()
        {
            var outcome = Outcome(("u1", 2), ("bot-1", 1), ("bot-2", 3));

            var msg = MatchEndedMessageBuilder.Build(MatchEndedMessageBuilder.PracticeParticipants(outcome), "u1", null, new[] { "u1" }, practice: true);

            Assert.AreEqual(3, msg.Placements.Count);
            Assert.AreEqual("bot-1", msg.Placements[1].UserId);
            Assert.AreEqual(1, msg.Placements[1].Placement);
            Assert.AreEqual(1, msg.Placements[1].Stats["kills"]);
        }

        [Test]
        public void 연습_결과는_점수_변화_없음()
        {
            var outcome = Outcome(("u1", 1), ("bot-1", 2));
            var participants = MatchEndedMessageBuilder.PracticeParticipants(outcome);

            var msg = MatchEndedMessageBuilder.Build(participants, "u1", null, new[] { "u1" }, practice: true);

            Assert.IsFalse(msg.HasRatingChange);
            Assert.AreEqual(participants[0].mmrBefore, participants[0].mmrAfter);
        }

        [Test]
        public void 봇_이름은_봇_N()
        {
            var outcome = Outcome(("u1", 1), ("bot-3", 2));

            var msg = MatchEndedMessageBuilder.Build(MatchEndedMessageBuilder.PracticeParticipants(outcome), "u1", null, new[] { "u1" }, practice: true);

            Assert.AreEqual("봇 3", msg.Placements[1].DisplayName);
            Assert.AreEqual("플레이어 1", msg.Placements[0].DisplayName);
        }
    }
}
