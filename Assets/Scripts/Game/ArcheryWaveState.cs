namespace LOP
{
    /// <summary>
    /// 지금 웨이브에서 어느 과녁이 먹혔나(서버 권위). <b>판정하는 쪽과 내보내는 쪽이 함께 보는 값</b>이라
    /// 둘 중 어느 시스템에도 넣지 않고 따로 둔다 — 그래야 판정을 세션 없이 테스트할 수 있다.
    ///
    /// <para>과녁이 최대 세 개라 "먹힌 슬롯"은 비트마스크 하나면 된다.</para>
    /// </summary>
    public class ArcheryWaveState
    {
        /// <summary>이 마스크가 말하는 웨이브. 아직 시작 전이면 −1이다.</summary>
        public int WaveIndex { get; private set; } = -1;

        /// <summary>먹힌 슬롯의 비트마스크. 슬롯 0이 1비트.</summary>
        public int ConsumedMask { get; private set; }

        /// <summary>웨이브가 넘어가면 과녁도 통째로 새것이다 — 기록을 비운다.</summary>
        public void BeginWave(int waveIndex)
        {
            WaveIndex = waveIndex;
            ConsumedMask = 0;
        }

        public bool IsConsumed(int slot) => (ConsumedMask & (1 << slot)) != 0;

        /// <summary>먹었다고 기록한다. 이미 먹힌 슬롯이면 거짓을 돌려준다.</summary>
        public bool TryConsume(int slot)
        {
            if (IsConsumed(slot))
            {
                return false;
            }
            ConsumedMask |= 1 << slot;
            return true;
        }
    }
}
