using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LOP
{
    public class EntityInputComponent : LOPComponent
    {
        // jitter buffer: 입력이 처리 시점(serverTick)보다 2틱 일찍 도착하도록 여유. (Phase 2 lead + 이 버퍼로 적시 도착)
        private static int INPUT_DELAY_TICKS = 2;

        private SortedDictionary<long, PlayerInputToS> inputBuffer = new SortedDictionary<long, PlayerInputToS>();

        private long lastProcessedSequence = -1;
        public long expectedNextSequence { get; private set; }

        public PlayerInputToS GetInput(long tick)
        {
            // command-frame 정렬: 입력의 클라 tick == 서버 처리 tick(= serverTick − jitter buffer)
            long targetTick = tick - INPUT_DELAY_TICKS;

            // 처리 시점이 지난 입력(지각/처리불가)은 버린다 — 버퍼에 남아 나중에 잘못 처리되는 것 방지.
            var staleTicks = inputBuffer.Keys.Where(k => k < targetTick).ToList();
            foreach (var staleTick in staleTicks)
            {
                inputBuffer.Remove(staleTick);
            }

            // 정확히 targetTick의 입력만 처리. 없으면 miss → null(호출부가 no-input으로 진행, 클라 recon이 보정).
            if (inputBuffer.TryGetValue(targetTick, out var input))
            {
                inputBuffer.Remove(targetTick);
                lastProcessedSequence = input.PlayerInput.SequenceNumber;
                return input;
            }

            return null;
        }

        public void AddInput(long tick, global::PlayerInput playerInput)
        {
            // redundancy로 같은 입력이 여러 번 와도 정상 — 이미 처리됐거나 버퍼에 있으면 조용히 무시(dedup).
            if (playerInput.SequenceNumber <= lastProcessedSequence)
            {
                return;
            }

            if (inputBuffer.ContainsKey(tick) == false)
            {
                inputBuffer.Add(tick, new PlayerInputToS
                {
                    Tick = tick,
                    PlayerInput = playerInput,
                });
                expectedNextSequence = playerInput.SequenceNumber + 1;
            }
        }
    }
}
