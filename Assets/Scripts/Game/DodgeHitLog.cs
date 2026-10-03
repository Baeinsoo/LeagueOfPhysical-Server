namespace LOP
{
    /// <summary>보정 로그 한 줄(슬라이스 4a 스펙 §7) — 사람 판의 스테이지별 맞음을 kubectl logs에서 뽑아 봇과 대조한다.</summary>
    public static class DodgeHitLog
    {
        public static string Format(in DodgeStagePoint at, long tick, long gameplayStartTick, int lives)
        {
            string stage = at.SuddenDeath ? "SD" : (at.Index + 1).ToString();
            float t = (tick - gameplayStartTick) / (float)DodgeConfig.TicksPerSecond;
            return $"[Dodge] hit stage={stage} t={t.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} lives={lives}";
        }
    }
}
