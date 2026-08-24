using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace M1.EditorTools
{
    /// <summary>构建可 USB 安装的 Android Debug APK；不处理签名或 adb 部署。</summary>
    public static class AndroidDebugBuild
    {
        private const string OutputPath = "Builds/Android/ProbeTeaching-debug.apk";

        [MenuItem("Tools/Build/Build Android Debug APK")]
        public static void BuildFromMenu() => Build();

        /// <summary>批处理入口：Unity -executeMethod M1.EditorTools.AndroidDebugBuild.BuildBatch。</summary>
        public static void BuildBatch()
        {
            Build();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("未安装 Unity Android Build Support，无法构建 APK。");

            BuildScenesSetup.EnsureBuildScenes();
            var required = BuildScenesSetup.RequiredScenePaths;
            var missing = required.Where(path => !EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == path)).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException("Build Settings 缺少启用场景：" + string.Join(", ", missing));

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var fullPath = Path.Combine(projectRoot, OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = required,
                locationPathName = fullPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            });

            if (report.summary.result != BuildResult.Succeeded || !File.Exists(fullPath))
                throw new InvalidOperationException("Android Debug APK 构建失败，请检查 BuildReport 和 Editor.log。");
            Debug.Log("[AndroidDebugBuild] 构建成功：" + fullPath);
        }
    }
}
