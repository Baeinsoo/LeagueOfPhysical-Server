using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameInputMessageHandler : IGameMessageHandler
    {
        [Inject]
        private IGameEngine gameEngine;

        [Inject]
        private IMessageDispatcher messageDispatcher;

        public void Register()
        {
            EventBus.Default.Subscribe<PlayerInputToS>(nameof(IMessage), OnPlayerInputToS);
        }

        public void Unregister()
        {
            EventBus.Default.Unsubscribe<PlayerInputToS>(nameof(IMessage), OnPlayerInputToS);
        }

        private void OnPlayerInputToS(PlayerInputToS playerInputToS)
        {
            LOPEntity entity = gameEngine.entityManager.GetEntity<LOPEntity>(playerInputToS.EntityId);

            PlayerInput playerInput = new PlayerInput
            {
                tick = playerInputToS.Tick,
                horizontal = playerInputToS.PlayerInput.Horizontal,
                vertical = playerInputToS.PlayerInput.Vertical,
                jump = playerInputToS.PlayerInput.Jump,
                actionCode = playerInputToS.PlayerInput.ActionCode,
                sequenceNumber = playerInputToS.PlayerInput.SequenceNumber,
            };

            entity.GetEntityComponent<EntityInputComponent>().AddInput(playerInputToS);
        }
    }
}
