using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace M2
{
    /// <summary>
    /// 模块标题去编号前缀（台词.pptx Slide 3-【1】）：「M3 轨头侧面探测」→「轨头侧面探测」。
    /// 冻结 Scene 不可改节点文本，故场景加载后运行时覆盖（不写回）；M2 已无前缀，跳过。
    /// </summary>
    public static class ModuleTitleStrip
    {
        private const string TitleName = "ModuleTitle";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            TryStrip();
            SceneManager.sceneLoaded += (_, _) => TryStrip(); // 场景切换（M2→M3→M4→M5）后同样生效
        }

        private static void TryStrip()
        {
            foreach (var tmp in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
            {
                if (tmp.name != TitleName || string.IsNullOrEmpty(tmp.text)) continue;
                var t = tmp.text;
                if (t.Length >= 2 && (t[0] == 'M' || t[0] == 'm') && char.IsDigit(t[1]))
                {
                    var idx = t.IndexOf(' ');
                    if (idx >= 0) tmp.text = t.Substring(idx + 1);
                }
            }
        }
    }
}
