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

            int statValue = 0;
            // wire stat 문자열은 소문자 필드명("strength" 등) — 클라가 보내는 기존 계약 유지.
            switch (statAllocationToS.Stat)
            {
                case "strength":
                    statValue = (int)statsSystem.AddBase(stats, (int)GameFramework.World.EntityStatType.Strength, 1);
                    break;
                case "dexterity":
                    statValue = (int)statsSystem.AddBase(stats, (int)GameFramework.World.EntityStatType.Dexterity, 1);
                    break;
                case "intelligence":
                    statValue = (int)statsSystem.AddBase(stats, (int)GameFramework.World.EntityStatType.Intelligence, 1);
                    break;
                case "vitality":
                    statValue = (int)statsSystem.AddBase(stats, (int)GameFramework.World.EntityStatType.Vitality, 1);
                    break;
            }

            entity.GetEntityComponent<PlayerComponent>().statPoints--;

            StatAllocationToC statAllocationToC = new StatAllocationToC
            {
                Stat = statAllocationToS.Stat,
                StatValue = statValue,
            };

            session.Send(statAllocationToC);
        }
    }
}
