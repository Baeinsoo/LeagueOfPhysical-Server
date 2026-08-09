using GameFramework;
using GameFramework.Runner;
using MessagePipe;

namespace LOP
{
    public class GameInputMessageHandler : MessageHandlerBase
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly InputBufferSystem inputBufferSystem;
        private readonly EntitySpawner entitySpawner;
        private readonly ISubscriber<ClientMessage<InputCommandToS>> inputCommandSubscriber;
        private readonly ITickUpdater tickUpdater;

        public GameInputMessageHandler(
            GameFramework.World.EntityRegistry entityRegistry,
            InputBufferSystem inputBufferSystem,
            EntitySpawner entitySpawner,
            ISubscriber<ClientMessage<InputCommandToS>> inputCommandSubscriber,
            ITickUpdater tickUpdater)
        {
            this.entityRegistry = entityRegistry;
            this.inputBufferSystem = inputBufferSystem;
            this.entitySpawner = entitySpawner;
            this.inputCommandSubscriber = inputCommandSubscriber;
            this.tickUpdater = tickUpdater;
        }

        protected override void Subscribe() => Track(inputCommandSubscriber.Subscribe(OnInputCommandToS));

        private void OnInputCommandToS(ClientMessage<InputCommandToS> received)
        {
            //  세션은 연결에서 이미 찾아 왔다 — 여기서 계정으로 다시 조회하지 않는다.
            ISession session = received.Session;
            string entityId = entitySpawner.GetEntityIdByUserId(session.userId);
            var buffer = entityRegistry.Get(entityId).Get<InputBuffer>();
            if (buffer == null)
            {
                return;
            }

            // sliding-window redundancy: recent_inputs의 각 틱을 투입(이미 있는 tick/처리된 seq는 Enqueue가 dedup).
            // 유실된 틱이 다음 패킷의 redundancy로 채워진다.
            // 와이어(proto) → 도메인(InputCommand) 변환은 여기(수신 어댑터)까지 — 버퍼부터는 도메인 타입만.
            foreach (var entry in received.Message.RecentInputs)
            {
                if (inputBufferSystem.Enqueue(buffer, entry.Tick, ToInputCommand(entry.InputCommand)))
                {
                    buffer.TimingTracker.RecordArrival((int)(tickUpdater.tick - entry.Tick));
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
