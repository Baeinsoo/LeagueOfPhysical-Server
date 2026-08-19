using System.Collections.Generic;
using GameFramework;

namespace LOP
{
    /// <summary>구 LOPRunner.ProcessInput 이동. 조종 엔티티별로 이번 틱 커맨드를 확정(Current)만 한다 —
    /// 그 커맨드가 무슨 뜻인지(이동·발동)는 LOPWorld.Tick이 읽어서 정한다.</summary>
    public class ServerInputSystem : GameFramework.Runner.ITickSystem
    {
        // 유실 틱을 마지막 입력으로 몇 틱까지 메울지. 8틱 = 160ms — 그보다 길게 비면 순간적인
        // 패킷 유실이 아니라 연결 문제이고, 낡은 입력으로 계속 달리는 게 눈에 보이기 시작한다.
        private const int MaxPredictedTicks = 8;

        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly InputBufferSystem inputBufferSystem;

        public ServerInputSystem(GameFramework.World.EntityRegistry entityRegistry, InputBufferSystem inputBufferSystem)
        {
            this.entityRegistry = entityRegistry;
            this.inputBufferSystem = inputBufferSystem;
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
                    // 커맨드가 없다 = 유실/지각. 클라가 무입력 틱에도 0 커맨드를 보내므로 이 자리는
                    // "안 눌렀다"를 뜻하지 않는다 — 마지막으로 받은 이동을 이어 쓴다.
                    inputBufferSystem.PredictMissing(buffer, MaxPredictedTicks);
                    continue;
                }

                long gap = input.SequenceNumber - previousSequence - 1;
                if (gap > 0)
                {
                    buffer.TimingTracker.RecordSeqGap((int)gap);
                }
            }
        }
    }
}
