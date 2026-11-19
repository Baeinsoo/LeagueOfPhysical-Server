using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 삼색 그랩 상태 방송(서버 End 페이즈, 맨 마지막). 판본이 바뀌면 모든 세션에 한 번씩 보낸다.
    /// 끊긴 세션은 받은 목록에서 지워 두어, 다시 연결되면 지금 상태를 한 번 더 받는다(DodgeStateBroadcastSystem과 같다).
    /// </summary>
    public sealed class TriGrabStateBroadcastSystem : GameFramework.Runner.ITickSystem
    {
        private readonly TriGrabMatch match;
        private readonly GameFramework.ISessionManager sessionManager;
        private readonly HashSet<string> receivedSessionIds = new HashSet<string>();
        private int sentVersion;

        public TriGrabStateBroadcastSystem(TriGrabMatch match, GameFramework.ISessionManager sessionManager)
        {
            this.match = match;
            this.sessionManager = sessionManager;
        }

        public void Tick(long tick, float deltaTime)
        {
            var state = match.State;
            if (state.Version == 0) return;
            if (state.Version != sentVersion)
            {
                sentVersion = state.Version;
                receivedSessionIds.Clear();
            }

            TriGrabStateToC message = null;
            foreach (var session in sessionManager.GetAllSessions())
            {
                if (session.isConnected == false)
                {
                    receivedSessionIds.Remove(session.sessionId);
                    continue;
                }
                if (receivedSessionIds.Contains(session.sessionId)) continue;
                message ??= ToWire(state);
                session.Send(message);
                receivedSessionIds.Add(session.sessionId);
            }
        }

        public static TriGrabStateToC ToWire(TriGrabMatchState state)
        {
            var m = new TriGrabStateToC
            {
                Version = state.Version,
                Phase = (int)state.Phase,
                PhaseEndTick = state.PhaseEndTick,
                Round = state.Round,
                RuleType = (int)state.Rule.Type,
                RuleColor = state.Rule.Color,
                BaseAngle0 = state.Clock.BaseAngle0,
                Omega = state.Clock.Omega,
                LiveStartTick = state.Clock.LiveStartTick,
                FreezeTick = state.Clock.FreezeTick,
                RoundWinnerId = state.RoundWinnerId ?? "",
                TargetScore = TriGrabConfig.TargetScore,
                MatchWinnerId = state.MatchWinnerId ?? "",
            };
            foreach (var slot in state.Slots)
            {
                var w = new TriGrabSlotWire { EmptyUntilTick = slot.EmptyUntilTick };
                if (slot.Card.HasValue) w.Card = CardToWire(slot.Card.Value);
                m.Slots.Add(w);
            }
            foreach (var h in state.Hooks)
            {
                var w = new TriGrabHookWire
                {
                    OwnerId = h.OwnerId, FireTick = h.FireTick,
                    OriginX = h.Origin.x, OriginZ = h.Origin.y, DirX = h.Dir.x, DirZ = h.Dir.y,
                    TurnTick = h.TurnTick, TurnLength = h.TurnLength,
                    Result = (int)h.Result, EndTick = h.EndTick,
                };
                if (h.Carried.HasValue) w.Carried = CardToWire(h.Carried.Value);
                m.Hooks.Add(w);
            }
            foreach (var id in state.PlayerOrder)
            {
                var p = state.Players[id];
                m.Players.Add(new TriGrabPlayerWire
                {
                    EntityId = id, Score = p.Score, Mistakes = p.Mistakes,
                    ReloadUntilTick = p.ReloadUntilTick, BestRtMs = p.BestRtMs,
                });
            }
            return m;
        }

        private static TriGrabCardWire CardToWire(TriGrabCard c) => new TriGrabCardWire
        {
            Id = c.Id, Word = c.Word, Ink = c.Ink, Cycles = c.Cycles, PeriodTicks = c.PeriodTicks, T0Tick = c.T0Tick,
        };
    }
}
