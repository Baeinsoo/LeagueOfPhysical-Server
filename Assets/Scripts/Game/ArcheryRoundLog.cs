using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 한 발 승부에서 라운드마다 누가 어디를 맞혔나(서버 권위). 판정(<see cref="ArcheryHitSystem"/>)이
    /// 쓰고 라운드 마감(<see cref="ArcheryRoundSystem"/>)이 읽는다 — 둘 다 들여다보는 값이라 따로 둔다
    /// (<see cref="ArcheryWaveState"/>와 같은 이유).
    /// </summary>
    public class ArcheryRoundLog
    {
        private readonly Dictionary<(int round, string shooterId), (Vector2 face, float distance)> impacts
            = new Dictionary<(int, string), (Vector2, float)>();

        public void Record(int round, string shooterId, Vector2 faceOffsetMeters, float distanceMeters)
        {
            //  라운드당 한 발이다. 혹시 둘째가 와도 첫 발을 지킨다.
            var key = (round, shooterId);
            if (impacts.ContainsKey(key) == false)
            {
                impacts[key] = (faceOffsetMeters, distanceMeters);
            }
        }

        public bool TryGet(int round, string shooterId, out Vector2 face, out float distance)
        {
            if (impacts.TryGetValue((round, shooterId), out var v))
            {
                face = v.face;
                distance = v.distance;
                return true;
            }
            face = default;
            distance = 0f;
            return false;
        }

        public void Forget(int round)
        {
            var stale = new List<(int, string)>();
            foreach (var key in impacts.Keys)
            {
                if (key.round == round) { stale.Add(key); }
            }
            foreach (var key in stale) { impacts.Remove(key); }
        }
    }
}
