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
        private ISessionManager sessionManager;

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
            PlayerInput playerInput = new PlayerInput
            {
                tick = playerInputToS.Tick,
                horizontal = playerInputToS.PlayerInput.Horizontal,
                vertical = playerInputToS.PlayerInput.Vertical,
                jump = playerInputToS.PlayerInput.Jump,
                actionCode = playerInputToS.PlayerInput.ActionCode,
                sequenceNumber = playerInputToS.PlayerInput.SequenceNumber,
            };

            ISession session = sessionManager.GetSessionById(playerInputToS.SessionId);
            LOPEntity entity = gameEngine.entityManager.GetEntityByUserId<LOPEntity>(session.userId);
            entity.GetEntityComponent<EntityInputComponent>().AddInput(playerInputToS);
        }
    }
}
