using System.Collections.Generic;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시 봇이 실제로 끝까지 판을 돈다 — 두뇌·채우기·입력이 맞물려 8명이 2분 상한 안에 완주하는지,
    /// 그리고 완주 시각이 봇 수치(반응·흔들림·실수)가 바뀌었을 때 알 수 있을 만큼 합리적인 범위인지(회귀 경보).
    /// </summary>
    public class CardRushBotMatchTests
    {
        private sealed class Seed : IMatchSeed { public ulong Value => 42UL; }

        private static CardRushConfig Config()
            => new CardRushConfig(matchDurationSeconds: 120f, moveSeconds: 0.3f, stunSeconds: 0.6f, minLanes: 4,
                                  botReactMinSeconds: 0.2f, botReactMaxSeconds: 0.5f, botTimingJitterSeconds: 0.05f,
                                  botMissChance: 0.1f);

        [Test]
        public void 봇_8명은_2분_안에_모두_완주()
        {
            var registry = new EntityRegistry();
            var field = new CardRushMapRulesField();
            field.Set(CardRushTheme.StoneBridge, 40, 2.2f, 2.6f, CardRushSections.Default,
                      Vector3.zero, Vector3.forward);
            var config = Config();
            var world = new CardRushWorld(registry, new WorldEventBuffer(), field, new Seed(), config.Timing);
            var finish = new FinishTrackingSystem(registry);
            var inputSystem = new InputBufferSystem();

            const long start = 1000;
            world.GameplayStartTick = start;

            var ids = new List<string>();
            for (int i = 0; i < 8; i++)
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
                entity.Add(new CardRushRunner { LaneOrigin = field.LaneOrigin(i) });
                registry.Add(entity);
                finish.Watch(id);
                ids.Add(id);
            }

            var botSystem = new CardRushBotSystem(registry, world, inputSystem, new Seed(), config, MountedArcheryBots.IsBot);

            long tick = start;
            for (; tick < start + 7500 && finish.AllWatchedFinished == false; tick++)
            {
                botSystem.Tick(tick, 0.02f);
                foreach (var id in ids)
                {
                    inputSystem.Consume(registry.Get(id).Get<InputBuffer>(), tick);
                }
                world.Tick(tick, 0.02f);
                finish.Tick(tick, 0.02f);
            }

            Assert.IsTrue(finish.AllWatchedFinished, "7500틱 안에 8명 모두 완주해야 한다");
            foreach (var id in ids)
            {
                var state = registry.Get(id).Get<FinishState>();
                Assert.IsTrue(state.Finished, id);
                float elapsedSeconds = (state.FinishedTick - start) * CardRushTiming.TickInterval;
                //  봇 수치(반응·흔들림·실수) 회귀 경보 — 범위를 벗어나면 그 수치가 바뀌었다는 뜻이다.
                Assert.GreaterOrEqual(elapsedSeconds, 45f, $"{id}: {elapsedSeconds}초");
                Assert.LessOrEqual(elapsedSeconds, 75f, $"{id}: {elapsedSeconds}초");
            }
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
