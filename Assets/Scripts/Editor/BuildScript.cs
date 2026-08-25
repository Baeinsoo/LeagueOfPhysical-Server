using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
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

    // 에셋 카탈로그(Addressables)는 플레이어 빌드에 자동으로 따라오지 않는다. 프로젝트 설정의
    // "Build Addressables on Player Build"는 기본값이 *에디터 환경설정을 따름*이라 레포에 없고
    // 빌드 머신마다 다르다 — CI 러너는 Library/를 지우지 않으므로 예전에 구워 둔 카탈로그가
    // 그대로 남아, 새로 등록한 에셋이 런타임에 "No Location found"로 실패한다(실제로 겪음:
    // 판치기 동전 프리팹). 그래서 여기서 명시적으로 굽는다 — 빌드 머신 설정에 기대지 않는다.
    static void BuildAddressables()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            throw new System.Exception("AddressableAssetSettings not found");
        }

        //  옛 번들이 남아 있으면 새 카탈로그와 섞인다 — 지우고 처음부터 굽는다.
        AddressableAssetSettings.CleanPlayerContent(settings.ActivePlayerDataBuilder);
        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);

        if (string.IsNullOrEmpty(result.Error) == false)
        {
            throw new System.Exception($"Addressables build failed: {result.Error}");
        }

        //  remoteCatalog=true면 런타임이 S3의 카탈로그를 먼저 본다 — 여기서 구운 것이 그대로
        //  쓰이지 않는다는 뜻이라, 진단할 때 이 값을 같이 봐야 한다.
        Debug.Log($"Addressables OK: {result.LocationCount} locations, {result.Duration:F1}s, " +
                  $"remoteCatalog={settings.BuildRemoteCatalog}");
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
            BuildAddressables();

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
