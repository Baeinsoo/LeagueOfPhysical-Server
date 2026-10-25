using System;

namespace LOP
{
    /// <summary>
    /// 로컬 시험용 봇 채우기 스위치. 값은 "판을 이 인원까지 채운다"는 목표 인원이고 0이면 꺼짐.
    /// 읽는 곳은 말 활쏘기 규칙 하나뿐이다 — 다른 모드가 같은 env를 읽으면 봇이 새어 들어간다.
    /// </summary>
    public static class MountedArcheryTestBots
    {
        public const string EnvName = "MOUNTED_ARCHERY_TEST_BOTS";
        public const string EditorPrefsKey = "LOP.Debug.MountedArcheryTestBots";

        /// <summary>에디터 메뉴 값이 있으면 그것이 먼저(0 이하는 꺼짐). 없으면 env를 정수로, 못 읽으면 꺼짐.</summary>
        public static int Resolve(string envValue, int? editorValue)
        {
            if (editorValue.HasValue) return Math.Max(0, editorValue.Value);
            if (int.TryParse(envValue, out int n) && n > 0) return n;
            return 0;
        }

        public static int ReadFillTo()
        {
            int? editorValue = null;
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.HasKey(EditorPrefsKey))
            {
                editorValue = UnityEditor.EditorPrefs.GetInt(EditorPrefsKey);
            }
#endif
            return Resolve(Environment.GetEnvironmentVariable(EnvName), editorValue);
        }
    }
}
