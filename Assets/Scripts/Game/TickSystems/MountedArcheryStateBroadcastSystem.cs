using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 말 타고 활쏘기 상태 방송(서버 End 페이즈, 맨 마지막). 판본이 바뀌면 모든 세션에 한 번씩,
    /// 새로 붙은 세션에도 한 번 보낸다. DodgeStateBroadcastSystem과 같은 모양이다.
    /// </summary>
    public class MountedArcheryStateBroadcastSystem : GameFramework.Runner.ITickSystem
    {
        private readonly MountedArcheryMatchState state;
        private readonly GameFramework.ISessionManager sessionManager;
        private readonly HashSet<string> receivedSessionIds = new HashSet<string>();
        private int sentVersion;

        public MountedArcheryStateBroadcastSystem(MountedArcheryMatchState state, GameFramework.ISessionManager sessionManager)
        {
            this.state = state;
            this.sessionManager = sessionManager;
        }

        public void Tick(long tick, float deltaTime)
        {
            if (state.Version == 0)
            {
                return;
            }
            if (state.Version != sentVersion)
            {
                sentVersion = state.Version;
                receivedSessionIds.Clear();
            }

            MountedArcheryStateToC message = null;
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

        public static MountedArcheryStateToC ToWire(MountedArcheryMatchState state)
        {
            var message = new MountedArcheryStateToC { Version = state.Version };
            foreach (var kv in state.Players)
            {
                message.Players.Add(new MountedArcheryPlayerWire
                {
                    EntityId = kv.Key,
                    EliminatedTick = kv.Value.EliminatedTick,
                    KillerEntityId = kv.Value.KillerEntityId,
                    DisplayName = kv.Value.DisplayName ?? "",
                });
            }
            return message;
        }
    }
}
