using System;
using Cysharp.Threading.Tasks;
using GameFramework;
using MessagePipe;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 결과가 정해진 사람(완주·탈락)이 판을 떠나겠다고 하면 로비에 그 사람 위치를 비워 달라고 하고 답한다.
    /// 이걸 안 하면 로비가 아직 "게임 방"이라는 위치를 보고 같은 방으로 다시 들여보낸다.
    /// 연결이 끊긴 것만으로는 비우지 않는다 — 끊긴 사람은 다시 들어와야 한다(재접속).
    /// </summary>
    public class LeaveMatchMessageHandler : MessageHandlerBase
    {
        private readonly ISubscriber<ClientMessage<LeaveMatchToS>> subscriber;
        private readonly IRoomDataStore roomDataStore;
        private readonly IGameRuleSystem gameRuleSystem;
        private readonly IUserLocationReleaser releaser;

        public LeaveMatchMessageHandler(ISubscriber<ClientMessage<LeaveMatchToS>> subscriber, IRoomDataStore roomDataStore,
            IGameRuleSystem gameRuleSystem, IUserLocationReleaser releaser)
        {
            this.subscriber = subscriber;
            this.roomDataStore = roomDataStore;
            this.gameRuleSystem = gameRuleSystem;
            this.releaser = releaser;
        }

        protected override void Subscribe() => Track(subscriber.Subscribe(received => HandleAsync(received.Session).Forget()));

        public async UniTask HandleAsync(ISession session)
        {
            string userId = session.userId;
            if (CanLeave(userId, out string reason) == false)
            {
                Debug.LogWarning($"[LeaveMatch] 거절 — {reason} — {userId}");
                session.Send(new LeaveMatchToC { Accepted = false });
                return;
            }

            bool accepted;
            try
            {
                await releaser.ReleaseAsync(userId, roomDataStore.room.id);
                accepted = true;
            }
            catch (Exception e)
            {
                //  클라는 거절을 받아도 로비로 간다 — 이 경우 같은 방으로 다시 들어가게 된다(나가기 전과 같은 결과).
                Debug.LogError($"[LeaveMatch] 로비 위치 비우기 실패 — {userId}: {e.Message}");
                accepted = false;
            }

            session.Send(new LeaveMatchToC { Accepted = accepted });
        }

        private bool CanLeave(string userId, out string reason)
        {
            if (Array.IndexOf(roomDataStore.match.playerList, userId) < 0)
            {
                reason = "참가자가 아니다";
                return false;
            }
            //  결과가 끝까지 안 정해지는 종목(점수제)은 나가기가 곧 기권이다 — 기권은 아직 없다.
            if (gameRuleSystem is not ISettledResults settled || settled.IsResultSettled(userId) == false)
            {
                reason = "결과가 아직 정해지지 않았다";
                return false;
            }
            reason = null;
            return true;
        }
    }
}
