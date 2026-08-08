using System.IO;
using M1;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

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
        private const string M1ScenePath = "Assets/Settings/Scenes/M1.unity";
        private const string ContinueButtonName = "点击继续";
        private const string FontAssetPath =
            "Assets/font/sarasa-gothic-sc-regular/sarasa-gothic-sc-regular_cn.asset";

        // 开场引导动画：webm 优先（需求指定），导入失败自动兜底 mp4（同一视频，编码 H.264 更稳）
        private const string IntroCanvasName = "引导遮罩";
        private const string IntroDimName = "半黑遮罩";
        private const string IntroVideoName = "引导视频";
        private const string IntroSkipName = "跳过引导";
        private const string IntroBasePath =
            "Assets/DigitalHuman/A-04 引导动画/引导动画-1/引导动画-1（有音轨版）/引导动画-1（有音轨版）";
        private const float IntroDimAlpha = 0.8f;          // 半黑遮罩黑度（与视频纯黑底视觉融合）
        private const float IntroVideoAspect = 1080f / 1450f; // 竖屏 1080x1450，方案 A：高度适配居中
        private const string LumaKeyMatPath = "Assets/Shaders/UI-LumaKey.mat";

        /// <summary>命令行/批处理入口：打开 M1 场景后执行 Setup（供 CI 与无人值守使用）。</summary>
        public static void SetupM11Batch()
        {
            var scene = EditorSceneManager.OpenScene(M1ScenePath, OpenSceneMode.Single);
            SetupM11();
            Debug.Log("[M1Setup] Batch 完成，场景：" + scene.name);
        }

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

            // 5) 开场引导动画 UI（幂等：已存在则校验/补全引用）
            var intro = EnsureIntro(board, cnFont);

            EditorSceneManager.MarkSceneDirty(scene);
            var saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[M1Setup] 完成：移除缺失脚本 {removed} 个；挂载 {comp.GetType().Name}；" +
                      $"按钮 {button.name} (active={button.activeSelf})；重指向 TMP {repointed} 个；修复图片 {spriteFixed} 个；" +
                      $"引导 {intro}；场景保存={saved}");
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

        /// <summary>
        /// 搭建开场引导动画（挂在画板下的嵌套 Canvas，sortingOrder 100 盖过主 UI）：
        /// 半黑遮罩（全屏、点击跳过）+ 竖屏引导视频（高度适配居中）+ 右上角跳过按钮。
        /// 幂等：已存在则只补全缺失引用（如 VideoClip），不重建对象。
        /// </summary>
        private static string EnsureIntro(GameObject board, TMP_FontAsset font)
        {
            var root = FindIncludingInactive(board.transform, IntroCanvasName);

            // 加载引导视频：webm 优先，导入失败则 mp4 兜底
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(IntroBasePath + ".webm")
                       ?? AssetDatabase.LoadAssetAtPath<VideoClip>(IntroBasePath + ".mp4");
            if (clip == null)
            {
                Debug.LogWarning("[M1Setup] 未找到可用的引导视频 VideoClip（webm/mp4），" +
                                 "请确认 Assets/DigitalHuman/A-04 引导动画 已导入后重新执行 Setup。");
            }

            if (root != null)
            {
                // 幂等路径：修复历史生成的 RectTransform（早期版本未拉伸全屏，导致引导只显示 100x100），
                // 并只补全缺失的 VideoClip / 引用，不重建
                StretchFullScreen(root.GetComponent<RectTransform>());
                var m1 = root.GetComponent<M1IntroVideo>();
                var player = FindIncludingInactive(root, IntroVideoName);
                var vp = player != null ? player.GetComponent<VideoPlayer>() : null;
                if (clip != null && vp != null && vp.clip == null) vp.clip = clip;
                if (m1 != null && clip != null && m1.player != null && m1.player.clip == null) m1.player.clip = clip;
                // 修复材质：引导视频必须用 LumaKey 抠像材质（黑底去除、人物悬空）
                var existingRaw = player != null ? player.GetComponent<RawImage>() : null;
                var lumaMat = EnsureLumaKeyMaterial();
                if (existingRaw != null && lumaMat != null && existingRaw.material != lumaMat) existingRaw.material = lumaMat;
                Debug.Log("[M1Setup] 已存在 " + IntroCanvasName + "，跳过创建（补全引用）。");
                return "已存在";
            }

            // --- 引导遮罩 Canvas（画板的嵌套子 Canvas，靠 sortingOrder 盖过主 UI）---
            var canvasGo = new GameObject(IntroCanvasName, typeof(Canvas), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(board.transform, false);
            StretchFullScreen(canvasGo.GetComponent<RectTransform>()); // 关键：嵌套 Canvas 必须全屏拉伸，否则内容只有 100x100
            canvasGo.GetComponent<Canvas>().sortingOrder = 100;

            // --- 半黑遮罩：全屏 Image，挡点击（游戏 UI 不可交互），点击=跳过（非首次）---
            var dim = new GameObject(IntroDimName, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            dim.transform.SetParent(canvasGo.transform, false);
            StretchFullScreen(dim.GetComponent<RectTransform>());
            var dimImage = dim.GetComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, IntroDimAlpha);
            dimImage.raycastTarget = true;

            // --- 引导视频：RawImage 高度适配居中（方案 A），两侧留黑由遮罩覆盖 ---
            var videoGo = new GameObject(IntroVideoName, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(RawImage), typeof(AspectRatioFitter), typeof(VideoPlayer));
            videoGo.transform.SetParent(canvasGo.transform, false);
            var vrt = videoGo.GetComponent<RectTransform>();
            vrt.anchorMin = new Vector2(0.5f, 0f);
            vrt.anchorMax = new Vector2(0.5f, 1f);
            vrt.pivot = new Vector2(0.5f, 0.5f);
            vrt.anchoredPosition = Vector2.zero;
            vrt.sizeDelta = new Vector2(100f, 0f); // 宽度由 AspectRatioFitter 按高度推算
            var fitter = videoGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            fitter.aspectRatio = IntroVideoAspect;
            var raw = videoGo.GetComponent<RawImage>();
            raw.raycastTarget = false; // 穿透点击到下方遮罩（遮罩负责跳过）
            raw.material = EnsureLumaKeyMaterial(); // 黑底抠像：人物悬空，无视频黑底
            var vp2 = videoGo.GetComponent<VideoPlayer>();
            vp2.clip = clip;
            vp2.playOnAwake = false;
            vp2.isLooping = false;
            vp2.skipOnDrop = true;
            vp2.audioOutputMode = VideoAudioOutputMode.Direct;

            // --- 右上角跳过按钮（首次进入隐藏，由 M1IntroVideo 控制）---
            var skipGo = new GameObject(IntroSkipName, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            skipGo.transform.SetParent(canvasGo.transform, false);
            var srt = skipGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(1f, 1f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(1f, 1f);
            srt.anchoredPosition = new Vector2(-48f, -48f);
            srt.sizeDelta = new Vector2(140f, 60f);
            var sImg = skipGo.GetComponent<Image>();
            sImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            sImg.type = Image.Type.Sliced;
            sImg.color = new Color(0.1f, 0.1f, 0.1f, 0.7f);
            var sBtn = skipGo.GetComponent<Button>();
            sBtn.targetGraphic = sImg;
            var skipText = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            skipText.transform.SetParent(skipGo.transform, false);
            var trt = skipText.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = skipText.GetComponent<TextMeshProUGUI>();
            tmp.text = "跳过";
            tmp.fontSize = 32;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            if (font != null) tmp.font = font;

            // --- 挂载运行时控制器并接线 ---
            var intro = canvasGo.AddComponent<M1IntroVideo>();
            intro.overlay = canvasGo;
            intro.player = vp2;
            intro.videoImage = raw;
            intro.skipButton = sBtn;
            dim.GetComponent<Button>().onClick.AddListener(intro.Skip);
            sBtn.onClick.AddListener(intro.Skip);
            EditorUtility.SetDirty(canvasGo);

            Debug.Log($"[M1Setup] 已创建引导遮罩：视频={(clip != null ? clip.name : "未找到")}，" +
                      $"遮罩黑度={IntroDimAlpha}，方案 A 高度适配居中。");
            return "已创建";
        }

        /// <summary>
        /// 加载/创建黑底抠像材质（UI/LumaKey）。幂等：材质资产已存在则直接加载。
        /// </summary>
        private static Material EnsureLumaKeyMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(LumaKeyMatPath);
            if (mat != null) return mat;
            var shader = Shader.Find("UI/LumaKey");
            if (shader == null)
            {
                Debug.LogError("[M1Setup] 未找到 Shader UI/LumaKey（Assets/Shaders/UI-LumaKey.shader），无法抠像。");
                return null;
            }
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, LumaKeyMatPath);
            Debug.Log("[M1Setup] 已创建黑底抠像材质：" + LumaKeyMatPath);
            return mat;
        }

        /// <summary>RectTransform 铺满父级（全屏）。</summary>
        private static void StretchFullScreen(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
