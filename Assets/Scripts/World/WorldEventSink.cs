using GameFramework;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// WorldEventBuffer 스냅샷을 단일 폴리모픽 배치(WorldEventBatchToC)로 조립해 모든 세션에 1회 송출하는
    /// egress sink(서버). 코어 상태·새 이벤트 안 만듦. 개념별 패킷(DamageEventToC 등)은 배치 안의
    /// WorldEventToC(oneof) 레코드로 담긴다 — 새 WorldEvent 타입이 와이어 포맷을 흔들지 않음.
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
            var batch = new WorldEventBatchToC { Tick = Runner.Time.tick };
            foreach (var e in events)
            {
                var rec = WorldEventWire.ToWire(e);   // 매핑 없는 타입은 null → 무시
                if (rec != null)
                {
                    batch.Events.Add(rec);
                }
            }

            if (batch.Events.Count == 0)
            {
                return;   // 연출 이벤트 없는 틱은 송신 안 함
            }

            foreach (var session in _sessionManager.GetAllSessions())
            {
                session.Send(batch);   // 세션당 1패킷(기존 이벤트당 1패킷 → 배치)
            }
        }
    }
}
