using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace M2
{
    /// <summary>统一 M2-M5 重置弹窗文案与视觉；仅运行时修改，冻结 Scene 保持不变。</summary>
    public static class ModuleResetDialogStyle
    {
        private static readonly Color BorderColor = new Color(.18f, .24f, .28f, .72f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            Apply();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene _, LoadSceneMode __) => Apply();

        private static void Apply()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (sceneName != "M2" && sceneName != "M3" && sceneName != "M4" && sceneName != "M5") return;

            foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rt.name != "ResetConfirmDialog") continue;
                var title = FindChild(rt, "Title")?.GetComponent<TMP_Text>();
                if (title != null)
                {
                    title.text = $"重置 {sceneName} 流程？";
                    title.alignment = TextAlignmentOptions.Center;
                }

                CenterButton(rt, "ConfirmButton", -100f);
                CenterButton(rt, "CancelButton", 100f);
                var background = FindChild(rt, "bg")?.GetComponent<Image>();
                if (background == null) background = rt.GetComponent<Image>();
                EnsureBorder(background);
            }
        }

        private static void CenterButton(Transform root, string name, float x)
        {
            var rt = FindChild(root, name) as RectTransform;
            if (rt == null) return;
            rt.anchorMin = new Vector2(.5f, 0f);
            rt.anchorMax = new Vector2(.5f, 0f);
            rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
        }

        private static void EnsureBorder(Image background)
        {
            if (background == null) return;
            var outline = background.GetComponent<Outline>();
            if (outline == null)
            {
                outline = background.gameObject.AddComponent<Outline>();
                outline.hideFlags = HideFlags.DontSave;
            }
            outline.effectColor = BorderColor;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            outline.enabled = true;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                var found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
