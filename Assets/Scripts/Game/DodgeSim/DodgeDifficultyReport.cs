using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 난이도 보고서(슬라이스 4a 스펙 §6). 봇 판(1·2·8인)과 검사기(1인)를 시드마다 돌려 표로 낸다.
    /// "막혀서 맞음" = 같은 시드를 충돌 없이 다시 돌렸을 때 줄어든 맞음의 비율(인원 2 이상만).
    /// </summary>
    public static class DodgeDifficultyReport
    {
        public const long MaxTicks = 15000;   // 서버 상한 5분

        public static string Build(DodgeConfig c, DodgeStageTable s, int seeds, int[] playerCounts)
        {
            var sb = new StringBuilder();
            string Slot(int i) => i < s.Count ? $"{i + 1} {s[i].Name}" : "서든데스";
            int slots = s.Count + 1;
            int SlotOf(long tick) { var at = s.At(tick, 0, c); return !at.Started ? 0 : at.SuddenDeath ? s.Count : at.Index; }

            sb.AppendLine($"# Dodge 난이도 보고서 — 시드 {seeds}개, 봇 반응 0.25초·빗나감 0.3m");
            sb.AppendLine();
            foreach (int n in playerCounts)
            {
                var hits = new double[slots]; var seconds = new double[slots];
                var elimSlot = new int[slots + 1];   // 마지막 칸 = 살아남음(우승)
                var lengths = new List<double>();
                int hitsCollide = 0, hitsFree = 0;
                for (ulong seed = 1; seed <= (ulong)seeds; seed++)
                {
                    var m = new DodgeSimMatch(seed, c, s, n);
                    m.Run(new DodgeHumanBot(seed), MaxTicks);
                    foreach (var (_, t) in m.Hits) hits[SlotOf(t)]++;
                    for (long t = 0; t <= m.Tick; t++) seconds[SlotOf(t)] += (double)AliveAt(m, t) / DodgeConfig.TicksPerSecond;
                    foreach (var (_, t) in m.Eliminations) elimSlot[SlotOf(t)]++;
                    elimSlot[slots] += n - m.Eliminations.Count;
                    lengths.Add(m.Tick / (double)DodgeConfig.TicksPerSecond);
                    if (n >= 2)
                    {
                        hitsCollide += m.Hits.Count;
                        var free = new DodgeSimMatch(seed, c, s, n, collide: false);
                        free.Run(new DodgeHumanBot(seed), MaxTicks);
                        hitsFree += free.Hits.Count;
                    }
                }
                sb.AppendLine($"## {n}인");
                sb.AppendLine();
                sb.AppendLine("| 스테이지 | 1분당 맞는 횟수(1인당) | 탈락 스테이지 비율 |");
                sb.AppendLine("|---|---|---|");
                int totalPlayers = seeds * n;
                for (int i = 0; i < slots; i++)
                {
                    double perMin = seconds[i] > 0 ? hits[i] / (seconds[i] / 60.0) : 0;
                    sb.AppendLine($"| {Slot(i)} | {perMin:F2} | {100.0 * elimSlot[i] / totalPlayers:F0}% |");
                }
                sb.AppendLine($"| 살아남음(우승) | — | {100.0 * elimSlot[slots] / totalPlayers:F0}% |");
                sb.AppendLine();
                lengths.Sort();
                sb.AppendLine($"- 판 길이 중앙값: {lengths[lengths.Count / 2]:F0}초");
                if (n >= 2)
                {
                    double blocked = hitsCollide > 0 ? 100.0 * (hitsCollide - hitsFree) / hitsCollide : 0;
                    sb.AppendLine($"- 다른 선수에 막혀서 맞은 비율: {blocked:F0}% (충돌 {hitsCollide}회 / 충돌 없음 {hitsFree}회)");
                }
                sb.AppendLine();
            }

            sb.AppendLine("## 검사기(완벽한 1인)");
            sb.AppendLine();
            var minCells = new List<int>[slots];
            for (int i = 0; i < slots; i++) minCells[i] = new List<int>();
            var sdDead = new List<double>(); int stageDead = 0;
            long stagesEnd = DodgeFeasibility.StagesEndTick(s, c);
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                var r = DodgeFeasibility.Run(seed, c, s, MaxTicks);
                for (int i = 0; i < slots; i++) if (r.MinCellsByStage[i] >= 0) minCells[i].Add(r.MinCellsByStage[i]);
                if (r.DeadTick >= 0 && r.DeadTick < stagesEnd) stageDead++;
                else if (r.DeadTick >= 0) sdDead.Add((r.DeadTick - r.SuddenDeathStartTick) / (double)DodgeConfig.TicksPerSecond);
            }
            sb.AppendLine("| 스테이지 | 가장 좁은 순간의 안전 칸 수(시드 중앙값 / 최솟값) |");
            sb.AppendLine("|---|---|");
            for (int i = 0; i < slots; i++)
            {
                var l = minCells[i]; l.Sort();
                sb.AppendLine(l.Count == 0 ? $"| {Slot(i)} | — |" : $"| {Slot(i)} | {l[l.Count / 2]} / {l[0]} |");
            }
            sb.AppendLine();
            sb.AppendLine($"- 스테이지 1~5에서 못 피하는 판: {stageDead}/{seeds}");
            sdDead.Sort();
            sb.AppendLine(sdDead.Count == 0
                ? "- 서든데스 불가능 시점: 5분 상한까지 안 옴"
                : $"- 서든데스 불가능 시점(서든데스 시작 뒤, 시드 중앙값): {sdDead[sdDead.Count / 2]:F0}초 ({sdDead.Count}/{seeds} 시드)");
            sb.AppendLine();
            sb.AppendLine("> 이동은 단순화(가속 무시) — 실제보다 조금 덜 맞게 나온다(스펙 §3).");
            return sb.ToString();
        }

        private static int AliveAt(DodgeSimMatch m, long t)
        {
            int dead = m.Eliminations.Count(e => e.tick <= t);
            return m.Players - dead;
        }
    }
}
