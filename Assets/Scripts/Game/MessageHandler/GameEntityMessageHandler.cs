using GameFramework;
using MessagePipe;

namespace LOP
{
    public class GameEntityMessageHandler : MessageHandlerBase
    {
        private readonly ISessionManager sessionManager;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.StatsSystem statsSystem;
        private readonly EntitySpawner entitySpawner;
        private readonly ISubscriber<StatAllocationToS> statAllocationSubscriber;

        public GameEntityMessageHandler(
            ISessionManager sessionManager,
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.StatsSystem statsSystem,
            EntitySpawner entitySpawner,
            ISubscriber<StatAllocationToS> statAllocationSubscriber)
        {
            this.sessionManager = sessionManager;
            this.entityRegistry = entityRegistry;
            this.statsSystem = statsSystem;
            this.entitySpawner = entitySpawner;
            this.statAllocationSubscriber = statAllocationSubscriber;
        }

        protected override void Subscribe() => Track(statAllocationSubscriber.Subscribe(OnStatAllocationToS));

        private void OnStatAllocationToS(StatAllocationToS statAllocationToS)
        {
            ISession session = sessionManager.GetSessionById(statAllocationToS.SessionId);
            string entityId = entitySpawner.GetEntityIdByUserId(session.userId);
            GameFramework.World.Stats stats = entityRegistry.Get(entityId)?.Get<GameFramework.World.Stats>();
            if (stats == null)
            {
                UnityEngine.Debug.LogWarning($"[World] StatAllocation: Stats not found for entity {entityId}");
                return;
            }

            int statType;
            // wire stat 문자열은 소문자 필드명("strength" 등) — 클라가 보내는 기존 계약 유지.
            switch (statAllocationToS.Stat)
            {
                case "strength": statType = (int)GameFramework.World.EntityStatType.Strength; break;
                case "dexterity": statType = (int)GameFramework.World.EntityStatType.Dexterity; break;
                case "intelligence": statType = (int)GameFramework.World.EntityStatType.Intelligence; break;
                case "vitality": statType = (int)GameFramework.World.EntityStatType.Vitality; break;
                default: return;
            }

            int statValue = statsSystem.Allocate(stats, statType);

            StatAllocationToC statAllocationToC = new StatAllocationToC
            {
                Stat = statAllocationToS.Stat,
                StatValue = statValue,
            };

            session.Send(statAllocationToC);
        }
    }
}
