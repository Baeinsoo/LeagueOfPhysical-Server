using GameFramework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace LOP
{
    public class EntityInputComponent : MonoBehaviour
    {
        private SortedDictionary<long, PlayerInput> inputBuffer = new SortedDictionary<long, PlayerInput>();

        private long inputDelayTicks = 5;
        private long lastProcessedSequence = -1;
        private long expectedNextSequence = 0;

        private async void Awake()
        {
            await UniTask.WaitUntil(() => GameEngine.current != null);

            inputDelayTicks = (long)(0.1 / GameEngine.Time.tickInterval);
        }

        public PlayerInput GetNextInput(long tick)
        {
            long targetTick = tick - inputDelayTicks;

            foreach (var key in inputBuffer.Keys.Where(k => k < targetTick).ToList())
            {
                Debug.Log($"늦게 도착한 입력 스킵 됨: 틱 {key}, 시퀀스 {inputBuffer[key].sequenceNumber}");
                inputBuffer.Remove(key);
            }

            if (inputBuffer.TryGetValue(targetTick, out PlayerInput input))
            {
                inputBuffer.Remove(targetTick);

                lastProcessedSequence = input.sequenceNumber;

                return input;
            }

            return null;
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
