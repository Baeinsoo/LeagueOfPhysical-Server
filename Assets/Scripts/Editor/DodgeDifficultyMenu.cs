using System.IO;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    public static class DodgeDifficultyMenu
    {
        [MenuItem("LOP/Dodge/Measure Difficulty")]
        public static void Measure() => Run(100);

        /// <summary>eval에서도 부른다: <c>LOP.EditorTools.DodgeDifficultyMenu.Run(100)</c>. 대화상자 없이 파일과 로그만.</summary>
        public static string Run(int seeds)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string md = DodgeDifficultyReport.Build(DodgeSimTables.Config(), DodgeSimTables.Stages(), seeds, new[] { 1, 2, 8 });
            md += $"\n_측정 {sw.Elapsed.TotalSeconds:F0}초_\n";
            string path = Path.GetFullPath("Temp/dodge-difficulty.md");
            File.WriteAllText(path, md);
            Debug.Log("[Dodge] 난이도 보고서: " + path);
            return path;
        }
    }
}
