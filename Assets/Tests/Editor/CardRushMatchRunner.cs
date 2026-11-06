using System;
using System.Collections.Generic;
using GameFramework.World;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시 봇 판 하나를 처음부터 끝까지 돌린다 — 서버와 같은 길(봇 시스템 → 입력 버퍼 → 월드 → 완주 추적).
    /// 마커·씬 없이 규칙 필드를 주입한 월드에 봇 8명(레인 0~7)을 넣는다. 레인마다 정책을 고를 수 있어
    /// 봇 판 경보(<see cref="CardRushBotMatchTests"/>)와 튜닝 하네스(<see cref="CardRushTuningHarness"/>)가 같이 쓴다.
    /// </summary>
    public static class CardRushMatchRunner
    {
        public const int Bots = 8;
        public const long Start = 1000;
        public const long MatchTicks = 6000;   // 2분 상한

        /// <summary>선수 한 명의 한 판.</summary>
        public struct RunnerResult
        {
            public int Lane;
            /// <summary>출발부터 완주까지 틱. 0 = 2분 안에 못 들어옴.</summary>
            public long FinishTicks;
            public int Hits;
            /// <summary>치운 장애물 수(부숨·올라감·막음·꽈당).</summary>
            public int Obstacles;
            /// <summary>터진 구역 수(피함·버팀·으악).</summary>
            public int Zones;
            /// <summary>바리케이드에 닿아 기다린 틱.</summary>
            public long WaitTicks;
            public int Uses;

            public bool Finished => FinishTicks != 0;
            public float Seconds => FinishTicks * CardRushTiming.TickInterval;
        }

        private sealed class Seed : IMatchSeed
        {
            public Seed(ulong value) { Value = value; }
            public ulong Value { get; }
        }

        /// <summary>한 판 — 레인 i의 봇은 <paramref name="policyFor"/>(i, 봇 난수)가 고른 정책(null이면 출시 봇).</summary>
        public static RunnerResult[] Run(CardRushConfig config, ulong seed, Func<int, System.Random, ICardRushBotPolicy> policyFor = null)
        {
            var registry = new EntityRegistry();
            var field = new CardRushMapRulesField();
            field.Set(CardRushTheme.StoneBridge, 2.6f, Vector3.zero, Vector3.forward);
            var world = new CardRushWorld(registry, new WorldEventBuffer(), field, new Seed(seed), config);
            var finish = new FinishTrackingSystem(registry);
            var inputSystem = new InputBufferSystem();
            world.GameplayStartTick = Start;

            var runners = new CardRushRunner[Bots];
            var buffers = new InputBuffer[Bots];
            for (int i = 0; i < Bots; i++)
            {
                string id = MountedArcheryBots.UserIdFor(i + 1);
                var entity = new Entity(id);
                entity.Add(new GameFramework.World.Transform());
                entity.Add(new Velocity());
                entity.Add(buffers[i] = new InputBuffer());
                entity.Add(new Simulated());
                entity.Add(new FinishState());
                entity.Add(new FinishPlacement());
                entity.Add(new Ownership(id));
                entity.Add(runners[i] = new CardRushRunner { LaneOrigin = field.LaneOrigin(i), Lane = i });
                registry.Add(entity);
                finish.Watch(id);
            }

            var botSystem = new CardRushBotSystem(registry, world, inputSystem, new Seed(seed), config, MountedArcheryBots.IsBot, policyFor);

            var wait = new long[Bots];
            for (long tick = Start; tick < Start + MatchTicks && finish.AllWatchedFinished == false; tick++)
            {
                botSystem.Tick(tick, 0.02f);
                for (int i = 0; i < Bots; i++)
                {
                    inputSystem.Consume(buffers[i], tick);
                }
                world.Tick(tick, 0.02f);
                finish.Tick(tick, 0.02f);
                for (int i = 0; i < Bots; i++)
                {
                    if (WaitingAtBarricade(runners[i].State, world.Track)) wait[i]++;
                }
            }

            var results = new RunnerResult[Bots];
            for (int i = 0; i < Bots; i++)
            {
                var s = runners[i].State;
                results[i] = new RunnerResult
                {
                    Lane = i,
                    FinishTicks = s.FinishTick != 0 ? s.FinishTick - Start : 0,
                    Hits = s.Hits,
                    Obstacles = s.ObstacleIndex,
                    Zones = s.ZoneIndex,
                    WaitTicks = wait[i],
                    Uses = s.UseCount,
                };
            }
            return results;
        }

        private static bool WaitingAtBarricade(in CardRushRunnerState s, CardRushTrack track)
            => track != null && s.FinishTick == 0 && s.ObstacleTick != 0 && s.ObstacleIndex < track.Obstacles.Count
               && track.Obstacles[s.ObstacleIndex].Kind == CardRushObstacleKind.Barricade;

        /// <summary>정렬된 값의 분위(선형 보간). 비면 NaN.</summary>
        public static float Quantile(List<float> sorted, float q)
        {
            if (sorted.Count == 0) return float.NaN;
            float i = q * (sorted.Count - 1);
            int lo = (int)Math.Floor(i), hi = (int)Math.Ceiling(i);
            return sorted[lo] + (sorted[hi] - sorted[lo]) * (i - lo);
        }
    }
}
