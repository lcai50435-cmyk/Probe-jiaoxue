using System.IO;
using M1;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace M1.EditorTools
{
    /// <summary>
    /// 编辑器一次性配置：Tools/M1/Setup M1-1
    /// 1) 移除 "画板" 上的缺失脚本引用
    /// 2) 挂载 M1ToolSelection
    /// 3) 创建 "点击继续" 占位按钮（默认隐藏）
    /// 4) 把场景里所有 TMP 文字的字体重指向到生成好的中文 SDF 资产（*_cn.asset）
    /// 幂等：重复执行不会重复创建/挂载/重指向。
    /// </summary>
    public static class M1Setup
    {
        private const string BoardName = "画板";
        private const string ContinueButtonName = "点击继续";
        private const string FontAssetPath =
            "Assets/font/sarasa-gothic-sc-regular/sarasa-gothic-sc-regular_cn.asset";

        [MenuItem("Tools/M1/Setup M1-1 %#&m")]
        public static void SetupM11()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[M1Setup] 请先退出 Play 模式再运行 Setup。");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            var board = GameObject.Find(BoardName);
            if (board == null)
            {
                Debug.LogError("[M1Setup] 未找到场景物体：" + BoardName);
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(board, "M1 Setup");

            // 0) 加载中文 SDF 字体资产（由 Tools/字体/重新生成中文字体资产 生成）
            var cnFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (cnFont == null)
            {
                Debug.LogError("[M1Setup] 未找到中文字体资产：" + FontAssetPath +
                               "，请先运行菜单 Tools/字体/重新生成中文字体资产 (Sarasa Gothic) 再执行本 Setup。");
            }

            // 1) 移除缺失脚本
            var removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(board);

            // 2) 挂载运行时脚本
            var comp = board.GetComponent<M1ToolSelection>();
            if (comp == null) comp = board.AddComponent<M1ToolSelection>();
            // 规范化路径字段（幂等，确保序列化值正确）
            comp.toolsRootPath = "白板背景/物品";
            comp.aiAnswerPath = "白板背景/数字人/对话框/AI回答";
            comp.continueButtonPath = "点击继续";
            EditorUtility.SetDirty(comp);

            // 3) 创建占位按钮
            var button = EnsureContinueButton(board, cnFont);

            // 4) 场景所有 TMP 字体重指向到中文 SDF
            var repointed = 0;
            if (cnFont != null)
            {
                foreach (var tmp in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
                {
                    if (tmp == null) continue;
                    if (!tmp.gameObject.scene.IsValid()) continue; // 只处理场景物体，跳过内存中的 prefab 资产
                    if (tmp.font != cnFont)
                    {
                        tmp.font = cnFont;
                        EditorUtility.SetDirty(tmp);
                        repointed++;
                    }
                }
            }

            // 4.5) 补齐缺失的工具/装饰图片（幂等：已有 Sprite 的不覆盖）
            var spriteFixed = EnsureSprites(board);

            EditorSceneManager.MarkSceneDirty(scene);
            var saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[M1Setup] 完成：移除缺失脚本 {removed} 个；挂载 {comp.GetType().Name}；" +
                      $"按钮 {button.name} (active={button.activeSelf})；重指向 TMP {repointed} 个；修复图片 {spriteFixed} 个；场景保存={saved}");
        }

        private static GameObject EnsureContinueButton(GameObject board, TMP_FontAsset font)
        {
            var existing = FindIncludingInactive(board.transform, ContinueButtonName);
            if (existing != null)
            {
                Debug.Log("[M1Setup] 已存在 " + ContinueButtonName + "，跳过创建。");
                return existing.gameObject;
            }

            var go = new GameObject(ContinueButtonName, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            go.transform.SetParent(board.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 80f);
            rt.sizeDelta = new Vector2(240f, 76f);

            var img = go.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = new Color(0.15f, 0.42f, 0.82f, 1f);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => Debug.Log("[M1-1] 点击继续：M1-2 尚未实现（占位）。"));

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text = "点击继续";
            tmp.fontSize = 36;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            if (font != null) tmp.font = font;
            else Debug.LogWarning("[M1Setup] 未找到字体资产：" + FontAssetPath);

            // 默认隐藏，选对后由运行时脚本显示
            go.SetActive(false);
            return go;
        }

        private static Transform FindIncludingInactive(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                var hit = FindIncludingInactive(child, name);
                if (hit != null) return hit;
            }
            return null;
        }
        /// <summary>按路径查找子物体（含未激活物体）。</summary>
        private static Transform FindDeep(Transform root, string path)
        {
            var cur = root;
            foreach (var part in path.Split('/'))
            {
                cur = FindIncludingInactive(cur, part);
                if (cur == null) return null;
            }
            return cur;
        }

        /// <summary>
        /// 补齐画板下缺失的按钮/装饰图片（幂等：已有 Sprite 的不覆盖）。
        /// 修复对象：手推式钢轨探伤仪 / 钢轨打磨机 / 内燃威客镐 / 背景圆 / 对话框。
        /// </summary>
        private static int EnsureSprites(GameObject board)
        {
            var map = new (string objPath, string spritePath)[]
            {
                ("白板背景/物品/手推式钢轨探伤仪", "Assets/交互动画素材/01 探伤工具素材/手推式钢轨探伤仪.jpg"),
                ("白板背景/物品/钢轨打磨机", "Assets/交互动画素材/01 探伤工具素材/钢轨打磨机.jpeg"),
                ("白板背景/物品/内燃威客镐", "Assets/交互动画素材/01 探伤工具素材/内燃威客镐.jpeg"),
                ("白板背景/数字人/背景圆", "Assets/交互动画素材/额外/圆蓝色.png"),
                ("白板背景/数字人/对话框", "Assets/交互动画素材/额外/对话框.png"),
            };

            int fixedCount = 0;
            foreach (var (objPath, spritePath) in map)
            {
                var go = FindDeep(board.transform, objPath);
                if (go == null)
                {
                    Debug.LogWarning("[M1Setup] 未找到物体：" + objPath);
                    continue;
                }
                var img = go.GetComponent<Image>();
                if (img == null)
                {
                    Debug.LogWarning("[M1Setup] 物体缺少 Image：" + objPath);
                    continue;
                }
                if (img.sprite != null) continue; // 已有图片，跳过
                var spr = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (spr == null)
                {
                    Debug.LogError("[M1Setup] 无法加载 Sprite：" + spritePath);
                    continue;
                }
                img.sprite = spr;
                EditorUtility.SetDirty(img);
                fixedCount++;
            }
            return fixedCount;
        }
    }
}
