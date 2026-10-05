using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 예고를 본 뒤 닿을 수 있나(공정성). 검사기(<see cref="DodgeFeasibility"/>)는 다음 패턴을 미리 아는 사람이라 무작위 안전지대의
    /// 불공정을 못 잡는다(온돌 16~25% 못 닿음 — feasibility-is-not-fairness). 반응 0.35초 뒤 4 m/s로 갈 수 있는 원 안에
    /// 그 패턴이 켜져 있는 동안 내내 안전한 자리가 있으면 닿는다. 패턴 하나만 본다(다른 패턴과 겹침은 검사기 몫).
    /// </summary>
    public static class DodgeFairness
    {
        public const float ReactionSeconds = 0.35f;
        public const float Speed = 4f;
        private const float Step = 0.25f;
        private const int SampleTicks = 2;

        public static float Reach(int warnTicks) => Mathf.Max(0f, Speed * (warnTicks / (float)DodgeConfig.TicksPerSecond - ReactionSeconds));

        /// <param name="limit">설 수 있는 범위(벽 안쪽 − 몸 반지름).</param>
        public static bool Reachable(in DodgePattern p, Vector2 from, in DodgeConfig c, float limit)
        {
            int warn = DodgeHazards.Warn(p, c);
            float r = Reach(warn);
            long life = DodgeHazards.LifetimeTicks(p, c);
            var frames = new List<List<DodgeShape>>();
            for (long t = warn; t <= life; t += SampleTicks)
            {
                var shapes = new List<DodgeShape>();
                DodgeHazards.Shapes(p, p.StartTick + t, c, shapes);
                frames.Add(shapes);
            }
            int steps = Mathf.CeilToInt(r / Step);
            for (int ix = -steps; ix <= steps; ix++)
            for (int iz = -steps; iz <= steps; iz++)
            {
                var off = new Vector2(ix, iz) * Step;
                if (off.magnitude > r + 1e-4f) continue;
                var cand = from + off;
                if (Mathf.Abs(cand.x) > limit || Mathf.Abs(cand.y) > limit) continue;
                if (SafeThroughout(frames, cand, c)) return true;
            }
            return false;
        }

        private static bool SafeThroughout(List<List<DodgeShape>> frames, Vector2 at, in DodgeConfig c)
        {
            foreach (var shapes in frames)
            foreach (var s in shapes)
            {
                if (DodgeHazards.ShapeHits(s, at, at, c)) return false;
            }
            return true;
        }

        /// <summary>예고가 있는 종류만 — 탄은 예고 없이 날아오는 걸 보고 피한다.</summary>
        public static bool Warned(DodgePatternKind k) =>
            k == DodgePatternKind.Bomb || k == DodgePatternKind.Laser || k == DodgePatternKind.Rock || k == DodgePatternKind.Tiles;
    }
}
