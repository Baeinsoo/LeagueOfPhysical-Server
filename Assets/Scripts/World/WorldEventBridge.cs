using GameFramework;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// WorldEventBuffer 드레인 이벤트를 서버 내부 EventBus로 fan-out한다(게임 반응용).
    /// 클라 WorldEventBridge의 서버 대응 — wire 송신(WireBroadcaster)과 별개 sink.
    /// 처리: DeathEvent → EventBus(EventTopic.Entity) → LOPGame.HandleDeath(디스폰+경험치 구슬).
    /// 향후 cascade(loot 등)는 여기 case 추가.
    /// </summary>
    public class WorldEventBridge
    {
        public void FanOut(IReadOnlyList<GameFramework.World.WorldEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case GameFramework.World.DeathEvent death:
                        EventBus.Default.Publish(EventTopic.Entity, death);
                        break;
                }
            }
        }
    }
}
