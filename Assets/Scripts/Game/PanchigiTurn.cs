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
    /// 판치기 한 판의 진행(골프식 — 같은 배치에서 적은 타수로 다 뒤집기). 물리도 시계도 모르고
    /// "무슨 일이 있었나"만 받아 다음 국면과 타수를 정한다. 판은 사람마다 따로 쌓이는데, 동전 자세를
    /// 바꿔 끼우는 일은 부르는 쪽(<see cref="PanchigiTurnSystem"/>)이 한다.
    /// </summary>
    public class PanchigiTurn
    {
        private readonly int strokeLimit;

        private readonly Dictionary<string, int> strokes = new();
        private readonly HashSet<string> finished = new();
        private readonly List<string> active = new();

        private int nextIndex;

        public PanchigiPhase Phase { get; private set; } = PanchigiPhase.Settling;

        /// <summary>지금 칠 차례인 사람. <see cref="PanchigiPhase.Aiming"/>이 아니면 null.</summary>
        public string CurrentEntityId { get; private set; }

        /// <summary>방금 친 사람 — 멎은 판을 누구 몫으로 저장할지 부르는 쪽이 여기서 안다.</summary>
        public string LastStrikerEntityId { get; private set; }

        /// <summary>
        /// 친 것과 시간 초과를 모두 센다. 조준 마감을 "새 조준마다 한 번" 정하는 데 쓴다 —
        /// 혼자 남아 같은 사람이 연달아 받아도 이 값은 바뀐다.
        /// </summary>
        public int TurnCount { get; private set; }

        /// <summary>벌타와 상한 기록까지 합친 모두의 타수. "무언가 달라졌나"를 싸게 보려는 용도다.</summary>
        public int TotalStrokes { get; private set; }

        /// <summary>사람별 타수. 끝난 사람은 기록 타수(상한에 닿았으면 상한+1).</summary>
        public IReadOnlyDictionary<string, int> Strokes => strokes;

        /// <summary>홀아웃했거나 상한에 닿아 순번에서 빠진 사람들.</summary>
        public IReadOnlyCollection<string> FinishedEntityIds => finished;

        public PanchigiTurn(IReadOnlyList<string> playerEntityIds, int strokeLimit)
        {
            this.strokeLimit = strokeLimit;
            active.AddRange(playerEntityIds);
            foreach (string id in playerEntityIds)
            {
                strokes[id] = 0;
            }
        }

        public int GetStrokes(string entityId)
        {
            return entityId != null && strokes.TryGetValue(entityId, out int count) ? count : 0;
        }

        public bool IsFinished(string entityId)
        {
            return entityId != null && finished.Contains(entityId);
        }

        /// <summary>
        /// 동전이 모두 멎었다. 판 시작 직후에도 한 번 온다(그땐 아무도 안 쳤다).
        /// </summary>
        /// <param name="allFlipped">친 사람의 판이 전부 뒤집혔다 — 홀아웃.</param>
        /// <param name="droppedOut">동전이 판 밖으로 나갔다 — 1벌타. 판은 부르는 쪽이 처음 배치로 되돌렸다.</param>
        public void OnRested(bool allFlipped, bool droppedOut)
        {
            if (Phase != PanchigiPhase.Settling) { return; }

            string striker = LastStrikerEntityId;
            if (striker != null)
            {
                //  낙을 먼저 본다 — 되돌린 판에는 뒤집힌 동전이 없으니 같은 타에 홀아웃할 수 없다.
                if (droppedOut)
                {
                    AddStroke(striker);
                }
                else if (allFlipped)
                {
                    Finish(striker);
                }

                FinishIfAtLimit(striker);
            }

            EnterAiming();
        }

        public void OnStruck(string entityId)
        {
            if (Phase != PanchigiPhase.Aiming) { return; }

            LastStrikerEntityId = entityId;
            TurnCount++;
            AddStroke(entityId);
            CurrentEntityId = null;
            Phase = PanchigiPhase.Settling;
        }

        /// <summary>조준 시간을 넘겼다 — 1타로 치고 넘어간다. 물리를 안 건드리므로 Settling을 거치지 않는다.</summary>
        public void OnAimTimeout()
        {
            if (Phase != PanchigiPhase.Aiming) { return; }

            string passer = CurrentEntityId;
            TurnCount++;
            AddStroke(passer);
            FinishIfAtLimit(passer);
            EnterAiming();
        }

        private void AddStroke(string entityId)
        {
            strokes[entityId] = GetStrokes(entityId) + 1;
            TotalStrokes++;
        }

        /// <summary>상한에 닿았는데 못 끝냈으면 상한+1로 기록하고 뺀다. 벌타로 넘어간 만큼은 세지 않는다.</summary>
        private void FinishIfAtLimit(string entityId)
        {
            if (strokeLimit <= 0 || finished.Contains(entityId)) { return; }

            int count = GetStrokes(entityId);
            if (count < strokeLimit) { return; }

            TotalStrokes += strokeLimit + 1 - count;
            strokes[entityId] = strokeLimit + 1;
            Finish(entityId);
        }

        private void Finish(string entityId)
        {
            finished.Add(entityId);

            //  뺀 자리보다 뒤에 있던 사람들이 한 칸씩 앞으로 당겨진다 — 다음 차례 인덱스를 같이
            //  당기지 않으면 바로 다음 사람을 통째로 건너뛴다.
            int removedIndex = active.IndexOf(entityId);
            active.RemoveAt(removedIndex);
            if (removedIndex < nextIndex) { nextIndex--; }
        }

        private void EnterAiming()
        {
            if (active.Count == 0)
            {
                CurrentEntityId = null;
                Phase = PanchigiPhase.Over;
                return;
            }

            if (nextIndex >= active.Count) { nextIndex = 0; }

            CurrentEntityId = active[nextIndex];
            nextIndex = (nextIndex + 1) % active.Count;
            Phase = PanchigiPhase.Aiming;
        }
    }
}
