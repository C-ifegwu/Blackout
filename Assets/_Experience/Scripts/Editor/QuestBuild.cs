using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Blackout.Experience.Editor
{
    /// <summary>One-click Meta Quest 2 configuration and APK build (Android, ARM64, IL2CPP, Vulkan, OpenXR).</summary>
    public static class QuestBuild
    {
        public const string OutputPath = "Builds/Quest2/Blackout.apk";

        [MenuItem("Blackout/Quest 2/Configure Build Settings")]
        public static void Configure()
        {
            PlayerSettings.companyName = "BlackoutTeam";
            PlayerSettings.productName = "Blackout";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.blackoutteam.blackout");
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = true;

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            Debug.Log("[Blackout] Quest 2 build settings applied (Android, ARM64, IL2CPP, Vulkan).");
        }

        [MenuItem("Blackout/Quest 2/Build APK")]
        public static void BuildApk()
        {
            Configure();
            var scenes = new[] { "Assets/_World/Scenes/Blackout_City.unity", ExperienceSceneBuilder.ScenePath }.Where(File.Exists).Take(1).ToArray();
            if (scenes.Length == 0)
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Blackout] No scenes to build. Run Blackout > Experience > Create Test Scene first.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });

            var summary = report.summary;
            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                Debug.Log($"[Blackout] Quest 2 build SUCCEEDED: {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0}s)");
            else
                Debug.LogError($"[Blackout] Quest 2 build {summary.result}: {summary.totalErrors} error(s). See the Console.");
        }
    }
}
