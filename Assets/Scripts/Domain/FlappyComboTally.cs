namespace LOP
{
    /// <summary>연속 날갯짓 수가 지난 관측에서 이번 관측으로 바뀔 때 넘은 고비를 센다 — 순수 계산(spec 2026-10-28 §2).</summary>
    public static class FlappyComboTally
    {
        /// <summary>
        /// <paramref name="crossings"/>[i]에 고비 <c>thresholds[i]</c>를 넘은 횟수를 더한다. "넘는다" = 고비 미만에서 이상으로.
        /// 수가 줄었으면 끊긴 것(기절)이라 0에서 다시 오른 것으로 본다. 한 번에 여러 칸 올라도 그 사이 고비를 다 센다.
        /// </summary>
        public static void Observe(int last, int now, int[] thresholds, int[] crossings)
        {
            if (now < last)
            {
                last = 0;
            }
            for (int i = 0; i < thresholds.Length; i++)
            {
                if (last < thresholds[i] && thresholds[i] <= now)
                {
                    crossings[i]++;
                }
            }
        }
    }
}
