using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시 튜닝 하네스(3차 spec §9, 4차 spec §8) — 후보 설정마다 씨앗 200개로 봇 판을 끝까지 돌려 길이·빈도·벌·봇 열을 고른다.
    /// 씨앗마다 두 판: ① 출시 봇 8명(인구 대리, 1600명분) ② 시험용 두뇌를 레인에 섞은 판(읽는 고수 0·4, 파랑만 1·5, 막누름 2·6, 노랑만 3·7).
    /// 결과는 <c>Logs/cardrush-tuning-&lt;시각&gt;-&lt;격자&gt;.txt</c>에 표로(한 줄 = 한 후보, 목표 밖 값엔 <c>!</c>) — 콘솔·Temp는 사라진다.
    /// 느리다(후보 하나 15~75초, 에디터가 뒤에 있으면 느려진다) — 평소 CI에서 안 돈다(<c>Explicit</c>). 이름 필터로만: <c>ats-sv.sh CardRushTuningHarness</c>.
    /// </summary>
    [Explicit("튜닝 하네스 — 느리다. 이름 필터로만 돌린다")]
    [Category("CardRushTuning")]
    [Timeout(int.MaxValue)]   //  격자 하나가 수 분~1시간 — 기본 3분 제한을 넘는다
    public class CardRushTuningHarness
    {
        private const int Seeds = 200;

        //  4차 spec §8 목표 — 봇 중앙·p95·미완, 장애물/분 7~12·구역/분 2~4, 봇이 막누름보다 8~12% 빠름.
        //  R2(판단이 이득)와 "막누름이 가장 느림"은 완주율 50% 이상인 정책끼리만 비교한다 — 파랑만은 구덩이를 못 건너 거의 다 미완이라
        //  120초로 센 중앙값과 비교하면 어떤 표 값으로도 자동 통과·자동 경고가 된다(11-13 리뷰 I1).
        //  R2 = (완주율 50% 이상인 "한 색만"과 막누름 중 가장 빠른 것) ÷ 고수 ≥ 1.15.
        private const float MedianMin = 55f, MedianMax = 75f, P95Max = 105f, DnfMax = 0.02f;
        private const float ObstaclesPerMinMin = 7f, ObstaclesPerMinMax = 12f, ZonesPerMinMin = 2f, ZonesPerMinMax = 4f;
        private const float R2RatioMin = 1.15f, BotEdgeMin = 1.08f, BotEdgeMax = 1.12f, EligibleFinishRate = 0.5f;

        /// <summary>후보 하나 — 출시 표 행에서 바꿀 열만. null이면 표 값 그대로.</summary>
        public sealed class Candidate
        {
            public float? Track, ObstacleMin, ObstacleMax, ZoneMin, ZoneMax, BarricadeShare, BarricadeWait, RollKnock, RollStun, ZoneKnock, ZoneStun;
            public float? PitShare, PitStun, BotFreeDash, BotReactMin, BotReactMax;

            public CardRushConfig Apply(CardRushConfig b) => new CardRushConfig(
                b.MatchDurationSeconds, b.MinLanes, Track ?? b.TrackLengthM,
                ObstacleMin ?? b.ObstacleGapMinM, ObstacleMax ?? b.ObstacleGapMaxM, BarricadeShare ?? b.BarricadeShare,
                ZoneMin ?? b.ZoneGapMinM, ZoneMax ?? b.ZoneGapMaxM, BarricadeWait ?? b.BarricadeWaitSeconds,
                RollKnock ?? b.RollKnockM, RollStun ?? b.RollStunSeconds, ZoneKnock ?? b.ZoneKnockM, ZoneStun ?? b.ZoneStunSeconds,
                BotReactMin ?? b.BotReactMinSeconds, BotReactMax ?? b.BotReactMaxSeconds, b.BotReactExtraSeconds, b.BotHesitateChance,
                BotFreeDash ?? b.BotFreeDashChance, PitShare ?? b.PitShare, PitStun ?? b.PitStunSeconds);

            public static string Label(CardRushConfig c) => string.Format(CultureInfo.InvariantCulture,
                "{0,3:0}m 장{1,4:0.#}~{2,-4:0.#} 구{3,4:0.#}~{4,-4:0.#} 바{5:0.00} 구덩{6:0.00}/{7:0.0} 기{8:0.0} 굴{9:0.00}/{10:0.0} 구벌{11:0.00}/{12:0.0} 봇대시{13:0.00} 반응{14:0.00}~{15:0.00}",
                c.TrackLengthM, c.ObstacleGapMinM, c.ObstacleGapMaxM, c.ZoneGapMinM, c.ZoneGapMaxM,
                c.BarricadeShare, c.PitShare, c.PitStunSeconds, c.BarricadeWaitSeconds, c.RollKnockM, c.RollStunSeconds, c.ZoneKnockM, c.ZoneStunSeconds,
                c.BotFreeDashChance, c.BotReactMinSeconds, c.BotReactMaxSeconds);
        }

        /// <summary>한 정책의 모은 분포.</summary>
        public sealed class PolicyStats
        {
            public readonly List<float> Seconds = new List<float>();   // 못 들어오면 120초로 센다
            public int Runs, Dnf;
            public double ObstaclesPerMin, ZonesPerMin, Hits, WaitSeconds, UsesPerMin;

            public void Add(in CardRushMatchRunner.RunnerResult r, float capSeconds)
            {
                float t = r.Finished ? r.Seconds : capSeconds;
                Runs++;
                if (!r.Finished) Dnf++;
                Seconds.Add(t);
                ObstaclesPerMin += r.Obstacles / t * 60f;
                ZonesPerMin += r.Zones / t * 60f;
                Hits += r.Hits;
                WaitSeconds += r.WaitTicks * CardRushTiming.TickInterval;
                UsesPerMin += r.Uses / t * 60f;
            }

            public void Close()
            {
                Seconds.Sort();
                if (Runs == 0) return;
                ObstaclesPerMin /= Runs; ZonesPerMin /= Runs; Hits /= Runs; WaitSeconds /= Runs; UsesPerMin /= Runs;
            }

            public float Median => CardRushMatchRunner.Quantile(Seconds, 0.5f);
            public float Q(float q) => CardRushMatchRunner.Quantile(Seconds, q);
            public float DnfRate => Runs == 0 ? 0f : (float)Dnf / Runs;
        }

        public sealed class Row
        {
            public CardRushConfig Config;
            public PolicyStats Bot = new PolicyStats(), Judged = new PolicyStats(), BlueOnly = new PolicyStats(), Masher = new PolicyStats(), YellowOnly = new PolicyStats();
            public float MasherRatio => Masher.Median / Judged.Median;
            /// <summary>봇이 막누름보다 얼마나 빠른가(막누름 중앙 ÷ 봇 중앙).</summary>
            public float BotEdge => Masher.Median / Bot.Median;

            /// <summary>완주율이 50% 이상이라 중앙값끼리 비교해도 되는 정책인가.</summary>
            public static bool Eligible(PolicyStats p) => p.Runs > 0 && 1f - p.DnfRate >= EligibleFinishRate;

            /// <summary>읽지 않는 정책(한 색만 둘 + 막누름) 중 비교할 수 있는 것.</summary>
            public IEnumerable<(string Name, PolicyStats Stats)> Unreading()
            {
                if (Eligible(BlueOnly)) yield return (CardRushTuningPolicies.BlueOnly, BlueOnly);
                if (Eligible(YellowOnly)) yield return (CardRushTuningPolicies.YellowOnly, YellowOnly);
                if (Eligible(Masher)) yield return (CardRushTuningPolicies.Masher, Masher);
            }

            /// <summary>R2 — 읽지 않는 정책 중 가장 빠른 것 ÷ 고수. 비교할 것이 없으면 NaN.</summary>
            public (string Name, float Ratio) R2()
            {
                string name = "-";
                float best = float.NaN;
                foreach (var (n, p) in Unreading())
                    if (float.IsNaN(best) || p.Median < best) { best = p.Median; name = n; }
                return (name, best / Judged.Median);
            }

            /// <summary>완주율 50% 이상인 정책 중 막누름보다 느린 것들 — "막누름이 가장 느림"이면 비어 있다.</summary>
            public List<string> SlowerThanMasher()
            {
                var list = new List<string>();
                if (!Eligible(Masher)) return list;
                foreach (var (n, p) in new[]
                         {
                             (CardRushTuningPolicies.Bot, Bot), (CardRushTuningPolicies.Judged, Judged),
                             (CardRushTuningPolicies.BlueOnly, BlueOnly), (CardRushTuningPolicies.YellowOnly, YellowOnly),
                         })
                    if (Eligible(p) && p.Median > Masher.Median) list.Add(n);
                return list;
            }
        }

        //  섞은 판의 레인 → 정책. 레인은 첫 장 씨앗일 뿐이라 정책을 레인에 고루 흩는다.
        private static string MixedPolicy(int lane) => (lane % 4) switch
        {
            0 => CardRushTuningPolicies.Judged,
            1 => CardRushTuningPolicies.BlueOnly,
            2 => CardRushTuningPolicies.Masher,
            _ => CardRushTuningPolicies.YellowOnly,
        };

        private static ICardRushBotPolicy MakeMixed(int lane) => (lane % 4) switch
        {
            0 => CardRushTuningPolicies.MakeJudged(),
            1 => new CardRushTuningPolicies.BlueOnlyPolicy(),
            2 => new CardRushTuningPolicies.MasherPolicy(),
            _ => (ICardRushBotPolicy)new CardRushTuningPolicies.YellowOnlyPolicy(),
        };

        public static Row Measure(CardRushConfig config, int seeds)
        {
            var row = new Row { Config = config };
            float cap = config.MatchDurationSeconds;
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                foreach (var r in CardRushMatchRunner.Run(config, seed)) row.Bot.Add(r, cap);
                foreach (var r in CardRushMatchRunner.Run(config, seed, (lane, rng) => MakeMixed(lane)))
                {
                    switch (MixedPolicy(r.Lane))
                    {
                        case CardRushTuningPolicies.Judged: row.Judged.Add(r, cap); break;
                        case CardRushTuningPolicies.BlueOnly: row.BlueOnly.Add(r, cap); break;
                        case CardRushTuningPolicies.YellowOnly: row.YellowOnly.Add(r, cap); break;
                        default: row.Masher.Add(r, cap); break;
                    }
                }
            }
            row.Bot.Close(); row.Judged.Close(); row.BlueOnly.Close(); row.Masher.Close(); row.YellowOnly.Close();
            return row;
        }

        private static string Flag(bool ok) => ok ? " " : "!";

        public static string Format(Row r)
        {
            var b = r.Bot;
            float med = b.Median, p95 = b.Q(0.95f);
            var r2 = r.R2();
            var slower = r.SlowerThanMasher();
            return string.Format(CultureInfo.InvariantCulture,
                "{0} | 봇 {1,5:0.0}{2} p5 {3,5:0.0} p95 {4,5:0.0}{5} 미완 {6,4:0.0}%{7} | 장/분 {8,4:0.0}{9} 구/분 {10,3:0.0}{11} 맞음 {12,3:0.0} 기다림 {13,3:0.0}s 사용/분 {14,3:0} | " +
                "고수 {15,5:0.0} p90 {16,5:0.0} 맞음 {17,3:0.0} 미완 {18,3:0.0}% | 파랑만 {19,5:0.0} 맞음 {20,5:0.0} 미완 {28,3:0}% | 노랑만 {31,5:0.0} 맞음 {32,4:0.0} 미완 {33,3:0}% | " +
                "막누름 {21,5:0.0} 맞음 {22,4:0.0} | R2비 {23:0.00}({24}){25} 막누름비 {26:0.00} 봇/막누름 {29:0.00}{30}{27}",
                Candidate.Label(r.Config),
                med, Flag(med >= MedianMin && med <= MedianMax), b.Q(0.05f), p95, Flag(p95 <= P95Max), b.DnfRate * 100f, Flag(b.DnfRate <= DnfMax),
                b.ObstaclesPerMin, Flag(b.ObstaclesPerMin >= ObstaclesPerMinMin && b.ObstaclesPerMin <= ObstaclesPerMinMax),
                b.ZonesPerMin, Flag(b.ZonesPerMin >= ZonesPerMinMin && b.ZonesPerMin <= ZonesPerMinMax),
                b.Hits, b.WaitSeconds, b.UsesPerMin,
                r.Judged.Median, r.Judged.Q(0.9f), r.Judged.Hits, r.Judged.DnfRate * 100f,
                r.BlueOnly.Median, r.BlueOnly.Hits,
                r.Masher.Median, r.Masher.Hits,
                r2.Ratio, r2.Name, Flag(r2.Ratio >= R2RatioMin), r.MasherRatio,
                slower.Count == 0 ? "" : " !막누름보다_느림:" + string.Join(",", slower),
                r.BlueOnly.DnfRate * 100f, r.BotEdge, Flag(r.BotEdge >= BotEdgeMin && r.BotEdge <= BotEdgeMax),
                r.YellowOnly.Median, r.YellowOnly.Hits, r.YellowOnly.DnfRate * 100f);
        }

        private static CardRushConfig ShippedConfig()
            => new CardRushConfigProvider(CardRushScopeWiringTests.RealMasterData()).Get();

        private static void RunGrid(string name, IEnumerable<Candidate> candidates)
        {
            var shipped = ShippedConfig();
            string dir = Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), "Logs");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"cardrush-tuning-{DateTime.Now:yyyyMMdd-HHmmss}-{name}.txt");
            var head = new StringBuilder();
            head.AppendLine($"# 카드 러시 튜닝 {name} — 씨앗 {Seeds} × (출시 봇 8 + 섞은 판 8), 시작 {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            head.AppendLine($"# 출시 표: {Candidate.Label(shipped)}");
            head.AppendLine($"# 목표: 봇 중앙 {MedianMin}~{MedianMax}초, p95 ≤ {P95Max}, 미완 ≤ {DnfMax * 100}%, 장/분 {ObstaclesPerMinMin}~{ObstaclesPerMinMax}, 구/분 {ZonesPerMinMin}~{ZonesPerMinMax}, " +
                            $"R2비(완주율 {EligibleFinishRate * 100}% 이상인 파랑만·노랑만·막누름 중 가장 빠른 것 ÷ 고수) ≥ {R2RatioMin}, 봇/막누름 {BotEdgeMin}~{BotEdgeMax}, " +
                            $"막누름이 가장 느림(완주율 {EligibleFinishRate * 100}% 이상끼리). 못 들어온 선수는 120초로 센다");
            File.WriteAllText(path, head.ToString());

            //  월드가 판마다 코스 지문을 찍는다 — 수천 줄이 콘솔을 덮지 않게 끈다.
            bool logWas = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int n = 0;
            try
            {
                foreach (var c in candidates)
                {
                    var row = Measure(c.Apply(shipped), Seeds);
                    File.AppendAllText(path, Format(row) + "\n");
                    n++;
                }
            }
            finally
            {
                Debug.unityLogger.logEnabled = logWas;
            }
            File.AppendAllText(path, $"# 끝: 후보 {n}개, {watch.Elapsed.TotalSeconds:0}초\n");
            TestContext.WriteLine(File.ReadAllText(path));
            Debug.Log($"[CardRushTuning] {name} 끝 — {path}");
        }

        /// <summary>
        /// 4차 첫 격자: 구덩이(비율·기절) × 길이 × 봇 빈 길 대시. 막누름은 구덩이에서만 크게 진다(빨 헛방·노 점프·파 대시가 모두 앞으로 가게 해서) —
        /// 구덩이 비율이 "봇이 막누름보다 8~12% 빠름"의 손잡이, 길이·봇 대시가 봇 중앙의 손잡이.
        /// </summary>
        [Test]
        public void 격자1_구덩이_길이_봇()
        {
            var list = new List<Candidate>();
            foreach (float track in new[] { 170f, 180f })
                foreach (float pit in new[] { 0.15f, 0.2f, 0.25f, 0.35f })
                    foreach (float stun in new[] { 0.5f, 0.8f })
                        foreach (float dash in new[] { 0.75f, 0.9f })
                            list.Add(new Candidate { Track = track, PitShare = pit, PitStun = stun, BotFreeDash = dash });
            RunGrid("v4-grid1", list);
        }

        /// <summary>고른 값 확인: 출시 표 그대로 + 길이·장애물 간격만 바꿔 본다(결승은 복도 205 m에서 15 m 이상 안쪽 — 190 m까지).</summary>
        [Test]
        public void 격자2_고른_값()
        {
            var list = new List<Candidate> { new Candidate() };
            foreach (float track in new[] { 170f, 175f, 185f, 190f })
                list.Add(new Candidate { Track = track });
            foreach (var (omin, omax) in new[] { (11f, 19f), (12f, 19f) })
                list.Add(new Candidate { ObstacleMin = omin, ObstacleMax = omax });
            RunGrid("v4-grid2", list);
        }

        /// <summary>출시 표 그대로 한 줄 — 정책 넷(고수·파랑만·막누름·노랑만)과 R2·"막누름 최느림" 판정을 다시 찍는다.</summary>
        [Test]
        public void 격자3_출시_표()
        {
            RunGrid("v4-grid3-shipped", new List<Candidate> { new Candidate() });
        }

        /// <summary>
        /// 4.3차 — 사용자 "장애물 좀 더 늘리고 기절 시간 늘리면". 막누름이 고수만큼 빨라서(4.2차 46.1 대 47.0) 장애물 간격을 좁히고
        /// 구덩이 기절·바리케이드 기다림을 늘려 카드를 읽는 쪽이 이기게 하는 값을 찾는다.
        /// </summary>
        [Test]
        public void 격자4_장애물_늘리고_기절_늘리기()
        {
            var list = new List<Candidate> { new Candidate() };
            foreach (var (omin, omax) in new[] { (9.4f, 13f), (10f, 14f) })
                foreach (float stun in new[] { 1.5f, 2.0f, 2.5f })
                    foreach (float wait in new[] { 2.0f, 3.0f })
                        list.Add(new Candidate { ObstacleMin = omin, ObstacleMax = omax, PitStun = stun, BarricadeWait = wait });
            RunGrid("v4-3-grid4", list);
        }
    }
}
