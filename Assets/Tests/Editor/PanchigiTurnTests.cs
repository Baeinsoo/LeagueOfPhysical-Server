using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    public class PanchigiTurnTests
    {
        private static readonly string[] TwoPlayers = { "A", "B" };
        private const int Pins = 6;

        private static PanchigiTurn Started(IReadOnlyList<string> players, int frames = 5)
        {
            var turn = new PanchigiTurn(players, frames, Pins);
            Assert.AreEqual(PanchigiBoardAction.ResetFull, turn.OnRested(0, false), "판 시작은 처음 배치");
            return turn;
        }

        private static PanchigiBoardAction Roll(PanchigiTurn turn, int flipped, bool dropped = false)
        {
            turn.OnStruck(turn.CurrentEntityId);
            return turn.OnRested(flipped, dropped);
        }

        [Test]
        public void 판이_시작되면_첫_사람이_조준한다()
        {
            var turn = Started(TwoPlayers);
            Assert.AreEqual(PanchigiPhase.Aiming, turn.Phase);
            Assert.AreEqual("A", turn.CurrentEntityId);
        }

        [Test]
        public void 첫_번째_뒤에는_뒤집힌_것을_치우고_같은_사람이_또_친다()
        {
            var turn = Started(TwoPlayers);

            Assert.AreEqual(PanchigiBoardAction.RemoveFlipped, Roll(turn, 2));
            Assert.AreEqual("A", turn.CurrentEntityId);
        }

        [Test]
        public void 두_번째_뒤에는_판을_새로_세우고_다음_사람이다()
        {
            var turn = Started(TwoPlayers);
            Roll(turn, 2);

            Assert.AreEqual(PanchigiBoardAction.ResetFull, Roll(turn, 1));
            Assert.AreEqual("B", turn.CurrentEntityId);
        }

        [Test]
        public void 스트라이크면_바로_다음_사람이다()
        {
            var turn = Started(TwoPlayers);

            Assert.AreEqual(PanchigiBoardAction.ResetFull, Roll(turn, 6));
            Assert.AreEqual("B", turn.CurrentEntityId);
        }

        [Test]
        public void 파울이면_치기_직전으로_되돌리고_같은_사람이_두_번째를_친다()
        {
            var turn = Started(TwoPlayers);

            Assert.AreEqual(PanchigiBoardAction.RestoreBeforeRoll, Roll(turn, 3, dropped: true));
            Assert.AreEqual("A", turn.CurrentEntityId);
            Assert.IsTrue(turn.Rolls("A")[0].Foul);
        }

        [Test]
        public void 두_번째_파울이면_프레임이_끝나고_판을_새로_세운다()
        {
            var turn = Started(TwoPlayers);
            Roll(turn, 2);

            Assert.AreEqual(PanchigiBoardAction.ResetFull, Roll(turn, 3, dropped: true));
            Assert.AreEqual("B", turn.CurrentEntityId);
        }

        [Test]
        public void 첫_번째_파울_뒤_두_번째에_다_뒤집으면_스페어다()
        {
            var turn = Started(TwoPlayers);
            Roll(turn, 0, dropped: true);
            Roll(turn, 6);

            Assert.AreEqual("B", turn.CurrentEntityId);
            //  스페어 보너스는 A의 다음 프레임 첫 번째에서 정해진다
            Roll(turn, 1); Roll(turn, 1);   // B
            Roll(turn, 2);                  // A 2프레임 첫 번째
            Assert.AreEqual(8, PanchigiBowlingScore.Frames(turn.Rolls("A"), 5, Pins)[0].Cumulative);
        }

        [Test]
        public void 시간_초과는_0점_타격이고_같은_프레임이_이어진다()
        {
            var turn = Started(TwoPlayers);

            turn.OnAimTimeout();

            Assert.AreEqual(1, turn.Rolls("A").Count);
            Assert.AreEqual(0, turn.Rolls("A")[0].Flipped);
            Assert.IsFalse(turn.Rolls("A")[0].Foul);
            Assert.AreEqual("A", turn.CurrentEntityId);
        }

        [Test]
        public void 두_번째에서_시간_초과면_다음_사람에게_판을_새로_세운다()
        {
            var turn = Started(TwoPlayers);
            Roll(turn, 2);

            Assert.AreEqual(PanchigiBoardAction.ResetFull, turn.OnAimTimeout());
            Assert.AreEqual("B", turn.CurrentEntityId);
        }

        [Test]
        public void 마지막_프레임_스트라이크_뒤_판을_다시_세운다()
        {
            var turn = Started(TwoPlayers, frames: 1);

            Assert.AreEqual(PanchigiBoardAction.ResetFull, Roll(turn, 6), "보너스는 판을 다시 세워 친다");
            Assert.AreEqual("A", turn.CurrentEntityId, "마지막 프레임 보너스는 같은 사람");
            Assert.AreEqual(PanchigiBoardAction.RemoveFlipped, Roll(turn, 2));
            Assert.AreEqual("A", turn.CurrentEntityId);
            Assert.AreEqual(PanchigiBoardAction.ResetFull, Roll(turn, 1));
            Assert.AreEqual("B", turn.CurrentEntityId);
        }

        [Test]
        public void 마지막_프레임_오픈이면_두_번으로_끝난다()
        {
            var turn = Started(TwoPlayers, frames: 1);
            Roll(turn, 2); Roll(turn, 1);

            Assert.AreEqual("B", turn.CurrentEntityId);
        }

        [Test]
        public void 모두_끝나면_판이_끝나고_합계가_나온다()
        {
            var turn = Started(TwoPlayers, frames: 1);
            Roll(turn, 2); Roll(turn, 1);   // A 3
            Roll(turn, 6); Roll(turn, 6); Roll(turn, 6);   // B 18

            Assert.AreEqual(PanchigiPhase.Over, turn.Phase);
            Assert.IsNull(turn.CurrentEntityId);
            Assert.AreEqual(3, turn.Total("A"));
            Assert.AreEqual(18, turn.Total("B"));
        }

        [Test]
        public void 세_명이면_프레임마다_차례대로_돈다()
        {
            var turn = Started(new[] { "A", "B", "C" }, frames: 2);
            Roll(turn, 6);   // A
            Assert.AreEqual("B", turn.CurrentEntityId);
            Roll(turn, 1); Roll(turn, 1);
            Assert.AreEqual("C", turn.CurrentEntityId);
            Roll(turn, 1); Roll(turn, 1);
            Assert.AreEqual("A", turn.CurrentEntityId);
        }

        [Test]
        public void 타격_수_합계는_치거나_시간이_넘을_때마다_는다()
        {
            var turn = Started(TwoPlayers);
            int before = turn.TotalRolls;
            Roll(turn, 1);
            turn.OnAimTimeout();

            Assert.AreEqual(before + 2, turn.TotalRolls);
        }

        [Test]
        public void 순위는_높은_점수가_앞이고_같으면_공동이다()
        {
            var places = PanchigiRanking.Place(new Dictionary<string, int> { ["A"] = 30, ["B"] = 30, ["C"] = 12, ["D"] = 45 });
            Assert.AreEqual(1, places["D"]);
            Assert.AreEqual(2, places["A"]);
            Assert.AreEqual(2, places["B"]);
            Assert.AreEqual(4, places["C"]);
        }
    }
}
