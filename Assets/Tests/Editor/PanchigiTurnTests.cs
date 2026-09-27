using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    public class PanchigiTurnTests
    {
        private static readonly string[] TwoPlayers = { "A", "B" };
        private static readonly string[] ThreePlayers = { "A", "B", "C" };

        private static PanchigiTurn Aiming(IReadOnlyList<string> players, int strokeLimit = 10)
        {
            var turn = new PanchigiTurn(players, strokeLimit);
            turn.OnRested(false, false);   // 판 시작 직후 한 번 — 여기서 첫 조준으로 들어간다
            return turn;
        }

        /// <summary>지금 차례인 사람이 치고, 동전이 그 결과로 멎는다.</summary>
        private static void Strike(PanchigiTurn turn, bool allFlipped = false, bool droppedOut = false)
        {
            turn.OnStruck(turn.CurrentEntityId);
            turn.OnRested(allFlipped, droppedOut);
        }

        [Test]
        public void 판이_시작되면_첫_사람이_조준한다()
        {
            var turn = new PanchigiTurn(TwoPlayers, 10);

            turn.OnRested(false, false);

            Assert.AreEqual(PanchigiPhase.Aiming, turn.Phase);
            Assert.AreEqual("A", turn.CurrentEntityId);
            Assert.AreEqual(0, turn.GetStrokes("A"));
            Assert.AreEqual(0, turn.GetStrokes("B"));
        }

        [Test]
        public void 치면_1타이고_동전이_멎을_때까지_기다린다()
        {
            var turn = Aiming(TwoPlayers);

            turn.OnStruck("A");

            Assert.AreEqual(PanchigiPhase.Settling, turn.Phase);
            Assert.AreEqual(1, turn.GetStrokes("A"));
            Assert.AreEqual("A", turn.LastStrikerEntityId);
            Assert.IsNull(turn.CurrentEntityId, "구르는 동안은 아무도 조준하지 않는다");
        }

        [Test]
        public void 멎으면_다음_사람_차례다()
        {
            var turn = Aiming(TwoPlayers);

            Strike(turn);

            Assert.AreEqual(PanchigiPhase.Aiming, turn.Phase);
            Assert.AreEqual("B", turn.CurrentEntityId);
        }

        [Test]
        public void 조준_시간을_넘기면_1타이고_차례가_넘어간다()
        {
            var turn = Aiming(TwoPlayers);

            turn.OnAimTimeout();

            Assert.AreEqual(1, turn.GetStrokes("A"));
            Assert.AreEqual(PanchigiPhase.Aiming, turn.Phase);
            Assert.AreEqual("B", turn.CurrentEntityId);
        }

        [Test]
        public void 낙이면_친_1타에_1벌타가_붙는다()
        {
            var turn = Aiming(TwoPlayers);

            Strike(turn, droppedOut: true);

            Assert.AreEqual(2, turn.GetStrokes("A"));
            Assert.AreEqual(0, turn.GetStrokes("B"), "벌타는 친 사람만");
            Assert.IsFalse(turn.IsFinished("A"));
        }

        [Test]
        public void 전부_뒤집히면_홀아웃하고_그_타수로_기록된다()
        {
            var turn = Aiming(TwoPlayers);

            Strike(turn);                     // A 1타
            Strike(turn);                     // B 1타
            Strike(turn, allFlipped: true);   // A 2타째에 홀아웃

            Assert.IsTrue(turn.IsFinished("A"));
            Assert.AreEqual(2, turn.GetStrokes("A"));
            CollectionAssert.Contains(turn.FinishedEntityIds, "A");
        }

        [Test]
        public void 낙과_전부_뒤집힘이_같이_나면_홀아웃이_아니다()
        {
            //  낙이면 판을 처음 배치로 되돌린 뒤라 뒤집힌 동전이 남지 않는다.
            var turn = Aiming(TwoPlayers);

            Strike(turn, allFlipped: true, droppedOut: true);

            Assert.IsFalse(turn.IsFinished("A"));
            Assert.AreEqual(2, turn.GetStrokes("A"));
        }

        [Test]
        public void 끝난_사람에게는_차례가_안_돌아온다()
        {
            var turn = Aiming(ThreePlayers);

            Strike(turn, allFlipped: true);   // A 홀아웃

            for (int i = 0; i < 6; i++)
            {
                Assert.AreNotEqual("A", turn.CurrentEntityId);
                Strike(turn);
            }
        }

        [Test]
        public void 가운데_사람이_끝나도_다음_사람을_건너뛰지_않는다()
        {
            var turn = Aiming(ThreePlayers);

            Strike(turn);                     // A
            Strike(turn, allFlipped: true);   // B 홀아웃

            Assert.AreEqual("C", turn.CurrentEntityId);
            Strike(turn);
            Assert.AreEqual("A", turn.CurrentEntityId);
        }

        [Test]
        public void 상한에_닿도록_못_끝내면_상한_더하기_1로_기록하고_빠진다()
        {
            var turn = Aiming(TwoPlayers, strokeLimit: 2);

            Strike(turn);   // A 1
            Strike(turn);   // B 1
            Strike(turn);   // A 2 — 상한

            Assert.IsTrue(turn.IsFinished("A"));
            Assert.AreEqual(3, turn.GetStrokes("A"));
            Assert.AreEqual("B", turn.CurrentEntityId);
        }

        [Test]
        public void 상한째_타에_홀아웃하면_상한_그대로다()
        {
            var turn = Aiming(TwoPlayers, strokeLimit: 2);

            Strike(turn);                     // A 1
            Strike(turn);                     // B 1
            Strike(turn, allFlipped: true);   // A 2 — 상한째 타에 홀아웃

            Assert.AreEqual(2, turn.GetStrokes("A"));
        }

        [Test]
        public void 벌타로_상한을_넘어도_상한_더하기_1이다()
        {
            var turn = Aiming(TwoPlayers, strokeLimit: 2);

            Strike(turn);                     // A 1
            Strike(turn);                     // B 1
            Strike(turn, droppedOut: true);   // A 2 + 벌타 = 3

            Assert.IsTrue(turn.IsFinished("A"));
            Assert.AreEqual(3, turn.GetStrokes("A"), "상한+1에서 멈춘다 — 벌타만큼 더 늘지 않는다");
        }

        [Test]
        public void 시간_초과로_상한에_닿아도_빠진다()
        {
            var turn = Aiming(TwoPlayers, strokeLimit: 1);

            turn.OnAimTimeout();   // A 1 — 상한

            Assert.IsTrue(turn.IsFinished("A"));
            Assert.AreEqual(2, turn.GetStrokes("A"));
            Assert.AreEqual("B", turn.CurrentEntityId);
        }

        [Test]
        public void 혼자_남아도_시간_초과마다_차례가_새로_시작된다()
        {
            //  조준 마감은 TurnCount가 바뀔 때 새로 잡힌다 — 같은 사람이 연달아 받아도 바뀌어야 한다.
            var turn = Aiming(TwoPlayers);
            Strike(turn, allFlipped: true);   // A 홀아웃 → B 혼자

            int before = turn.TurnCount;
            turn.OnAimTimeout();

            Assert.AreEqual("B", turn.CurrentEntityId);
            Assert.AreNotEqual(before, turn.TurnCount);
        }

        [Test]
        public void 모두_끝나면_판이_끝난다()
        {
            var turn = Aiming(TwoPlayers);

            Strike(turn, allFlipped: true);   // A
            Strike(turn, allFlipped: true);   // B

            Assert.AreEqual(PanchigiPhase.Over, turn.Phase);
            Assert.IsNull(turn.CurrentEntityId);
        }

        [Test]
        public void 타수_총합은_벌타와_기록까지_따라간다()
        {
            //  방송할지 가르는 값이다 — 무언가 바뀌었는데 그대로면 화면이 안 바뀐다.
            var turn = Aiming(TwoPlayers);
            int start = turn.TotalStrokes;

            Strike(turn, droppedOut: true);   // A 1 + 벌타 1

            Assert.AreEqual(start + 2, turn.TotalStrokes);

            var capped = Aiming(TwoPlayers, strokeLimit: 1);
            capped.OnAimTimeout();            // A 1 — 상한에 닿아 기록이 2가 된다

            Assert.AreEqual(2, capped.TotalStrokes, "상한 기록으로 늘어난 몫도 총합에 들어가야 방송된다");
        }

        [Test]
        public void 순위는_적은_타수가_앞이고_같으면_공동에_다음은_건너뛴다()
        {
            var places = PanchigiRanking.Place(new Dictionary<string, int> { ["A"] = 3, ["B"] = 3, ["C"] = 5, ["D"] = 2 });

            Assert.AreEqual(1, places["D"]);
            Assert.AreEqual(2, places["A"]);
            Assert.AreEqual(2, places["B"]);
            Assert.AreEqual(4, places["C"]);
        }

        [Test]
        public void 모두_같은_타수면_전원_1등이다()
        {
            var places = PanchigiRanking.Place(new Dictionary<string, int> { ["A"] = 11, ["B"] = 11 });

            Assert.AreEqual(1, places["A"]);
            Assert.AreEqual(1, places["B"]);
        }
    }
}
