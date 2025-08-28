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
            LOPEntity entity = gameEngine.entityManager.GetEntity<LOPEntity>(statAllocationToS.EntityId);
            StatsComponent statsComponent = entity.GetEntityComponent<StatsComponent>();
            int statValue = 0;
            switch (statAllocationToS.Stat)
            {
                case nameof(StatsComponent.strength):
                    statsComponent.strength++;
                    statValue = statsComponent.strength;
                    break;

                case nameof(StatsComponent.dexterity):
                    statValue = statsComponent.dexterity++;
                    statValue = statsComponent.dexterity;
                    break;

                case nameof(StatsComponent.intelligence):
                    statValue = statsComponent.intelligence++;
                    statValue = statsComponent.intelligence;
                    break;

                case nameof(StatsComponent.vitality):
                    statValue = statsComponent.vitality++;
                    statValue = statsComponent.vitality;
                    break;
            }

            entity.GetEntityComponent<PlayerComponent>().statPoints--;

            string userId = gameEngine.entityManager.GetUserIdByEntityId(entity.entityId);
            var session = sessionManager.GetSessionByUserId(userId);

            StatAllocationToC statAllocationToC = new StatAllocationToC
            {
                EntityId = entity.entityId,
                Stat = statAllocationToS.Stat,
                StatValue = statValue,
            };

            session.Send(statAllocationToC);
        }
    }
}
