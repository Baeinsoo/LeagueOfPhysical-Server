using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 0.25m 칸 격자(슬라이스 4a 스펙 §4). 칸 중심이 이동 범위(±limit) 안인 칸만 있다. 한 줄 = 비트 두 개(ulong 2개, 칸 ≤ 128).
    /// </summary>
    public sealed class DodgeReachGrid
    {
        public const float Cell = 0.25f;
        public readonly int N;
        private readonly float origin;
        private readonly ulong[] lo, hi, tmpLo, tmpHi, srcLo, srcHi;
        private readonly ulong maskHi;

        public DodgeReachGrid(float limit)
        {
            N = Mathf.FloorToInt(2f * limit / Cell);
            origin = -N * Cell * 0.5f;
            lo = new ulong[N]; hi = new ulong[N]; tmpLo = new ulong[N]; tmpHi = new ulong[N];
            srcLo = new ulong[N]; srcHi = new ulong[N];
            maskHi = N > 64 ? (N - 64 >= 64 ? ulong.MaxValue : (1UL << (N - 64)) - 1) : 0;
        }

        public Vector2 Center(int x, int z) => new Vector2(origin + (x + 0.5f) * Cell, origin + (z + 0.5f) * Cell);
        public (int x, int z) CellOf(Vector2 p) =>
            (Mathf.Clamp(Mathf.FloorToInt((p.x - origin) / Cell), 0, N - 1), Mathf.Clamp(Mathf.FloorToInt((p.y - origin) / Cell), 0, N - 1));

        public bool Get((int x, int z) c) => Get(c.x, c.z);
        public bool Get(int x, int z) => x < 64 ? (lo[z] >> x & 1) != 0 : (hi[z] >> (x - 64) & 1) != 0;
        public void Clear(int x, int z) { if (x < 64) lo[z] &= ~(1UL << x); else hi[z] &= ~(1UL << (x - 64)); }

        public void FillAll()
        {
            ulong maskLo = N >= 64 ? ulong.MaxValue : (1UL << N) - 1;
            for (int z = 0; z < N; z++) { lo[z] = maskLo; hi[z] = maskHi; }
        }

        public void Only((int x, int z) c)
        {
            System.Array.Clear(lo, 0, N); System.Array.Clear(hi, 0, N);
            if (c.x < 64) lo[c.z] = 1UL << c.x; else hi[c.z] = 1UL << (c.x - 64);
        }

        public int Count
        {
            get
            {
                int n = 0;
                for (int z = 0; z < N; z++) n += Pop(lo[z]) + Pop(hi[z]);
                return n;
            }
        }

        private static int Pop(ulong v) { int n = 0; while (v != 0) { v &= v - 1; n++; } return n; }

        /// <summary>한 칸 넓힌다. 대각선을 번갈아 켜면 팔각형으로 퍼져 원에 가깝다.</summary>
        public void Dilate(bool diagonal)
        {
            for (int z = 0; z < N; z++)
            {
                ulong l = lo[z], h = hi[z];
                ulong sl = (l << 1), sh = (h << 1) | (l >> 63);      // x+1
                ulong rl = (l >> 1) | (h << 63), rh = h >> 1;        // x-1
                tmpLo[z] = l | sl | rl;
                tmpHi[z] = (h | sh | rh) & maskHi;
            }
            // 위아래 줄은 넓히기 전 값(대각선이면 가로로 넓힌 값)에서 가져온다 — 덮어쓴 줄을 다시 읽으면 아래로 번진다.
            var upLo = diagonal ? tmpLo : srcLo; var upHi = diagonal ? tmpHi : srcHi;
            System.Array.Copy(lo, srcLo, N); System.Array.Copy(hi, srcHi, N);
            for (int z = 0; z < N; z++)
            {
                ulong outLo = tmpLo[z], outHi = tmpHi[z];
                if (z > 0) { outLo |= upLo[z - 1]; outHi |= upHi[z - 1]; }
                if (z < N - 1) { outLo |= upLo[z + 1]; outHi |= upHi[z + 1]; }
                lo[z] = outLo; hi[z] = outHi & maskHi;
            }
            if (N < 64)
            {
                ulong maskLo = (1UL << N) - 1;
                for (int z = 0; z < N; z++) lo[z] &= maskLo;
            }
        }

        /// <summary>안전 칸 덩어리 중 가장 큰 것의 가운데에 가장 가까운 칸 중심 — 조준탄이 노릴 자리(가장 피하기 쉬운 자리 = 보수적).</summary>
        public Vector2 LargestRegionCenter()
        {
            var seen = new bool[N * N];
            var queue = new Queue<int>();
            int bestSize = 0; Vector2 bestSum = Vector2.zero;
            var members = new List<int>(); var bestMembers = new List<int>();
            for (int start = 0; start < N * N; start++)
            {
                if (seen[start] || !Get(start % N, start / N)) continue;
                members.Clear(); Vector2 sum = Vector2.zero;
                seen[start] = true; queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int k = queue.Dequeue(); members.Add(k);
                    int x = k % N, z = k / N; sum += Center(x, z);
                    if (x > 0) Visit(k - 1); if (x < N - 1) Visit(k + 1);
                    if (z > 0) Visit(k - N); if (z < N - 1) Visit(k + N);
                }
                if (members.Count > bestSize) { bestSize = members.Count; bestSum = sum; bestMembers.Clear(); bestMembers.AddRange(members); }
            }
            if (bestSize == 0) return Vector2.zero;
            Vector2 mean = bestSum / bestSize, best = Center(bestMembers[0] % N, bestMembers[0] / N);
            foreach (int k in bestMembers)
            {
                var c = Center(k % N, k / N);
                if ((c - mean).sqrMagnitude < (best - mean).sqrMagnitude) best = c;
            }
            return best;

            void Visit(int k) { if (!seen[k] && Get(k % N, k / N)) { seen[k] = true; queue.Enqueue(k); } }
        }
    }

    /// <summary>
    /// 완벽한 사람이면 피할 수 있나(슬라이스 4a 스펙 §4). R(t+1) = 넓히기(R(t)) − 틱 t+1에 켜진 도형이 덮는 칸.
    /// 넓히기는 누적 이동 거리가 한 칸이 될 때마다 한 번(평균 4 m/s). 패턴은 진짜 진행기 시스템이 고른다.
    /// </summary>
    public static class DodgeFeasibility
    {
        public struct Result
        {
            public long DeadTick;
            public int[] MinCellsByStage;   // [0..Count-1] 스테이지, [Count] 서든데스
            public long SuddenDeathStartTick;
        }

        private const int GhostEveryTicks = 10;

        public static long StagesEndTick(DodgeStageTable stages, in DodgeConfig c)
        {
            long t = 0;
            while (!stages.At(t, 0, c).SuddenDeath) t += DodgeConfig.TicksPerSecond;
            while (stages.At(t - 1, 0, c).SuddenDeath) t--;
            return t;
        }

        public static Result Run(ulong seed, DodgeConfig config, DodgeStageTable stages, long maxTicks)
        {
            float limit = config.ArenaHalf - DodgeSimMatch.BodyRadius;
            var grid = new DodgeReachGrid(limit);
            var ghost = new GhostMover();
            var match = new DodgeSimMatch(seed, config, stages, 1);
            grid.Only(grid.CellOf(match.Positions[0]));
            ghost.At = match.Positions[0];

            var result = new Result { DeadTick = -1, MinCellsByStage = new int[stages.Count + 1], SuddenDeathStartTick = -1 };
            for (int i = 0; i < result.MinCellsByStage.Length; i++) result.MinCellsByStage[i] = int.MaxValue;

            var shapes = new List<DodgeShape>();
            float budget = 0f; bool diagonal = false;
            float perTick = DodgeSimMatch.MoveSpeed / DodgeConfig.TicksPerSecond;
            while (match.Tick < maxTicks)
            {
                match.StepPatternsOnly(ghost);   // 진행기만 돈다 — 판정·목숨은 검사기가 대신한다
                long t = match.Tick;
                budget += perTick;
                while (budget >= DodgeReachGrid.Cell) { grid.Dilate(diagonal); diagonal = !diagonal; budget -= DodgeReachGrid.Cell; }

                shapes.Clear();
                foreach (var p in match.State.Patterns) DodgeHazards.Shapes(p, t, config, shapes);
                Carve(grid, shapes, config);

                var at = match.StageAt(t);
                if (at.Started)
                {
                    int slot = at.SuddenDeath ? stages.Count : at.Index;
                    if (at.SuddenDeath && result.SuddenDeathStartTick < 0) result.SuddenDeathStartTick = t;
                    result.MinCellsByStage[slot] = Mathf.Min(result.MinCellsByStage[slot], grid.Count);
                }
                if (grid.Count == 0) { result.DeadTick = t; break; }
                if (t % GhostEveryTicks == 0) ghost.At = grid.LargestRegionCenter();
            }
            for (int i = 0; i < result.MinCellsByStage.Length; i++)
                if (result.MinCellsByStage[i] == int.MaxValue) result.MinCellsByStage[i] = -1;   // 그 스테이지까지 안 갔다
            return result;
        }

        /// <summary>켜진 도형이 덮는 칸을 지운다. 도형 둘레 상자 안 칸만 서버와 같은 식(ShapeHits)으로 잰다.</summary>
        public static void Carve(DodgeReachGrid g, List<DodgeShape> shapes, in DodgeConfig c)
        {
            foreach (var s in shapes)
            {
                if (!s.Active) continue;
                float pad = s.Type == DodgeShapeType.Rect ? 0f : s.Radius + c.HitRadius;
                var a = g.CellOf(new Vector2(Mathf.Min(s.X0, s.X1) - pad, Mathf.Min(s.Z0, s.Z1) - pad));
                var b = g.CellOf(new Vector2(Mathf.Max(s.X0, s.X1) + pad, Mathf.Max(s.Z0, s.Z1) + pad));
                for (int z = a.z; z <= b.z; z++)
                for (int x = a.x; x <= b.x; x++)
                {
                    if (!g.Get(x, z)) continue;
                    var p = g.Center(x, z);
                    if (DodgeHazards.ShapeHits(s, p, p, c)) g.Clear(x, z);
                }
            }
        }

        private sealed class GhostMover : IDodgeSimMover
        {
            public Vector2 At;
            public Vector2 Want(DodgeSimMatch m, int player) => At;
        }
    }
}
