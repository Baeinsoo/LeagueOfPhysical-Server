using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 피하기 상태 방송(서버 End 페이즈, 맨 마지막). 판본이 바뀌면 모든 세션에 한 번씩 보낸다.
    /// 끊긴 세션은 받은 목록에서 지워 두어, 다시 연결되면(같은 sessionId) 지금 상태를 한 번 더 받는다.
    /// ArcheryStateBroadcastSystem과 같은 모양이다.
    /// </summary>
    public class DodgeStateBroadcastSystem : GameFramework.Runner.ITickSystem
    {
        private readonly DodgeMatchState state;
        private readonly GameFramework.ISessionManager sessionManager;
        private readonly HashSet<string> receivedSessionIds = new HashSet<string>();
        private int sentVersion;

        public DodgeStateBroadcastSystem(DodgeMatchState state, GameFramework.ISessionManager sessionManager)
        {
            this.state = state;
            this.sessionManager = sessionManager;
        }

        public void Tick(long tick, float deltaTime)
        {
            if (state.Version == 0)
            {
                return;   // 아직 아무것도 없다 — 클라 기본값과 같다
            }
            if (state.Version != sentVersion)
            {
                sentVersion = state.Version;
                receivedSessionIds.Clear();
            }

            DodgeStateToC message = null;
            foreach (var session in sessionManager.GetAllSessions())
            {
                if (session.isConnected == false)
                {
                    receivedSessionIds.Remove(session.sessionId);
                    continue;
                }
                if (receivedSessionIds.Contains(session.sessionId))
                {
                    continue;
                }
                message ??= ToWire(state);
                session.Send(message);
                receivedSessionIds.Add(session.sessionId);
            }
        }

        public static DodgeStateToC ToWire(DodgeMatchState state)
        {
            var message = new DodgeStateToC { Version = state.Version };
            foreach (var p in state.Patterns)
            {
                var w = new DodgePatternWire { Id = p.Id, Kind = (int)p.Kind, StartTick = p.StartTick, Seed = p.Seed };
                w.P.Add(p.P0); w.P.Add(p.P1); w.P.Add(p.P2); w.P.Add(p.P3);
                message.Patterns.Add(w);
            }
            foreach (var kv in state.Players)
            {
                message.Players.Add(new DodgePlayerWire
                {
                    EntityId = kv.Key,
                    Lives = kv.Value.Lives,
                    InvulnerableUntilTick = kv.Value.InvulnerableUntilTick,
                    EliminatedTick = kv.Value.EliminatedTick,
                });
            }
            return message;
        }
    }
}
