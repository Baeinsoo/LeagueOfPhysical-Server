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
        private readonly int turnLimit;
        private readonly int dropOutLimit;

        private readonly Dictionary<string, int> dropOutCounts = new();
        private readonly HashSet<string> eliminated = new();
        private readonly List<string> alive = new();

        private int nextIndex;
        private string lastStriker;

        public PanchigiPhase Phase { get; private set; } = PanchigiPhase.Settling;

        /// <summary>지금 칠 차례인 사람. <see cref="PanchigiPhase.Aiming"/>이 아니면 null.</summary>
        public string CurrentEntityId { get; private set; }

        /// <summary>친 것과 패스한 것을 모두 센다 — 안 그러면 전원이 계속 패스해 판이 안 끝난다.</summary>
        public int TurnCount { get; private set; }

        /// <summary>이긴 사람. 아직 안 끝났거나 무승부면 null.</summary>
        public string WinnerEntityId { get; private set; }

        /// <summary>낙(落) 몇 번까지 봐주는지. 0이면 벌칙 없음.</summary>
        public int DropOutLimit => dropOutLimit;

        public PanchigiTurn(IReadOnlyList<string> playerEntityIds, int turnLimit, int dropOutLimit)
        {
            this.turnLimit = turnLimit;
            this.dropOutLimit = dropOutLimit;
            alive.AddRange(playerEntityIds);
        }

        /// <summary>그 사람이 지금까지 떨어뜨린 횟수.</summary>
        public int GetDropOutCount(string entityId)
        {
            return entityId != null && dropOutCounts.TryGetValue(entityId, out int count) ? count : 0;
        }

        public bool IsEliminated(string entityId)
        {
            return entityId != null && eliminated.Contains(entityId);
        }

        /// <summary>사람별 낙 횟수. 한 번도 안 떨어뜨린 사람은 들어 있지 않다.</summary>
        public IReadOnlyDictionary<string, int> DropOutCounts => dropOutCounts;

        /// <summary>판에서 빠진 사람들.</summary>
        public IReadOnlyCollection<string> EliminatedEntityIds => eliminated;

        /// <summary>지금까지 난 낙의 총합. "무언가 달라졌나"를 싸게 보려는 용도다.</summary>
        public int TotalDropOuts { get; private set; }

        /// <summary>
        /// 동전이 모두 멎었다. 판 시작 직후에도 한 번 온다(그땐 아무도 안 쳐서 allFlipped가 거짓).
        /// </summary>
        /// <param name="allFlipped">동전이 전부 뒤집혔다 — 친 사람의 승리다.</param>
        /// <param name="droppedOut">동전이 판 밖으로 나갔다 — 친 사람의 벌점이다.</param>
        public void OnRested(bool allFlipped, bool droppedOut)
        {
            if (Phase != PanchigiPhase.Settling) { return; }

            if (droppedOut)
            {
                //  낙이 났으면 판을 처음 세팅으로 되돌린 뒤라 뒤집힌 동전이 남아 있지 않다.
                //  그래서 같은 턴에 승리가 성립할 수 없다 — allFlipped보다 먼저 본다.
                Penalize(lastStriker);

                if (Phase == PanchigiPhase.Over) { return; }
            }
            else if (allFlipped)
            {
                WinnerEntityId = lastStriker;   // 그 상태를 만든 사람
                Phase = PanchigiPhase.Over;
                return;
            }

            if (TurnCount >= turnLimit)
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

            if (TurnCount >= turnLimit)
            {
                CurrentEntityId = null;
                Phase = PanchigiPhase.Over;
                return;
            }

            EnterAiming();
        }

        /// <summary>낙 벌점을 매기고, 한도에 닿았으면 판에서 뺀다.</summary>
        private void Penalize(string entityId)
        {
            if (entityId == null || dropOutLimit <= 0) { return; }

            dropOutCounts.TryGetValue(entityId, out int count);
            dropOutCounts[entityId] = ++count;
            TotalDropOuts++;

            if (count < dropOutLimit) { return; }

            eliminated.Add(entityId);

            //  뺀 자리보다 뒤에 있던 사람들이 한 칸씩 앞으로 당겨진다 — 다음 차례 인덱스를 같이
            //  당기지 않으면 바로 다음 사람을 통째로 건너뛴다.
            int removedIndex = alive.IndexOf(entityId);
            alive.RemoveAt(removedIndex);
            if (removedIndex < nextIndex) { nextIndex--; }

            if (alive.Count == 1)
            {
                WinnerEntityId = alive[0];   // 마지막 한 사람
                CurrentEntityId = null;
                Phase = PanchigiPhase.Over;
            }
            else if (alive.Count == 0)
            {
                CurrentEntityId = null;
                Phase = PanchigiPhase.Over;   // 무승부
            }
        }

        private void EnterAiming()
        {
            if (alive.Count == 0)
            {
                Phase = PanchigiPhase.Over;
                return;
            }

            if (nextIndex >= alive.Count) { nextIndex = 0; }

            CurrentEntityId = alive[nextIndex];
            nextIndex = (nextIndex + 1) % alive.Count;
            Phase = PanchigiPhase.Aiming;
        }
    }
}
