using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace M1.EditorTools
{
    /// <summary>
    /// 幂等维护 Build Settings：M1-M5 固定为教学流程场景，M1 为启动场景（index 0）。
    /// 移除失效的场景引用（如已不存在的 SampleScene），保留用户其他有效场景。
    /// 菜单：Tools/M1/Setup Build Scenes；批处理入口供 CI 与无人值守执行。
    /// </summary>
    public static class BuildScenesSetup
    {
        private const string M1Path = "Assets/Settings/Scenes/M1.unity";
        private const string M2Path = "Assets/Settings/Scenes/M2.unity";
        private const string M3Path = "Assets/Settings/Scenes/M3.unity";
        private const string M4Path = "Assets/Settings/Scenes/M4.unity";
        private const string M5Path = "Assets/Settings/Scenes/M5.unity";

        public static readonly string[] RequiredScenePaths = { M1Path, M2Path, M3Path, M4Path, M5Path };

        /// <summary>命令行/批处理入口。</summary>
        public static void EnsureBuildScenesBatch()
        {
            EnsureBuildScenes();
            Debug.Log("[BuildScenesSetup] Batch 完成，Build Settings 已幂等更新。");
        }

        [MenuItem("Tools/M1/Setup Build Scenes")]
        public static void EnsureBuildScenes()
        {
            var wanted = RequiredScenePaths.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();

            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (string.IsNullOrEmpty(s.path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path) == null)
                {
                    Debug.Log("[BuildScenesSetup] 移除失效场景引用：" + (s.path ?? "<空>"));
                    continue; // 失效引用（如已删除的 SampleScene）直接移除
                }
                if (RequiredScenePaths.Contains(s.path)) continue; // 下方统一按 M1→M5 顺序重建
                scenes.Add(s); // 保留用户其他有效场景
            }

            var before = string.Join(";", EditorBuildSettings.scenes.Select(x => x.path).ToArray());
            for (var i = wanted.Length - 1; i >= 0; i--) scenes.Insert(0, wanted[i]); // M1-M5 固定顺序，M1 为启动场景
            var after = string.Join(";", scenes.Select(x => x.path).ToArray());
            if (before == after)
            {
                Debug.Log("[BuildScenesSetup] Build Settings 已正确，无需变更。");
                return;
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[BuildScenesSetup] Build Settings 更新：M1-M5 已确保，共 {scenes.Count} 个场景。");
        }
    }
}
