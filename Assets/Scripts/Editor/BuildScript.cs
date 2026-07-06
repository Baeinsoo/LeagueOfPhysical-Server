using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    // CI: Unity -batchmode -quit -nographics -projectPath . -executeMethod BuildScript.BuildLinuxServer -logFile -
    public static void BuildLinuxServer()
    {
        // 산출 경로: GameServer/Dockerfile 이 `COPY Build/`(컨텍스트 GameServer/)로 기대 → GameServer/Build/
        const string outputDir = "GameServer/Build";
        const string exe = outputDir + "/lop-server.x86_64";

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("Build FAILED: no enabled scenes in EditorBuildSettings");
            EditorApplication.Exit(1);
            return;
        }

        // Dedicated Server 서브타겟. 백엔드는 Mono2x —
        // IL2CPP는 Linux 크로스컴파일 sysroot 툴체인이 이 머신에 없어(Unable to find Linux Sysroot) 빌드 실패.
        // Mono는 sysroot 불필요하고 동작하는 Linux 서버 바이너리를 만든다. IL2CPP 전환은 sysroot 설치 후 후속.
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, ScriptingImplementation.Mono2x);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = exe,
            target = BuildTarget.StandaloneLinux64,
            subtarget = (int)StandaloneBuildSubtarget.Server,
            options = BuildOptions.None,
        };

        try
        {
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"Build FAILED: result={summary.result}, errors={summary.totalErrors}");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"Build OK: {summary.outputPath}, size={summary.totalSize} bytes");
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Build threw: {e}");
            EditorApplication.Exit(1);
        }
    }
}
