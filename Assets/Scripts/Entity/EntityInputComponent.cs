using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LOP
{
    public class EntityInputComponent : MonoBehaviour
    {
        private static int INPUT_DELAY_TICKS = 0;

        private SortedDictionary<long, PlayerInput> inputBuffer = new SortedDictionary<long, PlayerInput>();

        private long lastProcessedSequence = -1;
        public long expectedNextSequence { get; private set; }

        public PlayerInput GetInput(long tick)
        {
            if (inputBuffer.Count == 0)
            {
                return null;
            }

            // 지연을 적용한 처리 틱 계산
            long targetTick = tick - INPUT_DELAY_TICKS;

            var availableInputs = inputBuffer.Where(kvp => kvp.Key <= targetTick).ToList();
            if (availableInputs.Count == 0)
            {
                return null;
            }

            PlayerInput input = availableInputs.First().Value;
            inputBuffer.Remove(input.tick);

            lastProcessedSequence = input.sequenceNumber;

            return input;
        }

        public void AddInput(PlayerInput input)
        {
            if (input.sequenceNumber <= lastProcessedSequence)
            {
                Debug.LogWarning($"무시된 입력: 시퀀스 {input.sequenceNumber}는 이미 처리됨 (마지막 처리: {lastProcessedSequence})");
                return;
            }

            if (input.sequenceNumber > expectedNextSequence)
            {
                Debug.LogWarning($"누락된 입력 시퀀스 감지: {expectedNextSequence}부터 {input.sequenceNumber - 1}까지");
            }

            if (inputBuffer.ContainsKey(input.tick) == false)
            {
                inputBuffer.Add(input.tick, input);
                expectedNextSequence = input.sequenceNumber + 1;
            }
            else
            {
                Debug.LogWarning($"동일한 틱({input.tick})에 대한 입력이 이미 존재합니다. 새 입력 무시됨.");
            }
        }
    }
}
