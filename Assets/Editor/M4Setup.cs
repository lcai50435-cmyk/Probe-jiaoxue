using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace M4.EditorTools
{
    /// <summary>
    /// M4 Scene 由 M3 复制基线而来（未冻结）。此工具只打开现有 Scene，不生成、自愈或保存任何内容。
    /// </summary>
    public static class M4Setup
    {
        private const string ScenePath = "Assets/Settings/Scenes/M4.unity";

        public static void SetupM4Batch()
        {
            OpenFrozenScene();
        }

        [MenuItem("Tools/M4/Open M4 Scene %#&4")]
        public static void SetupM4()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[M4Setup] 请先退出 Play 模式再打开 M4 场景。");
                return;
            }

            OpenFrozenScene();
        }

        private static void OpenFrozenScene()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError("[M4Setup] M4 场景不存在，已跳过且不会创建：" + ScenePath);
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[M4Setup] M4 场景；本次仅打开，未生成、自愈或保存：" + ScenePath);
        }
    }
}
