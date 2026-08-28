using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace M1.EditorTools
{
    /// <summary>构建 Android APK：Debug 供 USB 调试，Release 用调试密钥自动签名、可直接分发安装；不处理 adb 部署。</summary>
    public static class AndroidDebugBuild
    {
        private const string OutputPath = "Builds/Android/ProbeTeaching-debug.apk";
        private const string ReleasePath = "Builds/Android/ProbeTeaching.apk";

        [MenuItem("Tools/Build/Build Android Debug APK")]
        public static void BuildFromMenu() => Build(OutputPath, true);

        /// <summary>批处理入口：Unity -executeMethod M1.EditorTools.AndroidDebugBuild.BuildBatch。</summary>
        public static void BuildBatch()
        {
            Build(OutputPath, true);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        [MenuItem("Tools/Build/Build Android Release APK")]
        public static void BuildReleaseFromMenu() => Build(ReleasePath, false);

        /// <summary>批处理入口：Unity -executeMethod M1.EditorTools.AndroidDebugBuild.BuildReleaseBatch。</summary>
        public static void BuildReleaseBatch()
        {
            Build(ReleasePath, false);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void Build(string outputPath, bool development)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("未安装 Unity Android Build Support，无法构建 APK。");

            BuildScenesSetup.EnsureBuildScenes();
            var required = BuildScenesSetup.RequiredScenePaths;
            var missing = required.Where(path => !EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == path)).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException("Build Settings 缺少启用场景：" + string.Join(", ", missing));

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var fullPath = Path.Combine(projectRoot, outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = required,
                locationPathName = fullPath,
                target = BuildTarget.Android,
                options = development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded || !File.Exists(fullPath))
                throw new InvalidOperationException("Android Debug APK 构建失败，请检查 BuildReport 和 Editor.log。");
            Debug.Log("[AndroidDebugBuild] 构建成功：" + fullPath);
        }
    }
}
