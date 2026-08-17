using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace M3.EditorTools
{
    /// <summary>
    /// M3 Scene 已冻结。此工具只打开现有 Scene，不生成、自愈或保存任何内容。
    /// </summary>
    public static class M3Setup
    {
        private const string ScenePath = "Assets/Settings/Scenes/M3.unity";

        public static void SetupM3Batch()
        {
            OpenFrozenScene();
        }

        [MenuItem("Tools/M3/Open Frozen M3 Scene %#&3")]
        public static void SetupM3()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[M3Setup] 请先退出 Play 模式再打开 M3 场景。");
                return;
            }

            OpenFrozenScene();
        }

        private static void OpenFrozenScene()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError("[M3Setup] 冻结的 M3 场景不存在，已跳过且不会创建：" + ScenePath);
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[M3Setup] M3 场景已冻结；本次仅打开，未生成、自愈或保存：" + ScenePath);
        }
    }
}
