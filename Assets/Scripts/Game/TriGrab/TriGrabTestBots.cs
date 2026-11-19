using System;

namespace LOP
{
    /// <summary>
    /// 로컬 시험용 봇 채우기 스위치(삼색 그랩). 값은 "판을 이 인원까지 채운다"는 목표 인원이고 0이면 꺼짐.
    /// 읽는 곳은 삼색 그랩 규칙 하나뿐이다 — 다른 모드가 같은 env를 읽으면 봇이 새어 들어간다.
    /// </summary>
    public static class TriGrabTestBots
    {
        public const string EnvName = "TRI_GRAB_TEST_BOTS";
        private const string EditorPrefsKeyBase = "LOP.Debug.TriGrabTestBots";

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
    /// 삼색 그랩 봇 채우기 목표 — 최소 인원(매칭 규칙)과 시험 스위치 중 더 큰 쪽을 사람 수~8명으로 자르고 사람 수를 뺀다.
    /// 스위치가 꺼져 있어도 최소 인원만큼은 채운다. 맨 Func를 스코프에 넣으면 다른 생성자가 조용히 받아 가서 타입으로 감쌌다.
    /// </summary>
    public sealed class TriGrabBotFill
    {
        private readonly Func<int> readSwitch;
        private readonly int minPlayers;

        public TriGrabBotFill(Func<int> readSwitch, int minPlayers)
        {
            this.readSwitch = readSwitch;
            this.minPlayers = minPlayers;
        }

        public int BotsToAdd(int humans)
        {
            int target = Math.Max(minPlayers, readSwitch());
            target = Math.Max(humans, Math.Min(target, MountedArcheryBots.MaxPlayers));
            return Math.Max(0, target - humans);
        }
    }
}
