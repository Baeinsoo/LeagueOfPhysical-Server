using System.Collections.Generic;
using GameFramework.Rng;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 보통 사람 흉내(슬라이스 4a 스펙 §5). 화면에 나타난 뒤 반응 지연이 지난 패턴만 안다.
    /// 0.1초마다 둘레 후보 중 앞으로 2초 동안 안 맞는 가장 가까운 곳(다 맞으면 가장 늦게 맞는 곳)을 골라, 조금 빗나가게 간다.
    /// 출발값(반응 0.25초·빗나감 0.3m)은 사람 기록으로 보정한다(§7).
    /// </summary>
    public sealed class DodgeHumanBot : IDodgeSimMover
    {
        public const int DecisionTicks = 5;
        public const int HorizonTicks = 150;
        public const int SampleTicks = 4;
        public const float SearchRadius = 3f;
        public const float SearchStep = 1f;
        /// <summary>먼 후보 — 경기장 전체를 이 간격으로. 탄 벽의 틈은 어디든 생기니 가까운 곳만 보면 늦는다.</summary>
        public const float FarStep = 2f;
        /// <summary>안전한 후보끼리는 가까운 곳 + 가운데 쪽을 고른다(구석에 몰리면 다음 것을 못 피한다).</summary>
        public const float CenterBias = 0.5f;
        public const int SafeBucketTicks = 25;
        /// <summary>계획 여유에 더하는 몫 — 4틱마다 재는 사이에 빠지는 거리.</summary>
        public const float SampleSlack = 0.15f;

        private readonly ulong seed;
        private readonly int reactionTicks;
        private readonly float aimNoise;
        private readonly Dictionary<int, Vector2> target = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, Vector2> chosen = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, Vector2> offset = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, int> decisions = new Dictionary<int, int>();
        private readonly List<DodgePattern> known = new List<DodgePattern>();
        private readonly Dictionary<long, List<DodgeShape>> shapeCache = new Dictionary<long, List<DodgeShape>>();
        private readonly List<(int id, float count)> knownKey = new List<(int, float)>();
        private readonly List<(int id, float count)> scratchKey = new List<(int, float)>();
        private DodgeConfig plan;
        private bool planReady;

        public DodgeHumanBot(ulong seed, float reactionSeconds = 0.25f, float aimNoise = 0.3f)
        {
            this.seed = seed;
            reactionTicks = Mathf.RoundToInt(reactionSeconds * DodgeConfig.TicksPerSecond);
            this.aimNoise = aimNoise;
        }

        public Vector2 Want(DodgeSimMatch m, int player)
        {
            if (!planReady)
            {
                plan = WithMargin(m.Config, aimNoise + SampleSlack);
                planReady = true;
            }
            long now = m.Tick;
            if (!target.TryGetValue(player, out var goal)) goal = m.Positions[player];
            if (now % DecisionTicks == 0)
            {
                Refresh(m, now);
                goal = Decide(m, player, now);
                target[player] = goal;
            }
            return goal;
        }

        // 아는 패턴 = 시작 틱(화면에 나타남) + 반응 지연이 지난 것.
        private void Refresh(DodgeSimMatch m, long now)
        {
            // 본 틱(지금 − 반응 지연)에 화면에 있던 것만 안다. 탄비는 그때까지 나온 탄만(검토 I1).
            long seen = now - reactionTicks;
            known.Clear();
            scratchKey.Clear();
            foreach (var p in m.State.Patterns)
            {
                if (seen < p.StartTick) continue;
                var v = Visible(p, seen);
                known.Add(v);
                scratchKey.Add((v.Id, v.P1));
            }
            // 아는 것이 바뀌면 미래 도형 캐시를 버린다 — 해시가 아니라 목록 그대로 비교(충돌 없음).
            bool same = scratchKey.Count == knownKey.Count;
            for (int i = 0; same && i < scratchKey.Count; i++) same = scratchKey[i] == knownKey[i];
            if (!same)
            {
                shapeCache.Clear();
                knownKey.Clear();
                knownKey.AddRange(scratchKey);
            }
        }

        /// <summary>
        /// seenTick에 사람이 알 수 있는 만큼의 패턴. 탄비는 탄이 spacing틱마다 하나씩 나오므로 그때까지 나온 개수로 줄인 사본 —
        /// 나머지(탄 벽·조준·폭탄·레이저·장독·바닥)는 예고가 뜨는 순간 전부 보인다.
        /// </summary>
        public static DodgePattern Visible(in DodgePattern p, long seenTick)
        {
            if (p.Kind != DodgePatternKind.BulletRain) return p;
            long spacing = (long)Mathf.Max(0f, p.P2);
            long age = seenTick - p.StartTick;
            float count = spacing <= 0 ? p.P1 : Mathf.Min(p.P1, age / spacing + 1);
            return new DodgePattern(p.Id, p.Kind, p.StartTick, p.Seed, p.P0, count, p.P2, p.P3, p.WarnTicks);
        }

        private List<DodgeShape> ShapesAt(DodgeSimMatch m, long t)
        {
            if (!shapeCache.TryGetValue(t, out var list))
            {
                list = new List<DodgeShape>();
                foreach (var p in known) DodgeHazards.Shapes(p, t, m.Config, list);
                shapeCache[t] = list;
            }
            return list;
        }

        private Vector2 Decide(DodgeSimMatch m, int player, long now)
        {
            Vector2 here = m.Positions[player];
            if (known.Count == 0) return here;

            // 얼마나 오래 안전한가를 0.5초 단위로 묶어 먼저 보고, 같은 묶음 안에서는 가깝고 가운데인 곳.
            // "끝까지 안전"만 고집하면 탄이 빽빽할 때 아무 데도 없어 가장 먼 구석으로 도망가 갇힌다.
            Vector2 best = here; float bestScore = float.MaxValue; long bestBucket = -1;
            void Consider(Vector2 cand)
            {
                cand = new Vector2(Mathf.Clamp(cand.x, -m.Limit, m.Limit), Mathf.Clamp(cand.y, -m.Limit, m.Limit));
                long hit = FirstHit(m, here, cand, now);
                long bucket = (hit == long.MaxValue ? HorizonTicks : hit - now) / SafeBucketTicks;
                float score = (cand - here).magnitude + CenterBias * cand.magnitude;
                if (bucket > bestBucket || (bucket == bestBucket && score < bestScore))
                {
                    best = cand; bestScore = score; bestBucket = bucket;
                }
            }
            int steps = Mathf.RoundToInt(SearchRadius / SearchStep);
            for (int dx = -steps; dx <= steps; dx++)
            for (int dz = -steps; dz <= steps; dz++)
            {
                var off = new Vector2(dx, dz) * SearchStep;
                if (off.magnitude <= SearchRadius + 1e-4f) Consider(here + off);
            }
            for (float x = -m.Limit; x <= m.Limit + 1e-4f; x += FarStep)
            for (float z = -m.Limit; z <= m.Limit + 1e-4f; z += FarStep)
            {
                Consider(new Vector2(x, z));
            }
            // 빗나감은 노리는 자리가 바뀔 때만 새로 뽑는다(1m 넘게) — 매 결정마다 뽑으면 제자리에서 떨기만 한다.
            if (!chosen.TryGetValue(player, out var last) || (best - last).magnitude > 1f || !offset.ContainsKey(player))
            {
                offset[player] = Noise(player);
            }
            chosen[player] = best;
            return best + offset[player];
        }

        // 지금 자리에서 cand로 곧장 가서 머물 때 처음 맞는 틱(안 맞으면 MaxValue). SampleTicks마다 그 사이 이동을 스윕으로 잰다.
        private long FirstHit(DodgeSimMatch m, Vector2 from, Vector2 cand, long now)
        {
            float perTick = DodgeSimMatch.MoveSpeed / DodgeConfig.TicksPerSecond;
            Vector2 prev = from;
            for (int k = SampleTicks; k <= HorizonTicks; k += SampleTicks)
            {
                Vector2 pos = Vector2.MoveTowards(from, cand, perTick * k);
                foreach (var s in ShapesAt(m, now + k))
                {
                    if (DodgeHazards.ShapeHits(s, prev, pos, plan)) return now + k;
                }
                prev = pos;
            }
            return long.MaxValue;
        }

        // 사람은 자기가 빗나가는 만큼 여유를 두고 피한다 — 판정 반지름을 빗나감만큼 키운 값으로 계획한다(실제 판정은 서버 값 그대로).
        private static DodgeConfig WithMargin(in DodgeConfig c, float margin) =>
            new DodgeConfig(c.Lives, c.InvulnerableSeconds, c.HitRadius + margin, c.LeadSeconds, c.ArenaHalf,
                            c.TileCount, c.FirstPatternDelaySeconds, c.PatternIntervalSeconds, c.OnlyKind,
                            c.WarnSeconds, c.BulletSpeed, c.BulletRadius, c.BombRadius, c.BombActiveSeconds,
                            c.LaserWidth, c.LaserOnSeconds, c.RockSpeed, c.RockRadius, c.TileOnSeconds,
                            c.MinIntervalSeconds, c.MinWarnSeconds, c.SuddenDeathBase, c.SuddenDeathGrowth);

        private Vector2 Noise(int player)
        {
            if (aimNoise <= 0f) return Vector2.zero;
            decisions.TryGetValue(player, out int n);
            decisions[player] = n + 1;
            var rng = new DeterministicRandom(Hashing.Combine(Hashing.Combine(seed, (ulong)player), (ulong)n));
            // 원판 안에서 고르게 — "빗나감 0.3m" = 많아야 0.3m. 계획 여유(판정 반지름 + 빗나감)가 이것을 덮는다.
            float r = aimNoise * Mathf.Sqrt(rng.Range(0f, 1f)), a = rng.Range(0f, 2f * Mathf.PI);
            return new Vector2(r * Mathf.Cos(a), r * Mathf.Sin(a));
        }
    }
}
