using UnityEditor;

namespace LOP.EditorTools
{
    /// <summary>에디터에서 로컬 서버를 돌릴 때 봇 채우기를 켜고 끈다. 켜면 8명까지, 끄면 저장값을 지워 env 규칙으로 돌아간다.</summary>
    public static class MountedArcheryTestBotsMenu
    {
        private const string Path = "LOP/Debug/말 활쏘기 시험 봇 채우기";
        private const int FillTo = 8;

        [MenuItem(Path)]
        private static void Toggle()
        {
            if (IsOn()) EditorPrefs.DeleteKey(MountedArcheryTestBots.EditorPrefsKey);
            else EditorPrefs.SetInt(MountedArcheryTestBots.EditorPrefsKey, FillTo);
        }

        [MenuItem(Path, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(Path, IsOn());
            return true;
        }

        private static bool IsOn() => EditorPrefs.GetInt(MountedArcheryTestBots.EditorPrefsKey, 0) > 0;
    }
}
