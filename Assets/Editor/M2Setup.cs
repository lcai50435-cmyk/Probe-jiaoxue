using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace M2.EditorTools
{
    /// <summary>
    /// M2 Scene 已冻结。此工具只打开现有 Scene，不生成、自愈或保存任何内容。
    /// </summary>
    public static class M2Setup
    {
        private const string ScenePath = "Assets/Settings/Scenes/M2.unity";

        public static void SetupM2Batch()
        {
            OpenFrozenScene();
        }

        [MenuItem("Tools/M2/Open Frozen M2 Scene %#&2")]
        public static void SetupM2()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[M2Setup] 请先退出 Play 模式再打开 M2 场景。");
                return;
            }

            OpenFrozenScene();
        }

        private static void OpenFrozenScene()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError("[M2Setup] 冻结的 M2 场景不存在，已跳过且不会创建：" + ScenePath);
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[M2Setup] M2 场景已冻结；本次仅打开，未生成、自愈或保存：" + ScenePath);
        }
    }
}
