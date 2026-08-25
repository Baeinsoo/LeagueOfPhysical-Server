using System.Collections.Generic;

namespace LOP
{
    public enum PanchigiPhase
    {
        Settling,
        Aiming,
        Over,
    }

    /// <summary>
    /// 판치기 한 판의 진행. 물리도 시계도 모르고 "무슨 일이 있었나"만 받아 다음 국면을 정한다.
    /// </summary>
    public class PanchigiTurn
    {
        private readonly IReadOnlyList<string> players;
        private readonly int turnLimit;

        private int nextIndex;
        private string lastStriker;

        public PanchigiPhase Phase { get; private set; } = PanchigiPhase.Settling;

        /// <summary>지금 칠 차례인 사람. <see cref="PanchigiPhase.Aiming"/>이 아니면 null.</summary>
        public string CurrentEntityId { get; private set; }

        /// <summary>친 것과 패스한 것을 모두 센다 — 안 그러면 전원이 계속 패스해 판이 안 끝난다.</summary>
        public int TurnCount { get; private set; }

        /// <summary>이긴 사람. 아직 안 끝났거나 무승부면 null.</summary>
        public string WinnerEntityId { get; private set; }

        public PanchigiTurn(IReadOnlyList<string> playerEntityIds, int turnLimit)
        {
            players = playerEntityIds;
            this.turnLimit = turnLimit;
        }

        /// <summary>동전이 모두 멎었다. 판 시작 직후에도 한 번 온다(그땐 아무도 안 쳐서 allFlipped가 거짓).</summary>
        public void OnRested(bool allFlipped)
        {
            if (Phase != PanchigiPhase.Settling) { return; }

            if (allFlipped)
            {
                WinnerEntityId = lastStriker;   // 그 상태를 만든 사람
                Phase = PanchigiPhase.Over;
                return;
            }

            if (TurnCount > turnLimit)
            {
                Phase = PanchigiPhase.Over;     // 무승부 — WinnerEntityId는 null
                return;
            }

            EnterAiming();
        }

        public void OnStruck(string entityId)
        {
            if (Phase != PanchigiPhase.Aiming) { return; }

            lastStriker = entityId;
            TurnCount++;
            CurrentEntityId = null;
            Phase = PanchigiPhase.Settling;
        }

        /// <summary>조준 시간을 넘겼다 — 그냥 패스한다. 물리를 안 건드리므로 Settling을 거치지 않는다.</summary>
        public void OnAimTimeout()
        {
            if (Phase != PanchigiPhase.Aiming) { return; }

            TurnCount++;

            if (TurnCount > turnLimit)
            {
                CurrentEntityId = null;
                Phase = PanchigiPhase.Over;
                return;
            }

            EnterAiming();
        }

        private void EnterAiming()
        {
            if (players.Count == 0)
            {
                Phase = PanchigiPhase.Over;
                return;
            }

            CurrentEntityId = players[nextIndex];
            nextIndex = (nextIndex + 1) % players.Count;
            Phase = PanchigiPhase.Aiming;
        }
    }
}
