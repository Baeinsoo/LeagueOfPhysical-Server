using GameFramework;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// WorldEventBuffer 드레인 이벤트에 반응해 서버 게임플레이 cascade를 일으키는 reactor(egress 아님).
    ///   DeathEvent → EventBus(EventTopic.Entity) → LOPGame.HandleDeath(디스폰+경험치 구슬).
    /// 향후 cascade(loot 등)는 여기 case 추가. (death→despawn을 Generation cascade로 옮기는 건 backlog #2-full.)
    /// </summary>
    public class WorldEventReactor
    {
        public void React(IReadOnlyList<GameFramework.World.WorldEvent> events)
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
