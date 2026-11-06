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
        //  출시 표(#CardRushConfig 1행) 그대로 — 손으로 적은 수치로 돌리면 표를 고쳐도 이 경보가 안 울린다(최종 리뷰 M5).
        private static CardRushConfig Config()
            => new CardRushConfigProvider(CardRushScopeWiringTests.RealMasterData()).Get();

        private const int Bots = CardRushMatchRunner.Bots;

        [Test]
        public void 봇_8명은_2분_안에_모두_완주()
        {
            var config = Config();
            var all = new List<float>();
            int unfinished = 0;
            var perSeed = new System.Text.StringBuilder();
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var sorted = new List<float>();
                foreach (var r in CardRushMatchRunner.Run(config, seed))
                {
                    if (r.Finished) { sorted.Add(r.Seconds); all.Add(r.Seconds); }
                    else unfinished++;
                }
                sorted.Sort();
                perSeed.Append($"씨앗 {seed}: 완주 {sorted.Count}/{Bots} [{string.Join(", ", sorted.ConvertAll(x => x.ToString("0.0")))}]\n");
            }
            all.Sort();
            float median = all.Count == 0 ? float.NaN : (all[(all.Count - 1) / 2] + all[all.Count / 2]) / 2f;
            string report = $"중앙값 {median:0.0}초, 미완주 {unfinished}/{20 * Bots}\n{perSeed}";

            //  모두 2분 안 + 중앙값이 하네스 중앙(62.5초, 씨앗 200, 170 m — CardRushTuningHarness 11-07) ± 8초 안.
            //  아래쪽은 목표 하한 55초로 자른다(spec §9.3 55~75). 표·봇·규칙이 바뀌어 벗어나면 하네스를 다시 돌린다.
            Assert.AreEqual(0, unfinished, report);
            Assert.That(median, Is.InRange(55f, 70f), report);
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
