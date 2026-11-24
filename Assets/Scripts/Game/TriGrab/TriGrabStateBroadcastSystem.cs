using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 삼색 그랩 상태 방송(서버 End 페이즈, 맨 마지막).
    /// 상태(<c>TriGrabRingStateToC</c>)는 판본이 바뀔 때 모든 세션에 신뢰 전송으로 한 번씩 보낸다 — 연출 사건(fx)은 틱마다
    /// 비워지므로 판엔진이 굴러간 바로 이 틱에 만들어 보내야 한 번씩만 나간다.
    /// 끊긴 세션은 받은 목록에서 지워 두어, 다시 연결되면 지금 상태를 한 번 더 받는다(DodgeStateBroadcastSystem과 같다).
    /// 카드 자리(<c>TriGrabCardsToC</c>)는 Live 중 짝수 틱마다 비신뢰로 보낸다(Resolved·Prep엔 카드가 안 움직인다).
    /// </summary>
    public sealed class TriGrabStateBroadcastSystem : GameFramework.Runner.ITickSystem
    {
        private readonly LOP.TriGrab.TriGrabMatch match;
        private readonly GameFramework.ISessionManager sessionManager;
        private readonly HashSet<string> receivedSessionIds = new HashSet<string>();
        private int sentVersion;

        public TriGrabStateBroadcastSystem(LOP.TriGrab.TriGrabMatch match, GameFramework.ISessionManager sessionManager)
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

            bool sendCards = state.Phase == LOP.TriGrab.TriGrabPhase.Live && tick % 2 == 0;
            TriGrabRingStateToC stateMessage = null;
            TriGrabCardsToC cardsMessage = null;
            foreach (var session in sessionManager.GetAllSessions())
            {
                if (session.isConnected == false)
                {
                    receivedSessionIds.Remove(session.sessionId);
                    continue;
                }
                if (receivedSessionIds.Contains(session.sessionId) == false)
                {
                    stateMessage ??= LOP.TriGrab.TriGrabWire.ToWire(match, tick);
                    session.Send(stateMessage);
                    receivedSessionIds.Add(session.sessionId);
                }
                if (sendCards)
                {
                    cardsMessage ??= LOP.TriGrab.TriGrabWire.CardsToWire(state, tick);
                    session.Send(cardsMessage, false);
                }
            }
        }
    }
}
