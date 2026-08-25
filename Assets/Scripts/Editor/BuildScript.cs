using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    // Linux 서버 아키텍처(x86_64/arm64) 지정. PlayerSettings.SetArchitecture는 iOS 전용이고
    // EditorUserBuildSettings.SetPlatformSettings는 안 먹는다. 실제 저장소는 Linux 빌드확장의
    // UnityEditor.LinuxStandalone.UserBuildSettings.architecture(Build Profile/classic 백킹).
    // 확장 어셈블리는 직접 참조 불가라 리플렉션으로 설정. (arch = "x86_64" | "arm64")
    static void SetLinuxArchitecture(string arch)
    {
        var asm = System.AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetType("UnityEditor.LinuxStandalone.UserBuildSettings") != null);
        if (asm == null) throw new System.Exception("LinuxStandalone extension assembly not found");
        var ubs = asm.GetType("UnityEditor.LinuxStandalone.UserBuildSettings");
        var helper = asm.GetType("UnityEditor.LinuxStandalone.LinuxArchitectureHelper");
        var fromStr = helper.GetMethod("GetArchitectureFromString", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        var archProp = ubs.GetProperty("architecture", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        var osArch = fromStr.Invoke(null, new object[] { arch }); // "x86_64"→x64, "arm64"→arm64
        archProp.SetValue(null, osArch);
        Debug.Log($"Linux architecture set: {arch} -> {archProp.GetValue(null)}");
    }

    // CI: LOP_BUILD_ARCH=x86_64|arm64 Unity -batchmode -quit -nographics -projectPath . \
    //     -executeMethod BuildScript.BuildLinuxServer -logFile -
    // 아치별로 산출 디렉토리를 분리(GameServer/Build-<arch>)해 멀티아치 도커 빌드에 각각 쓴다.
    public static void BuildLinuxServer()
    {
        var arch = (System.Environment.GetEnvironmentVariable("LOP_BUILD_ARCH") ?? "x86_64").Trim().ToLowerInvariant();
        if (arch != "x86_64" && arch != "arm64")
        {
            Debug.LogError($"Build FAILED: LOP_BUILD_ARCH must be x86_64 or arm64, got '{arch}'");
            EditorApplication.Exit(1);
            return;
        }

        string outputDir = $"GameServer/Build-{arch}";
        string exe = outputDir + "/lop-server.x86_64"; // 실행파일명은 유지(Dockerfile CMD 고정)

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("Build FAILED: no enabled scenes in EditorBuildSettings");
            EditorApplication.Exit(1);
            return;
        }

        // Dedicated Server 서브타겟 + IL2CPP. arm64 Linux 서버는 IL2CPP 전용(Unity에 arm64 Mono 없음).
        // sysroot는 manifest의 com.unity.sdk.linux-* 패키지가 제공.
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, ScriptingImplementation.IL2CPP);
        SetLinuxArchitecture(arch); // Server 서브타겟 설정 뒤에 호출(setter가 서브타겟을 봄)

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
                Debug.LogError($"Build FAILED: arch={arch}, result={summary.result}, errors={summary.totalErrors}");
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log($"Build OK: arch={arch}, {summary.outputPath}, size={summary.totalSize} bytes");
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Build threw: {e}");
            EditorApplication.Exit(1);
        }
    }
}
