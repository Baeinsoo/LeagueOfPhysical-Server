using System.Collections.Generic;
using GameFramework;

namespace LOP
{
    /// <summary>구 LOPRunner.ProcessInput 이동. 조종 엔티티별 입력을 소비해 이동/어빌리티에 반영하고, 처리 시퀀스를 클라에 통보한다.</summary>
    public class ServerInputSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly InputBufferSystem inputBufferSystem;
        private readonly AbilityActivator abilityActivator;
        private readonly ISessionManager sessionManager;

        public ServerInputSystem(GameFramework.World.EntityRegistry entityRegistry, InputBufferSystem inputBufferSystem, AbilityActivator abilityActivator, ISessionManager sessionManager)
        {
            this.entityRegistry = entityRegistry;
            this.inputBufferSystem = inputBufferSystem;
            this.abilityActivator = abilityActivator;
            this.sessionManager = sessionManager;
        }

        public void Tick(long tick, float deltaTime)
        {
            List<GameFramework.World.Entity> worldEntities = new List<GameFramework.World.Entity>(entityRegistry.All);

            foreach (var worldEntity in worldEntities)
            {
                var buffer = worldEntity.Get<InputBuffer>();
                if (buffer == null)
                {
                    continue;   // 입력 비조종(AI 등) — 버퍼 없음
                }

                // 입력을 스탬프된 틱에 제때 처리 — 클라 예측(즉시 적용)과 정렬(offset 0). 이건 하드 롤백 재조정의 전제다:
                // 서버를 늦추면(입력을 과거 틱에 소비) 클라 예측과 항상 어긋나 낙하·충돌에서 발산한다. 늦추지 말 것.
                // 지터로 입력이 늦게 도착할 여유가 더 필요하면 서버가 아니라 클라 lead(AheadMargin)를 키운다(표준).
                // command-frame 정렬 + 지각 prune → 이번 틱 커맨드 확정(Current). 소비는 LOPWorld.Tick(MovementSystem).
                long targetTick = tick;
                int pruned = inputBufferSystem.PruneBefore(buffer, targetTick);
                for (int i = 0; i < pruned; i++)
                {
                    buffer.TimingTracker.RecordPrune();
                }

                long previousSequence = buffer.LastProcessedSequence;
                var input = inputBufferSystem.Consume(buffer, targetTick);

                if (input == null)
                {
                    // 미스 → 0 커맨드 확정(수평 제동). 어빌리티/시퀀스 송신은 입력 있을 때만.
                    inputBufferSystem.SetCurrent(buffer, new InputCommand());
                    continue;
                }

                long gap = input.SequenceNumber - previousSequence - 1;
                if (gap > 0)
                {
                    buffer.TimingTracker.RecordSeqGap((int)gap);
                }

                if (input.AbilityId != 0)
                {
                    // 발동 연출 cue는 AbilityActivator가 내부에서 append한다(플레이어·AI 공용).
                    abilityActivator.TryActivate(worldEntity.Id, input.AbilityId, tick);
                }

                InputSequenceToC inputSequnceToC = new InputSequenceToC();
                inputSequnceToC.EntityId = worldEntity.Id;
                inputSequnceToC.InputSequence = new InputSequence
                {
                    Tick = tick,
                    Sequence = input.SequenceNumber,
                };

                string userId = worldEntity.Get<GameFramework.World.Ownership>()?.OwnerId;
                ISession session = sessionManager.GetSessionByUserId(userId);
                session.Send(inputSequnceToC);
            }
        }
    }
}
