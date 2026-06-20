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
            ISession session = sessionManager.GetSessionById(playerInputToS.SessionId);
            LOPEntity entity = gameEngine.entityManager.GetEntityByUserId<LOPEntity>(session.userId);
            EntityInputComponent inputComponent = entity.GetEntityComponent<EntityInputComponent>();

            // sliding-window redundancy: recent_inputs의 각 틱을 투입(이미 있는 tick은 AddInput이 dedup).
            // 유실된 틱이 다음 패킷의 redundancy로 채워진다.
            foreach (var entry in playerInputToS.RecentInputs)
            {
                inputComponent.AddInput(entry.Tick, entry.PlayerInput);
            }
        }
    }
}
