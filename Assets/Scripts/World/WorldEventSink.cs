using GameFramework;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// WorldEventBuffer 스냅샷을 와이어 메시지로 변환해 모든 세션에 송출하는 egress sink(서버). 출구=네트워크.
    /// 코어 상태·새 이벤트 안 만듦.
    ///   DamageDealtEvent → DamageEventToC → session.Send (모든 세션)
    /// </summary>
    public class WorldEventSink : GameFramework.World.IEventSink
    {
        private readonly ISessionManager _sessionManager;

        public WorldEventSink(ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public void Emit(IReadOnlyList<GameFramework.World.WorldEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case GameFramework.World.DamageDealtEvent dde:
                    {
                        var msg = new DamageEventToC
                        {
                            Tick        = GameEngine.Time.tick,
                            AttackerId  = dde.attackerId,
                            TargetId    = dde.targetId,
                            ActionCode  = "attack",
                            DamageType  = "physical",
                            Damage      = dde.amount,
                            IsCritical  = dde.isCritical,
                            IsDodged    = dde.isDodged,
                            IsBlocked   = false,
                        };
                        foreach (var session in _sessionManager.GetAllSessions())
                        {
                            session.Send(msg);
                        }
                        break;
                    }
                }
            }
        }
    }
}
