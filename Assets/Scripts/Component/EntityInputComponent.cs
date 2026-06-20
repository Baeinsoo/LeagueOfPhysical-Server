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

        public void AddInput(PlayerInputToS input)
        {
            if (input.PlayerInput.SequenceNumber <= lastProcessedSequence)
            {
                Debug.LogWarning($"무시된 입력: 시퀀스 {input.PlayerInput.SequenceNumber}는 이미 처리됨 (마지막 처리: {lastProcessedSequence})");
                return;
            }

            if (input.PlayerInput.SequenceNumber > expectedNextSequence)
            {
                Debug.LogWarning($"누락된 입력 시퀀스 감지: {expectedNextSequence}부터 {input.PlayerInput.SequenceNumber - 1}까지");
            }

            if (inputBuffer.ContainsKey(input.Tick) == false)
            {
                inputBuffer.Add(input.Tick, input);
                expectedNextSequence = input.PlayerInput.SequenceNumber + 1;
            }
            else
            {
                Debug.LogWarning($"동일한 틱({input.Tick})에 대한 입력이 이미 존재합니다. 새 입력 무시됨.");
            }
        }
    }
}
