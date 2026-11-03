using System;

namespace LOP
{
    /// <summary>
    /// 로컬 시험용 봇 채우기 스위치(카드 러시). 값은 "판을 이 인원까지 채운다"는 목표 인원이고 0이면 꺼짐.
    /// 읽는 곳은 카드 러시 규칙 하나뿐이다 — 다른 모드가 같은 env를 읽으면 봇이 새어 들어간다.
    /// 봇 아이디·이름·인원 계산은 말 활쏘기와 같은 <see cref="MountedArcheryBots"/> 규칙을 쓴다.
    /// </summary>
    public static class CardRushTestBots
    {
        public const string EnvName = "CARD_RUSH_TEST_BOTS";
        private const string EditorPrefsKeyBase = "LOP.Debug.CardRushTestBots";

        /// <summary>EditorPrefs는 컴퓨터 전체가 공유한다. 프로젝트 경로를 키에 넣어 다른 체크아웃으로 새지 않게 한다.</summary>
        public static string ScopedKey(string projectPath) => $"{EditorPrefsKeyBase}:{projectPath}";

#if UNITY_EDITOR
        public static string EditorPrefsKey => ScopedKey(UnityEngine.Application.dataPath);
#endif

        public static int ReadFillTo()
        {
            int? editorValue = null;
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.HasKey(EditorPrefsKey))
            {
                editorValue = UnityEditor.EditorPrefs.GetInt(EditorPrefsKey);
            }
#endif
            return MountedArcheryTestBots.Resolve(Environment.GetEnvironmentVariable(EnvName), editorValue);
        }
    }

    /// <summary>
    /// 카드 러시 룰이 받는 봇 채우기 목표 인원. 최소 레인(매칭 설정 <c>minLanes</c>)과 시험 스위치 중
    /// 더 큰 쪽을 목표로 삼고, 사람 수~8명 사이로 자른 뒤 사람 수를 빼 몇 명을 더 세울지 답한다.
    /// 스위치가 꺼져 있어도(0) <c>minLanes</c>만큼은 채운다 — 최소 레인 채우기는 시험 기능이 아니라
    /// 매칭 규칙이기 때문이다.
    /// </summary>
    public sealed class CardRushBotFill
    {
        private readonly Func<int> readSwitch;
        private readonly int minLanes;

        public CardRushBotFill(Func<int> readSwitch, int minLanes)
        {
            this.readSwitch = readSwitch;
            this.minLanes = minLanes;
        }

        public int BotsToAdd(int humans)
        {
            int target = Math.Max(minLanes, readSwitch());
            target = Clamp(target, humans, CardRushLanes.MaxLanes);
            return Math.Max(0, target - humans);
        }

        private static int Clamp(int value, int lo, int hi) => Math.Max(lo, Math.Min(value, hi));
    }
}
