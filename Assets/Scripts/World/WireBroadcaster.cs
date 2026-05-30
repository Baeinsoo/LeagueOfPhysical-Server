using GameFramework;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 서버판 "Bridge" 대응물. WorldEventBuffer 스냅샷을 와이어 메시지로 변환해
    /// 모든 세션에 broadcast한다. 클라의 WorldEventBridge가 같은 데이터를 로컬
    /// 프레젠테이션(EventBus)으로 fan-out하는 것과 동형 — 단 출구가 네트워크.
    ///
    /// 슬라이스 3 처리 이벤트:
    ///   DamageDealtEvent → DamageEventToC → session.Send (모든 세션)
    ///   DeathEvent       → Debug.Log only (별도 wire 메시지 없음; future-proof 자리)
    ///
    /// Generation(LOPCombatSystem)은 Append만, Application(WorldEventApplicator)은
    /// 코어 상태 쓰기만, Broadcaster는 송신만 — SRP 분리.
    /// </summary>
    public class WireBroadcaster
    {
        private readonly ISessionManager _sessionManager;

        public WireBroadcaster(ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public void Broadcast(IReadOnlyList<GameFramework.World.WorldEvent> events)
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
                            RemainingHP = dde.remaining,
                            IsDead      = dde.isDead,
                        };
                        foreach (var session in _sessionManager.GetAllSessions())
                        {
                            session.Send(msg);
                        }
                        break;
                    }
                    case GameFramework.World.DeathEvent de:
                        Debug.Log($"[World] Death entity {de.victimId} (killer={de.attackerId})");
                        break;
                }
            }
        }
    }
}
