using System.Collections.Generic;

namespace LOP
{
    public enum PanchigiPhase
    {
        Settling,
        Aiming,
        Over,
    }

    /// <summary>동전이 멎은 뒤 판에 할 일.</summary>
    public enum PanchigiBoardAction
    {
        None,
        ResetFull,          // 6개 전부 처음 배치로(새 프레임, 마지막 프레임 보너스)
        RemoveFlipped,      // 뒤집힌 동전을 판 옆으로 치운다(같은 프레임의 다음 타격)
        RestoreBeforeRoll,  // 파울 — 치기 직전 자세로
    }

    /// <summary>
    /// 판치기 한 판의 진행(볼링식). 물리도 시계도 모르고 "무슨 일이 있었나"만 받아 차례와 타격 기록을 정하고,
    /// 판에 무엇을 할지 돌려준다 — 그 조치를 물리로 실행하는 것은 <see cref="PanchigiTurnSystem"/>이다.
    /// 점수는 <see cref="PanchigiBowlingScore"/>가 기록에서 계산한다.
    /// </summary>
    public class PanchigiTurn
    {
        private readonly int frameCount;
        private readonly int pinCount;
        private readonly List<string> players = new();
        private readonly Dictionary<string, List<PanchigiRoll>> rolls = new();

        private int currentIndex;

        public PanchigiPhase Phase { get; private set; } = PanchigiPhase.Settling;

        /// <summary>지금 칠 차례인 사람. <see cref="PanchigiPhase.Aiming"/>이 아니면 null.</summary>
        public string CurrentEntityId { get; private set; }

        /// <summary>방금 친 사람.</summary>
        public string LastStrikerEntityId { get; private set; }

        /// <summary>친 것과 시간 초과를 모두 센다 — 조준 마감을 "새 조준마다 한 번" 정하는 데 쓴다.</summary>
        public int TurnCount { get; private set; }

        /// <summary>모두의 타격 수 합계 — "무언가 달라졌나"를 싸게 보려는 용도다.</summary>
        public int TotalRolls { get; private set; }

        public IReadOnlyList<string> PlayerEntityIds => players;

        public PanchigiTurn(IReadOnlyList<string> playerEntityIds, int frameCount, int pinCount)
        {
            this.frameCount = frameCount;
            this.pinCount = pinCount;
            players.AddRange(playerEntityIds);
            foreach (string id in playerEntityIds)
            {
                rolls[id] = new List<PanchigiRoll>();
            }
        }

        public IReadOnlyList<PanchigiRoll> Rolls(string entityId)
        {
            return entityId != null && rolls.TryGetValue(entityId, out var list) ? list : System.Array.Empty<PanchigiRoll>();
        }

        public int Total(string entityId) => PanchigiBowlingScore.Total(Rolls(entityId), frameCount, pinCount);

        public void OnStruck(string entityId)
        {
            if (Phase != PanchigiPhase.Aiming) { return; }

            LastStrikerEntityId = entityId;
            TurnCount++;
            CurrentEntityId = null;
            Phase = PanchigiPhase.Settling;
        }

        /// <summary>동전이 모두 멎었다. 판 시작 직후에도 한 번 온다(그땐 아무도 안 쳤다).</summary>
        /// <param name="flippedOnBoard">판 위에 남아 있던 동전 중 뒤집힌 개수(치운 동전은 세지 않는다).</param>
        /// <param name="droppedOut">동전이 판 밖으로 나갔다 — 파울.</param>
        public PanchigiBoardAction OnRested(int flippedOnBoard, bool droppedOut)
        {
            if (Phase != PanchigiPhase.Settling) { return PanchigiBoardAction.None; }

            if (LastStrikerEntityId == null)
            {
                currentIndex = 0;
                Aim();
                return PanchigiBoardAction.ResetFull;
            }

            //  낙을 먼저 본다 — 되돌릴 판이라 뒤집힌 개수는 의미가 없다.
            var roll = droppedOut ? PanchigiRoll.Fouled : new PanchigiRoll(flippedOnBoard);
            return Record(LastStrikerEntityId, roll);
        }

        /// <summary>조준 시간을 넘겼다 — 0점 타격으로 치고 이어 간다. 물리를 안 건드리므로 Settling을 거치지 않는다.</summary>
        public PanchigiBoardAction OnAimTimeout()
        {
            if (Phase != PanchigiPhase.Aiming) { return PanchigiBoardAction.None; }

            TurnCount++;
            return Record(CurrentEntityId, new PanchigiRoll(0));
        }

        private PanchigiBoardAction Record(string entityId, PanchigiRoll roll)
        {
            List<PanchigiRoll> mine = rolls[entityId];
            int frameBefore = PanchigiBowlingScore.Locate(mine, frameCount, pinCount).Frame;
            mine.Add(roll);
            TotalRolls++;

            PanchigiBowlingPosition now = PanchigiBowlingScore.Locate(mine, frameCount, pinCount);
            if (now.Complete || now.Frame != frameBefore)
            {
                NextPlayer();
                return Phase == PanchigiPhase.Over ? PanchigiBoardAction.None : PanchigiBoardAction.ResetFull;
            }

            Aim();   // 같은 사람의 같은 프레임
            if (roll.Foul) { return PanchigiBoardAction.RestoreBeforeRoll; }

            //  마지막 프레임 보너스에서 다 뒤집혔으면 판을 다시 세운다.
            return now.Standing == pinCount ? PanchigiBoardAction.ResetFull : PanchigiBoardAction.RemoveFlipped;
        }

        private void NextPlayer()
        {
            for (int step = 1; step <= players.Count; step++)
            {
                int index = (currentIndex + step) % players.Count;
                if (PanchigiBowlingScore.Locate(rolls[players[index]], frameCount, pinCount).Complete == false)
                {
                    currentIndex = index;
                    Aim();
                    return;
                }
            }

            CurrentEntityId = null;
            Phase = PanchigiPhase.Over;
        }

        private void Aim()
        {
            CurrentEntityId = players[currentIndex];
            Phase = PanchigiPhase.Aiming;
        }
    }
}
