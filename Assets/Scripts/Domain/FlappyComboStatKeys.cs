namespace LOP
{
    /// <summary>
    /// Flappy가 매치 결과 자루(<c>MatchPlacement.stats</c>)에 넣는 연속 날갯짓 고비 키(spec 2026-10-28 §3). 값은 그 판에 연속이 그 고비를 넘은 횟수.
    /// 로비 경제 표(<c>EconomyConfig.combo_coins</c>)가 같은 문자열로 고비당 코인을 찾는다 — 이름을 바꾸면 코인이 조용히 0이 된다(표와 함께 바꾼다).
    /// </summary>
    public static class FlappyComboStatKeys
    {
        /// <summary>
        /// 코인을 세는 고비. 맵 마커의 대시 고비(<see cref="FlappyFlapComboRules"/>)를 따르지 않는다 — 경제가 맵 데이터로 바뀌면 안 된다.
        /// 고비당 코인 양은 로비 표가 정한다.
        /// </summary>
        public static readonly int[] Thresholds = { 10, 25, 50, 100 };

        public static string For(int threshold) => "flapCombo" + threshold;

        /// <summary>그 판 가장 긴 연속 날갯짓. 로비 퀘스트 "한 판에 연속 N번"이 읽는다(spec 2026-10-30 §4) — 표와 함께 바꾼다.</summary>
        public const string Best = "flapComboBest";

        /// <summary>새로 기절에 들어간 횟수(추격자에게 잡힌 것은 아니다). 로비 퀘스트 "기절 없이 완주"가 읽는다.</summary>
        public const string Stuns = "flapStuns";
    }
}
