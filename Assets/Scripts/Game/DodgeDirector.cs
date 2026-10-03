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

        /// <summary>탄막 투척기(경기장 가운데의 심판). 맵에도 같은 자리에 충돌체가 있다.</summary>
        public static readonly Vector2 Thrower = DodgeHazards.Thrower;
        // rad/틱 — 반지름 4m에서 접선 속도 3~5 m/s. 사람(4 m/s)이 따라 돌거나 갈래 사이로 비킬 수 있게.
        private const float SpiralTurnMin = 0.015f, SpiralTurnMax = 0.025f;

        public static int LaserCount(float intensity) => Mathf.Clamp(Mathf.FloorToInt(intensity), 1, 3);
        public static int RockCount(float intensity) => intensity >= 1.6f ? 2 : 1;

        private const int SecondRockDelayTicks = 20;

        // 수박은 발밑이 아니라 근처에 — 정확히 노리면 예고 1초에 2.2m를 뛰어야 해 보통 사람은 못 피한다(4a 측정, 4b).
        public const float BombNearMin = 0.5f;
        public const float BombNearMax = 1.2f;
        /// <summary>수박 예고 하한(1.2초) — 세기가 올라도 반응 + 탈출 시간은 줄지 않는다. 1.5초·1~1.8m 옆은 사람 판에서 너무 쉬웠다.</summary>
        public const int BombMinWarnTicks = 60;
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
                    if (alivePositions.Count == 0) break;
                    {
                        Vector2 target = alivePositions[rng.Range(0, alivePositions.Count)];
                        // 가운데 투척기에서 그 사람 쪽으로 3갈래(탄막 규칙 — 발사원은 한 곳).
                        into.Add(new DodgePattern(nextId++, kind, start, 0, Thrower.x, Thrower.y, target.x, target.y));
                    }
                    break;
                case DodgePatternKind.Ring:
                    into.Add(new DodgePattern(nextId++, kind, start, 0, Thrower.x, Thrower.y,
                                              RingCount(at.Intensity), rng.Range(0f, 2f * Mathf.PI)));
                    break;
                case DodgePatternKind.Spiral:
                    {
                        float turn = rng.Range(SpiralTurnMin, SpiralTurnMax) * (rng.Range(0, 2) == 0 ? -1f : 1f);
                        into.Add(new DodgePattern(nextId++, kind, start, 0, Thrower.x, Thrower.y, SpiralArms(at.Intensity), turn));
                    }
                    break;
                case DodgePatternKind.Bomb:
                    foreach (var p in alivePositions)
                    {
                        float angle = rng.Range(0f, 2f * Mathf.PI), off = rng.Range(BombNearMin, BombNearMax);
                        float x = Mathf.Clamp(p.x + Mathf.Cos(angle) * off, -h, h), z = Mathf.Clamp(p.y + Mathf.Sin(angle) * off, -h, h);
                        into.Add(new DodgePattern(nextId++, kind, start, 0, x, z, config.BombRadius, 0f,
                                                  Mathf.Max(warn, BombMinWarnTicks)));
                    }
                    break;
                case DodgePatternKind.Laser:
                    for (int i = LaserCount(at.Intensity); i > 0; i--)
                    {
                        float pos = rng.Range(-h + 1f, h - 1f);
                        bool vertical = rng.Range(0, 2) == 0;
                        into.Add(vertical
                            ? new DodgePattern(nextId++, kind, start, 0, pos, -h, pos, h, warn)
                            : new DodgePattern(nextId++, kind, start, 0, -h, pos, h, pos, warn));
                    }
                    break;
                case DodgePatternKind.Rock:
                    for (int i = 0; i < RockCount(at.Intensity); i++)
                    {
                        into.Add(new DodgePattern(nextId++, kind, start + i * SecondRockDelayTicks, 0, rng.Range(0, 4),
                                                  rng.Range(-h + 2f, h - 2f), rng.Range(-0.35f, 0.35f), 0f, warn));
                    }
                    break;
                case DodgePatternKind.Tiles:
                    {
                        int n = Mathf.Max(1, config.TileCount);
                        int parity = rng.Range(0, 2);
                        ulong mask = 0;
                        for (int i = 0; i < n * n && i < 64; i++)
                        {
                            if (((i % n) + (i / n)) % 2 == parity) mask |= 1UL << i;   // 체크무늬 한쪽
                        }
                        into.Add(new DodgePattern(nextId++, kind, start, mask, 0f, 0f, 0f, 0f, Mathf.Max(warn, TileMinWarnTicks)));
                    }
                    break;
            }
        }
    }
}
