using System.Collections.Generic;
using GameFramework.Rng;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 다음 위험을 고른다(서버 전용). 매치 씨앗으로 뽑아 같은 판을 다시 돌리면 같은 순서가 나온다.
    /// 고른 패턴은 지금이 아니라 예약 시간 뒤에 시작한다 — 핑 낮은 사람이 먼저 보지 않게(스펙 §5.1).
    /// 사람을 노리는 패턴(조준탄·발밑 폭탄)은 고르는 순간의 서버 위치를 담는다.
    /// </summary>
    public class DodgeDirector
    {
        private static readonly DodgePatternKind[] Rotation =
        {
            DodgePatternKind.BulletRain, DodgePatternKind.Bomb, DodgePatternKind.BulletWall, DodgePatternKind.Laser,
            DodgePatternKind.BulletAimed, DodgePatternKind.Rock, DodgePatternKind.Tiles,
        };

        private readonly ulong seed;
        private readonly DodgeConfig config;
        private int counter;
        private int nextId = 1;
        private long nextTick = long.MinValue;

        public DodgeDirector(ulong matchSeed, DodgeConfig config)
        {
            seed = Hashing.Combine(matchSeed, Hashing.Fnv1a64("dodge-director"));
            this.config = config;
        }

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
            nextTick = tick + config.PatternIntervalTicks;

            var kind = config.OnlyKind > 0 ? (DodgePatternKind)config.OnlyKind : Rotation[counter % Rotation.Length];
            // 뽑는 순서가 계약이다 — 같은 씨앗에서 같은 판이 나와야 재현·디버깅이 된다.
            var rng = new DeterministicRandom(Hashing.Combine(seed, (ulong)counter));
            counter++;

            long start = tick + config.LeadTicks;
            float h = config.ArenaHalf;
            switch (kind)
            {
                case DodgePatternKind.BulletRain:
                    into.Add(new DodgePattern(nextId++, kind, start, rng.NextUInt64(), rng.Range(0, 4), 12f, 4f, 0.15f));
                    break;
                case DodgePatternKind.BulletWall:
                    into.Add(new DodgePattern(nextId++, kind, start, 0, rng.Range(0, 4), rng.Range(-h + 2f, h - 2f), 3f, 0.9f));
                    break;
                case DodgePatternKind.BulletAimed:
                    if (alivePositions.Count == 0) break;
                    {
                        Vector2 target = alivePositions[rng.Range(0, alivePositions.Count)];
                        Vector2 origin = DodgeHazards.EdgePoint(rng.Range(0, 4), rng.Range(-h, h), config.EdgeDistance);
                        into.Add(new DodgePattern(nextId++, kind, start, 0, origin.x, origin.y, target.x, target.y));
                    }
                    break;
                case DodgePatternKind.Bomb:
                    foreach (var p in alivePositions)
                    {
                        into.Add(new DodgePattern(nextId++, kind, start, 0, p.x, p.y, config.BombRadius, 0f));
                    }
                    break;
                case DodgePatternKind.Laser:
                    {
                        float at = rng.Range(-h + 1f, h - 1f);
                        bool vertical = rng.Range(0, 2) == 0;
                        into.Add(vertical
                            ? new DodgePattern(nextId++, kind, start, 0, at, -h, at, h)
                            : new DodgePattern(nextId++, kind, start, 0, -h, at, h, at));
                    }
                    break;
                case DodgePatternKind.Rock:
                    into.Add(new DodgePattern(nextId++, kind, start, 0, rng.Range(0, 4), rng.Range(-h + 2f, h - 2f),
                                              rng.Range(-0.35f, 0.35f), 0f));
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
                        into.Add(new DodgePattern(nextId++, kind, start, mask, 0f, 0f, 0f, 0f));
                    }
                    break;
            }
        }
    }
}
