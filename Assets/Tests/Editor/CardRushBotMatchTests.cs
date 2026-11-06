using System.Collections.Generic;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시 봇이 실제로 끝까지 판을 돈다 — 두뇌·입력·월드가 맞물려 씨앗 20개 × 8명이 2분 상한 안에 완주하는지,
    /// 그리고 완주 시간 중앙값이 봇·표 수치가 바뀌었을 때 알 수 있을 만큼 합리적인 범위인지(회귀 경보).
    /// </summary>
    public class CardRushBotMatchTests
    {
        private sealed class Seed : IMatchSeed
        {
            public Seed(ulong value) { Value = value; }
            public ulong Value { get; }
        }

        //  출시 표(#CardRushConfig 1행) 그대로 — 손으로 적은 수치로 돌리면 표를 고쳐도 이 경보가 안 울린다(최종 리뷰 M5).
        private static CardRushConfig Config()
            => new CardRushConfigProvider(CardRushScopeWiringTests.RealMasterData()).Get();

        private const long Start = 1000;
        private const long MatchTicks = 6000;   // 2분 상한
        private const int Bots = 8;

        //  한 판: 봇 8명(레인 0~7)을 출시 봇으로 2분 돌려 선수별 완주 초(못 하면 null).
        private static float?[] Play(CardRushConfig config, ulong seed)
        {
            var registry = new EntityRegistry();
            var field = new CardRushMapRulesField();
            field.Set(CardRushTheme.StoneBridge, 2.6f,
                      Vector3.zero, Vector3.forward);
            var world = new CardRushWorld(registry, new WorldEventBuffer(), field, new Seed(seed), config);
            var finish = new FinishTrackingSystem(registry);
            var inputSystem = new InputBufferSystem();
            world.GameplayStartTick = Start;

            var ids = new List<string>();
            for (int i = 0; i < Bots; i++)
            {
                string id = MountedArcheryBots.UserIdFor(i + 1);
                var entity = new Entity(id);
                entity.Add(new GameFramework.World.Transform());
                entity.Add(new Velocity());
                entity.Add(new InputBuffer());
                entity.Add(new Simulated());
                entity.Add(new FinishState());
                entity.Add(new FinishPlacement());
                entity.Add(new Ownership(id));
                entity.Add(new CardRushRunner { LaneOrigin = field.LaneOrigin(i), Lane = i });
                registry.Add(entity);
                finish.Watch(id);
                ids.Add(id);
            }

            var botSystem = new CardRushBotSystem(registry, world, inputSystem, new Seed(seed), config, MountedArcheryBots.IsBot);

            for (long tick = Start; tick < Start + MatchTicks && finish.AllWatchedFinished == false; tick++)
            {
                botSystem.Tick(tick, 0.02f);
                foreach (var id in ids)
                {
                    inputSystem.Consume(registry.Get(id).Get<InputBuffer>(), tick);
                }
                world.Tick(tick, 0.02f);
                finish.Tick(tick, 0.02f);
            }

            var seconds = new float?[Bots];
            for (int i = 0; i < Bots; i++)
            {
                var state = registry.Get(ids[i]).Get<FinishState>();
                seconds[i] = state.Finished ? (state.FinishedTick - Start) * CardRushTiming.TickInterval : (float?)null;
            }
            return seconds;
        }

        [Test]
        public void 봇_8명은_2분_안에_모두_완주()
        {
            var config = Config();
            var all = new List<float>();
            int unfinished = 0;
            var perSeed = new System.Text.StringBuilder();
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var seconds = Play(config, seed);
                var sorted = new List<float>();
                foreach (var sec in seconds)
                {
                    if (sec.HasValue) { sorted.Add(sec.Value); all.Add(sec.Value); }
                    else unfinished++;
                }
                sorted.Sort();
                perSeed.Append($"씨앗 {seed}: 완주 {sorted.Count}/{Bots} [{string.Join(", ", sorted.ConvertAll(x => x.ToString("0.0")))}]\n");
            }
            all.Sort();
            float median = all.Count == 0 ? float.NaN : (all[(all.Count - 1) / 2] + all[all.Count / 2]) / 2f;
            string report = $"중앙값 {median:0.0}초, 미완주 {unfinished}/{20 * Bots}\n{perSeed}";

            //  모두 2분 안 + 중앙값이 넓은 경보 범위(45~85초) 안 — Task 8 하네스 결과로 좁힌다.
            Assert.AreEqual(0, unfinished, report);
            Assert.That(median, Is.InRange(45f, 85f), report);
            TestContext.WriteLine(report);
        }

        [Test]
        public void 봇이_있으면_연습_판()
        {
            var outcome = new MatchOutcome
            {
                placements = new List<MatchPlacement>
                {
                    new MatchPlacement { userId = "human-1", placement = 1 },
                    new MatchPlacement { userId = MountedArcheryBots.UserIdFor(1), placement = 2 },
                },
            };

            Assert.IsTrue(MatchEndedMessageBuilder.IsPractice(outcome));
        }
    }
}
