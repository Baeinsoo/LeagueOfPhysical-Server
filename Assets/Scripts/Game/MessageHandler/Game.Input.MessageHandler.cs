using GameFramework;
using MessagePipe;
using VContainer;

namespace LOP
{
    public class GameInputMessageHandler : IGameMessageHandler
    {
        [Inject]
        private IRunner runner;

        [Inject]
        private ISessionManager sessionManager;

        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        [Inject]
        private InputBufferSystem inputBufferSystem;

        [Inject]
        private ISubscriber<InputCommandToS> inputCommandSubscriber;

        private System.IDisposable subscription;

        public void Initialize()
        {
            subscription = inputCommandSubscriber.Subscribe(OnInputCommandToS);
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }

        private void OnInputCommandToS(InputCommandToS inputCommandToS)
        {
            ISession session = sessionManager.GetSessionById(inputCommandToS.SessionId);
            string entityId = (runner as LOPRunner).entityManager.GetEntityIdByUserId(session.userId);
            var buffer = entityRegistry.Get(entityId).Get<InputBuffer>();
            if (buffer == null)
            {
                return;
            }

            // sliding-window redundancy: recent_inputs의 각 틱을 투입(이미 있는 tick/처리된 seq는 Enqueue가 dedup).
            // 유실된 틱이 다음 패킷의 redundancy로 채워진다.
            // 와이어(proto) → 도메인(InputCommand) 변환은 여기(수신 어댑터)까지 — 버퍼부터는 도메인 타입만.
            foreach (var entry in inputCommandToS.RecentInputs)
            {
                if (inputBufferSystem.Enqueue(buffer, entry.Tick, ToInputCommand(entry.InputCommand)))
                {
                    buffer.TimingTracker.RecordArrival((int)(Runner.Time.tick - entry.Tick));
                }
            }
        }

        private static InputCommand ToInputCommand(global::InputCommand inputCommand)
        {
            return new InputCommand
            {
                SequenceNumber = inputCommand.SequenceNumber,
                Horizontal = inputCommand.Horizontal,
                Vertical = inputCommand.Vertical,
                Jump = inputCommand.Jump,
                AbilityId = inputCommand.AbilityId,
            };
        }
    }
}
