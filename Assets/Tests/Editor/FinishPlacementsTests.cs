using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    public class FinishPlacementsTests
    {
        static FinishRecord Rec(string entityId, long tick, float past) => new FinishRecord(entityId, tick, past);

        static Dictionary<string, string> Map(params string[] ids)
        {
            var map = new Dictionary<string, string>();
            foreach (string id in ids)
            {
                map[id] = "user-" + id;
            }
            return map;
        }

        static MatchOutcome Resolve(
            IReadOnlyList<FinishRecord> finished,
            IReadOnlyDictionary<string, string> map,
            IReadOnlyList<(string, float)> unfinished = null,
            IReadOnlyList<string> left = null)
            => FinishPlacements.Resolve(finished, map,
                unfinished ?? new List<(string, float)>(), left ?? new List<string>());

        static int PlacementOf(MatchOutcome outcome, string userId)
            => outcome.placements.Find(p => p.userId == userId).placement;

        [Test]
        public void 먼저_닿은_순서대로_1등부터()
        {
            var outcome = Resolve(
                new[] { Rec("a", 10, 0.5f), Rec("b", 11, 0.2f), Rec("c", 12, 0.1f) }, Map("a", "b", "c"));

            Assert.AreEqual(1, PlacementOf(outcome, "user-a"));
            Assert.AreEqual(2, PlacementOf(outcome, "user-b"));
            Assert.AreEqual(3, PlacementOf(outcome, "user-c"));
        }

        [Test]
        public void 동점이면_같은_등수고_다음은_건너뛴다()
        {
            //  스포츠 표준 1·1·3. 2등을 주면 세 번째가 실제보다 앞서 보인다.
            var outcome = Resolve(
                new[] { Rec("a", 10, 0.5f), Rec("b", 10, 0.5f), Rec("c", 11, 0.1f) }, Map("a", "b", "c"));

            Assert.AreEqual(1, PlacementOf(outcome, "user-a"));
            Assert.AreEqual(1, PlacementOf(outcome, "user-b"));
            Assert.AreEqual(3, PlacementOf(outcome, "user-c"));
        }

        [Test]
        public void 전원_동점도_된다()
        {
            //  Flappy에서 아무도 안 부딪힌 판이 바로 이 모양이다 — 새 속도가 전부 같다.
            var outcome = Resolve(
                new[] { Rec("a", 10, 0.5f), Rec("b", 10, 0.5f), Rec("c", 10, 0.5f) }, Map("a", "b", "c"));

            Assert.AreEqual(1, PlacementOf(outcome, "user-a"));
            Assert.AreEqual(1, PlacementOf(outcome, "user-b"));
            Assert.AreEqual(1, PlacementOf(outcome, "user-c"));
        }

        [Test]
        public void 같은_틱이라도_깊이가_다르면_등수가_갈린다()
        {
            var outcome = Resolve(
                new[] { Rec("a", 10, 0.9f), Rec("b", 10, 0.1f) }, Map("a", "b"));

            Assert.AreEqual(1, PlacementOf(outcome, "user-a"));
            Assert.AreEqual(2, PlacementOf(outcome, "user-b"));
        }

        [Test]
        public void 못_닿은_사람은_통과자_뒤에_진행도_큰_순으로()
        {
            var outcome = Resolve(
                new[] { Rec("a", 10, 0.5f) }, Map("a"),
                unfinished: new[] { ("느린이", 30f), ("빠른이", 90f) });

            Assert.AreEqual(1, PlacementOf(outcome, "user-a"));
            Assert.AreEqual(2, PlacementOf(outcome, "빠른이"));
            Assert.AreEqual(3, PlacementOf(outcome, "느린이"));
        }

        [Test]
        public void 나간_사람은_맨_뒤다()
        {
            var outcome = Resolve(
                new[] { Rec("a", 10, 0.5f) }, Map("a"),
                unfinished: new[] { ("달리는중", 50f) },
                left: new[] { "나간이" });

            Assert.AreEqual(3, PlacementOf(outcome, "나간이"));
        }

        [Test]
        public void 아무도_안_닿았으면_진행도만으로_매긴다()
        {
            //  시간 상한으로 끝난 판이 이 모양이다.
            var outcome = Resolve(
                new FinishRecord[0], new Dictionary<string, string>(),
                unfinished: new[] { ("뒤", 10f), ("앞", 80f) });

            Assert.AreEqual(1, PlacementOf(outcome, "앞"));
            Assert.AreEqual(2, PlacementOf(outcome, "뒤"));
        }

        [Test]
        public void 대응표에_없는_기록은_건너뛰되_등수에_구멍을_안_낸다()
        {
            //  있어선 안 되는 상태지만, 나더라도 뒷사람 등수가 밀려선 안 된다.
            var outcome = Resolve(
                new[] { Rec("유령", 9, 1f), Rec("a", 10, 0.5f), Rec("b", 11, 0.2f) }, Map("a", "b"));

            Assert.AreEqual(2, outcome.placements.Count);
            Assert.AreEqual(1, PlacementOf(outcome, "user-a"));
            Assert.AreEqual(2, PlacementOf(outcome, "user-b"));
        }

        [Test]
        public void 아무도_없으면_빈_결과다()
        {
            var outcome = Resolve(new FinishRecord[0], new Dictionary<string, string>());

            Assert.IsEmpty(outcome.placements);
        }
    }
}
