using System.Collections.Generic;
using GameFramework;

namespace LOP
{
    /// <summary>
    /// 지금 웨이브에서 어느 과녁이 먹혔는지를 모두에게 알린다.
    ///
    /// <para><b>왜 사건이 아니라 상태인가.</b> 적중 사건은 reliable로 가서 연결된 사람은 안 놓치지만,
    /// 미러는 <b>새 연결에 지난 메시지를 다시 틀어 주지 않는다</b> — 끊겼다 돌아온 사람은 이미 먹힌
    /// 과녁을 살아 있는 것으로 보고, 스스로는 그게 틀렸다는 것조차 알 수 없다. 그래서 "지금 남은
    /// 과녁"은 상태로 보낸다(<see cref="PanchigiStateToC"/>와 같은 사정·같은 방식).</para>
    ///
    /// <para>마스크가 0인 웨이브는 보내지 않는다 — 아무것도 안 먹힌 것이 클라의 기본값이라 보낼 것이
    /// 없다. 웨이브가 넘어가 마스크가 0으로 돌아간 것도 보낼 필요가 없다: 메시지에 웨이브 번호가
    /// 실려 있어 받는 쪽이 자기 웨이브와 다른 소식을 버린다.</para>
    /// </summary>
    public class ArcheryStateBroadcastSystem : GameFramework.Runner.ITickSystem
    {
        private readonly ArcheryWaveState waveState;
        private readonly ISessionManager sessionManager;

        private readonly HashSet<string> receivedSessionIds = new HashSet<string>();
        private int sentWaveIndex = -1;
        private int sentMask;

        public ArcheryStateBroadcastSystem(ArcheryWaveState waveState, ISessionManager sessionManager)
        {
            this.waveState = waveState;
            this.sessionManager = sessionManager;
        }

        public void Tick(long tick, float deltaTime)
        {
            if (waveState.ConsumedMask == 0)
            {
                return;   // 아무것도 안 먹혔다 = 클라의 기본값과 같다
            }

            if (waveState.WaveIndex != sentWaveIndex || waveState.ConsumedMask != sentMask)
            {
                sentWaveIndex = waveState.WaveIndex;
                sentMask = waveState.ConsumedMask;
                receivedSessionIds.Clear();   // 새 소식이다 — 모두 다시 받아야 한다
            }

            var message = new ArcheryStateToC
            {
                WaveIndex = waveState.WaveIndex,
                ConsumedMask = waveState.ConsumedMask,
            };

            foreach (var session in sessionManager.GetAllSessions())
            {
                if (session.isConnected == false)
                {
                    //  끊긴 세션은 "받은 적 없음"으로 되돌린다 — 재접속은 같은 sessionId를 그대로
                    //  다시 쓰므로(LOPRoom.OnPlayerEnter가 세션 객체를 재사용한다), 지워 두지 않으면
                    //  돌아온 사람이 이 웨이브의 상태를 통째로 놓친다.
                    receivedSessionIds.Remove(session.sessionId);
                    continue;
                }

                if (receivedSessionIds.Contains(session.sessionId))
                {
                    continue;
                }

                session.Send(message);
                receivedSessionIds.Add(session.sessionId);
            }
        }
    }
}
