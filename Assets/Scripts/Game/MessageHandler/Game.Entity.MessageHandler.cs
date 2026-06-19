using GameFramework;
using VContainer;

namespace LOP
{
    public class GameEntityMessageHandler : IGameMessageHandler
    {
        [Inject]
        private IGameEngine gameEngine;

        [Inject]
        private ISessionManager sessionManager;

        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        [Inject]
        private GameFramework.World.StatsSystem statsSystem;

        public void Register()
        {
            EventBus.Default.Subscribe<StatAllocationToS>(nameof(IMessage), OnStatAllocationToS);

            //RoomNetwork.instance.UnregisterHandler(typeof(EnterRoomToC), OnEnterRoomToC);
            //RoomNetwork.instance.UnregisterHandler(typeof(EntityStatesToC), OnEntityStatesToC);
        }

        public void Unregister()
        {
            EventBus.Default.Unsubscribe<StatAllocationToS>(nameof(IMessage), OnStatAllocationToS);

            //RoomNetwork.instance.UnregisterHandler(typeof(EnterRoomToC), OnEnterRoomToC);
            //RoomNetwork.instance.UnregisterHandler(typeof(EntityStatesToC), OnEntityStatesToC);
        }

        private void OnStatAllocationToS(StatAllocationToS statAllocationToS)
        {
            ISession session = sessionManager.GetSessionById(statAllocationToS.SessionId);
            LOPEntity entity = gameEngine.entityManager.GetEntityByUserId<LOPEntity>(session.userId);
            GameFramework.World.Stats stats = entityRegistry.Get(entity.entityId)?.Get<GameFramework.World.Stats>();
            if (stats == null)
            {
                UnityEngine.Debug.LogWarning($"[World] StatAllocation: Stats not found for entity {entity.entityId}");
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
