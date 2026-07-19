using UnityEngine;

namespace LOP
{
    /// <summary>매치당 1회 생성되는 결정론 RNG 씨앗(서버 권위). 재현용으로 생성 시 로그.</summary>
    public class MatchSeed : IMatchSeed
    {
        public ulong Value { get; }

        public MatchSeed()
        {
            var bytes = System.Guid.NewGuid().ToByteArray();
            Value = System.BitConverter.ToUInt64(bytes, 0);
            Debug.Log($"[MatchSeed] {Value}");
        }
    }
}
