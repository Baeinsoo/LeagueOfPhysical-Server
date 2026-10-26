using UnityEditor;

namespace LOP.EditorTools
{
    /// <summary>에디터에서 로컬 서버를 돌릴 때 넉백 봇 채우기를 켜고 끈다. 켜면 8명까지, 끄면 저장값을 지워 env 규칙으로 돌아간다.</summary>
    public static class KnockbackTestBotsMenu
    {
        private const string Path = "LOP/Debug/넉백 시험 봇 채우기";
        private const int FillTo = 8;

        [MenuItem(Path)]
        private static void Toggle()
        {
            if (IsOn()) EditorPrefs.DeleteKey(KnockbackTestBots.EditorPrefsKey);
            else EditorPrefs.SetInt(KnockbackTestBots.EditorPrefsKey, FillTo);
        }

        [MenuItem(Path, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(Path, IsOn());
            return true;
        }

        private static bool IsOn() => EditorPrefs.GetInt(KnockbackTestBots.EditorPrefsKey, 0) > 0;
    }
}
