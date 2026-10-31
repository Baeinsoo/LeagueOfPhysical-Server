using System;

namespace LOP
{
    /// <summary>
    /// 로컬 시험용 봇 채우기 스위치(넉백 생존전). 값은 "판을 이 인원까지 채운다"는 목표 인원이고 0이면 꺼짐.
    /// 읽는 곳은 넉백 규칙 하나뿐이다 — 다른 모드가 같은 env를 읽으면 봇이 새어 들어간다.
    /// 봇 아이디·이름·인원 계산은 말 활쏘기와 같은 <see cref="MountedArcheryBots"/> 규칙을 쓴다.
    /// </summary>
    public static class KnockbackTestBots
    {
        public const string EnvName = "KNOCKBACK_TEST_BOTS";
        private const string EditorPrefsKeyBase = "LOP.Debug.KnockbackTestBots";

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
    /// 넉백 규칙이 받는 봇 채우기 목표 인원. 맨 <c>Func&lt;int&gt;</c>로 스코프에 넣으면
    /// 나중에 <c>Func&lt;int&gt;</c>를 받는 다른 생성자가 생겼을 때 조용히 이 값을 받아 가서, 이름 있는 타입으로 감싼다.
    /// </summary>
    public sealed class KnockbackTestBotFill
    {
        private readonly Func<int> read;

        public KnockbackTestBotFill(Func<int> read)
        {
            this.read = read;
        }

        public int Read() => read();
    }
}
