using System.Collections.Generic;
using GameFramework.Rng;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 다음 위험을 고른다(서버 전용). 매치 씨앗으로 뽑아 같은 판을 다시 돌리면 같은 순서가 나온다.
    /// 무엇을 낼지는 지금 스테이지가, 얼마나 세게(간격·예고·개수)는 세기가 정한다(스펙 §4.2).
    /// 고른 패턴은 예약 시간 뒤에 시작한다 — 핑 낮은 사람이 먼저 보지 않게(스펙 §5.1).
    /// </summary>
    public class DodgeDirector
    {
        private readonly ulong seed;
        private readonly DodgeConfig config;
        private readonly DodgeStageTable stages;
        private int counter;
        private int nextId = 1;
        private long nextTick = long.MinValue;
        private long tileClearTick = long.MinValue;
        private int bombRotation;

        public DodgeDirector(ulong matchSeed, DodgeConfig config, DodgeStageTable stages)
        {
            seed = Hashing.Combine(matchSeed, Hashing.Fnv1a64("dodge-director"));
            this.config = config;
            this.stages = stages;
        }

        // 세기가 오르면 줄어드는 값들. 하한이 있다 — 세기가 끝없이 오르는 서든데스에서도 피할 수 있어야 한다.
        public static int IntervalTicksAt(in DodgeStagePoint at, in DodgeConfig c)
            => Mathf.Max(c.MinIntervalTicks, Mathf.RoundToInt(at.IntervalTicks / Mathf.Max(at.Intensity, 0.01f)));

        public static int WarnTicksAt(float intensity, in DodgeConfig c)
            => Mathf.Max(c.MinWarnTicks, Mathf.RoundToInt(c.WarnTicks / Mathf.Max(intensity, 0.01f)));

        // 세기가 오르면 느는 값들. 위가 막혀 있다 — 경기장이 탄으로 꽉 차면 "피하기"가 아니다.
        public static int RainCount(float intensity) => Mathf.Clamp(Mathf.RoundToInt(8f * intensity), 6, 40);
        public static float WallGap(float intensity) => Mathf.Max(1.8f, 3f / Mathf.Max(intensity, 0.01f));
        /// <summary>링 한 겹의 탄 수. 판정 지름 0.76m가 지나가는 반지름 = 0.38 / sin(π/N) — 10개면 1.2m, 28개면 3.4m 밖.</summary>
        public static int RingCount(float intensity) => Mathf.Clamp(Mathf.RoundToInt(12f * intensity), 8, 28);
        public static int SpiralArms(float intensity) => intensity >= 1.2f ? 4 : 3;

        /// <summary>링의 부채꼴 틈(탄 수) — 바깥 탄 벽의 "구멍 찾기"를 심판 기준으로 옮겼다.</summary>
        public const int RingGap = 3;
        // rad/틱 — 반지름 4m에서 접선 속도 3~5 m/s. 사람(4 m/s)이 따라 돌거나 갈래 사이로 비킬 수 있게.
        private const float SpiralTurnMin = 0.015f, SpiralTurnMax = 0.025f;

        public static int LaserCount(float intensity) => Mathf.Clamp(Mathf.FloorToInt(intensity), 1, 3);
        public static int RockCount(float intensity) => intensity >= 1.6f ? 2 : 1;

        private const int SecondRockDelayTicks = 20;

        /// <summary>
        /// 한 번에 노리는 사람 수 — 4명당 1명. 노리는 패턴(조준·연사·줄넘기 첫 줄·장독)은 한 사람만 노리면 인원이 늘수록
        /// 각자 노려지는 빈도가 1/N로 준다 — 이 수만큼 서로 다른 사람을 노려 1인 체감을 비슷하게 둔다.
        /// </summary>
        public static int TargetCount(int alive) => System.Math.Max(1, (alive + 3) / 4);
        /// <summary>수박 한 번에 떨어지는 최대 개수 — 사람마다 하나씩이면 8인에서 경기장이 덮인다. 대상은 돌아가며 모두에게.</summary>
        public const int MaxBombs = 3;

        /// <summary>
        /// 온돌 예고를 본 뒤 닿을 수 있는 거리(m) — 반응 0.35초 뒤 사람 걸음(4 m/s, #Character speed)으로, 15% 여유를 뺀다.
        /// 안전 칸을 무작위로만 고르면 내 근처에 하나도 없는 판이 생긴다(스테이지 5 끝 16%, 서든데스 25%).
        /// </summary>
        public static float TileReach(int warnTicks) =>
            Mathf.Max(0f, PlayerSpeed * (warnTicks / (float)DodgeConfig.TicksPerSecond - 0.35f) * 0.85f);
        private const float PlayerSpeed = 4f;

        /// <summary>온돌 안전 칸 수(36칸 중) — 세기가 오르면 준다. 18 = 예전 체크무늬와 같은 넓이.</summary>
        public static int SafeTiles(float intensity) => Mathf.Clamp(Mathf.RoundToInt(18f / Mathf.Max(intensity, 0.01f)), 6, 18);
        /// <summary>장독이 사람을 겨눌 때 들어오는 자리 흔들림(m) — 겨누되 매번 같은 길은 아니게.</summary>
        private const float RockAimJitter = 2f;
        /// <summary>온돌 예고 하한(1.2초) — 반응 후 옆 칸까지 갈 시간.</summary>
        public const int TileMinWarnTicks = 60;
        /// <summary>탄 벽의 탄 사이. 판정 지름(2×(탄+몸) = 0.76m)보다 좁아야 구멍으로만 지나간다.</summary>
        public const float WallBulletSpacing = 0.7f;   // 두 개 연달아 — 첫 것을 피한 자리를 둘째가 지난다

        public void Next(long tick, long gameplayStartTick, IReadOnlyList<Vector2> alivePositions, List<DodgePattern> into)
        {
            if (gameplayStartTick == long.MaxValue || tick < gameplayStartTick)
            {
                return;
            }
            if (nextTick == long.MinValue)
            {
                nextTick = gameplayStartTick + config.FirstPatternDelayTicks;
            }
            if (tick < nextTick)
            {
                return;
            }

            // 스테이지·세기는 화면에 뜨는 틱(고른 틱 + 예약 시간)으로 본다 — 고른 틱으로 보면 새 스테이지 배너가
            // 뜬 뒤에 이전 스테이지 종류가 나타난다.
            var at = stages.At(tick + config.LeadTicks, gameplayStartTick, config);
            nextTick = tick + IntervalTicksAt(at, config);

            var kinds = at.Kinds;
            var kind = config.OnlyKind > 0 ? (DodgePatternKind)config.OnlyKind : kinds[counter % kinds.Length];
            // 뽑는 순서가 계약이다 — 같은 씨앗에서 같은 판이 나와야 재현·디버깅이 된다.
            var rng = new DeterministicRandom(Hashing.Combine(seed, (ulong)counter));
            counter++;

            long start = tick + config.LeadTicks;
            // 탄은 전부 심판이 쏜다 — 나타나는 틱에 심판이 서 있는(또는 걸어오는) 자리에서.
            Vector2 thrower = DodgeReferee.PoseAt(start, gameplayStartTick, stages, config).Position;
            int warn = WarnTicksAt(at.Intensity, config);
            float h = config.ArenaHalf;
            switch (kind)
            {
                case DodgePatternKind.BulletRain:
                    into.Add(new DodgePattern(nextId++, kind, start, rng.NextUInt64(), rng.Range(0, 4),
                                              RainCount(at.Intensity), 4f, 0.15f));
                    break;
                case DodgePatternKind.BulletWall:
                    into.Add(new DodgePattern(nextId++, kind, start, 0, rng.Range(0, 4), rng.Range(-h + 2f, h - 2f),
                                              WallGap(at.Intensity), WallBulletSpacing));
                    break;
                case DodgePatternKind.BulletAimed:
                    // 투척기에서 노리는 사람마다 3갈래(탄막 규칙 — 발사원은 한 곳).
                    foreach (var target in Targets(ref rng, alivePositions))
                    {
                        into.Add(new DodgePattern(nextId++, kind, start, 0, thrower.x, thrower.y, target.x, target.y));
                    }
                    break;
                case DodgePatternKind.Ring:
                    into.Add(new DodgePattern(nextId++, kind, start, RingGap, thrower.x, thrower.y,
                                              RingCount(at.Intensity), rng.Range(0f, 2f * Mathf.PI)));
                    AddStream(ref rng, start, thrower, alivePositions, into);
                    break;
                case DodgePatternKind.Spiral:
                    {
                        float turn = rng.Range(SpiralTurnMin, SpiralTurnMax) * (rng.Range(0, 2) == 0 ? -1f : 1f);
                        into.Add(new DodgePattern(nextId++, kind, start, 0, thrower.x, thrower.y, SpiralArms(at.Intensity), turn));
                        AddStream(ref rng, start, thrower, alivePositions, into);
                    }
                    break;
                case DodgePatternKind.Bomb:
                    // 처음 버전 — 발밑에 하나, 예고는 세기에 따라 공통 하한(0.8초)까지(10-04 사람 판 결론).
                    // 한 번에 MaxBombs개까지, 대상은 돌아가며 — 인원이 많아도 경기장이 수박으로 덮이지 않게.
                    {
                        int count = Mathf.Min(alivePositions.Count, MaxBombs);
                        for (int k = 0; k < count; k++)
                        {
                            var p = alivePositions[(bombRotation + k) % alivePositions.Count];
                            into.Add(new DodgePattern(nextId++, kind, start, 0, p.x, p.y, config.BombRadius, 0f, warn));
                        }
                        bombRotation += count;
                    }
                    break;
                case DodgePatternKind.Laser:
                    {
                    // 앞 줄들은 노리는 사람마다 하나씩 그 자리를 지난다 — "나를 노리는 게 없다"(사람 판 소감).
                    var aimed = Targets(ref rng, alivePositions);
                    int lines = Mathf.Max(LaserCount(at.Intensity), aimed.Count);
                    for (int i = 0; i < lines; i++)
                    {
                        float pos = rng.Range(-h + 1f, h - 1f);
                        bool vertical = rng.Range(0, 2) == 0;
                        if (i < aimed.Count) pos = vertical ? aimed[i].x : aimed[i].y;
                        into.Add(vertical
                            ? new DodgePattern(nextId++, kind, start, 0, pos, -h, pos, h, warn)
                            : new DodgePattern(nextId++, kind, start, 0, -h, pos, h, pos, warn));
                    }
                    }
                    break;
                case DodgePatternKind.Rock:
                    {
                        // 사람 쪽으로 굴린다. 둘째는 맞은편 벽에서 시간차로 — 교차해 한 방향 도망을 막는다.
                        int firstSide = rng.Range(0, 4);
                        var aimed = Targets(ref rng, alivePositions);
                        int rocks = Mathf.Max(RockCount(at.Intensity), aimed.Count);
                        for (int i = 0; i < rocks; i++)
                        {
                            int side = i % 2 == 0 ? (firstSide + i / 2) % 4 : (firstSide + 2 + i / 2) % 4;
                            float along = rng.Range(-h + 2f, h - 2f), twist = rng.Range(-0.35f, 0.35f);
                            if (alivePositions.Count > 0)
                            {
                                var target = aimed[i % aimed.Count];
                                along = Mathf.Clamp((side % 2 == 0 ? target.x : target.y) + rng.Range(-RockAimJitter, RockAimJitter),
                                                    -h + 1.5f, h - 1.5f);
                                Vector2 entry = DodgeHazards.EdgePoint(side, along, config.EdgeDistance);
                                twist = Mathf.Clamp(Vector2.SignedAngle(DodgeHazards.Inward(side), target - entry) * Mathf.Deg2Rad, -0.6f, 0.6f);
                            }
                            into.Add(new DodgePattern(nextId++, kind, start + i * SecondRockDelayTicks, 0, side, along, twist, 0f, warn));
                        }
                    }
                    break;
                case DodgePatternKind.Tiles:
                    {
                        // 한 번에 한 판 — 경고가 겹치면 바닥 전체가 경고색이 돼 못 읽는다(WildStar 회고, 사람 판).
                        int tileWarn = Mathf.Max(warn, TileMinWarnTicks);
                        if (start <= tileClearTick)
                        {
                            nextTick = System.Math.Max(nextTick, tileClearTick - config.LeadTicks + 1);
                            break;
                        }
                        // 안전 칸 N개만 남긴다(Hexagon Heat·Perfect Match) — 무작위로 섞어 앞 N칸.
                        int n = Mathf.Max(1, config.TileCount), cells = Mathf.Min(n * n, 64);
                        var order = new int[cells];
                        for (int i = 0; i < cells; i++) order[i] = i;
                        for (int i = cells - 1; i > 0; i--)
                        {
                            int j = rng.Range(0, i + 1);
                            (order[i], order[j]) = (order[j], order[i]);
                        }
                        // 산 사람마다 닿는 거리 안(지금 선 칸 말고)에 안전 칸을 하나 보장하고, 나머지는 무작위로 채운다.
                        var safeSet = new HashSet<int>();
                        float size = config.ArenaHalf * 2f / n, reach = TileReach(tileWarn) * 0.9f;
                        foreach (var p in alivePositions)
                        {
                            int own = Mathf.Clamp((int)((p.x + config.ArenaHalf) / size), 0, n - 1)
                                    + Mathf.Clamp((int)((p.y + config.ArenaHalf) / size), 0, n - 1) * n;
                            var near = new List<int>();
                            bool covered = false;
                            for (int i = 0; i < cells; i++)
                            {
                                if (i == own) continue;
                                float x0 = -config.ArenaHalf + (i % n) * size, z0 = -config.ArenaHalf + (i / n) * size;
                                float dx = Mathf.Max(x0 - p.x, 0f, p.x - (x0 + size)), dz = Mathf.Max(z0 - p.y, 0f, p.y - (z0 + size));
                                if (Mathf.Sqrt(dx * dx + dz * dz) > reach) continue;
                                near.Add(i);
                                covered |= safeSet.Contains(i);
                            }
                            if (!covered && near.Count > 0) safeSet.Add(near[rng.Range(0, near.Count)]);
                        }
                        int safe = Mathf.Min(SafeTiles(at.Intensity), cells);
                        for (int i = 0; i < cells && safeSet.Count < safe; i++) safeSet.Add(order[i]);
                        ulong mask = 0;
                        for (int i = 0; i < cells; i++) if (!safeSet.Contains(i)) mask |= 1UL << i;
                        var tiles = new DodgePattern(nextId++, kind, start, mask, 0f, 0f, 0f, 0f, tileWarn);
                        into.Add(tiles);
                        tileClearTick = start + DodgeHazards.LifetimeTicks(tiles, config);
                    }
                    break;
            }
        }

        // 고정 탄막 위에 겹치는 조준 연사(스트리밍) — 고정은 장애물, 조준은 압박(Boghog·Sparen).
        private void AddStream(ref DeterministicRandom rng, long start, Vector2 thrower, IReadOnlyList<Vector2> alivePositions,
                               List<DodgePattern> into)
        {
            foreach (var target in Targets(ref rng, alivePositions))
            {
                into.Add(new DodgePattern(nextId++, DodgePatternKind.BulletStream, start, 0, thrower.x, thrower.y, target.x, target.y));
            }
        }

        // 서로 다른 TargetCount명 — 섞어서 앞에서부터. 산 사람이 없으면 빈 목록.
        private static List<Vector2> Targets(ref DeterministicRandom rng, IReadOnlyList<Vector2> alivePositions)
        {
            var picked = new List<Vector2>();
            int n = alivePositions.Count;
            if (n == 0) return picked;
            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            int k = Mathf.Min(TargetCount(n), n);
            for (int i = 0; i < k; i++) picked.Add(alivePositions[order[i]]);
            return picked;
        }
    }
}
