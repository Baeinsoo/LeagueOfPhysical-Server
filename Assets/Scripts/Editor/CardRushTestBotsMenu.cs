using UnityEditor;

namespace LOP.EditorTools
{
    /// <summary>에디터에서 로컬 서버를 돌릴 때 카드 러시 봇 채우기를 켜고 끈다. 켜면 8명까지, 끄면 저장값을 지워 env 규칙으로 돌아간다.</summary>
    public static class CardRushTestBotsMenu
    {
        private const string Path = "LOP/Debug/카드 러시 시험 봇 채우기";
        private const int FillTo = 8;

        [MenuItem(Path)]
        private static void Toggle()
        {
            if (IsOn()) EditorPrefs.DeleteKey(CardRushTestBots.EditorPrefsKey);
            else EditorPrefs.SetInt(CardRushTestBots.EditorPrefsKey, FillTo);
        }

        [MenuItem(Path, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(Path, IsOn());
            return true;
        }

        private static bool IsOn() => EditorPrefs.GetInt(CardRushTestBots.EditorPrefsKey, 0) > 0;
    }
}
