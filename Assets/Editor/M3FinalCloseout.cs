using System;
using System.IO;
using System.Security.Cryptography;
using M2;
using M3;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace M3.EditorTools
{
    /// <summary>M3 一次性收口：补角度 Slider、Probe/Ruler 的 bg 子节点、完成面板与当前距离文本，
    /// 挂载 M3 runtime 组件并配置引用，保存后记录前后 SHA-256。只操作 M3 Scene，不碰 M1/M2。</summary>
    public static class M3FinalCloseout
    {
        private const string ScenePath = "Assets/Settings/Scenes/M3.unity";
        private const string FontAssetPath = "Assets/font/sarasa-gothic-sc-regular/sarasa-gothic-sc-regular_cn.asset";
        private const string BeepClipPath = "Assets/Audio/E-03 蜂鸣报警音/蜂鸣报警音.mp3";
        private const string CorrectClipPath = "Assets/Audio/E-01 正确提示音/正确音2.mp3";

        [MenuItem("Tools/M3/Final Closeout（补节点并冻结）")]
        public static void Run()
        {
            var before = Sha256(ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject canvas = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Canvas") { canvas = root; break; }
                canvas = FindDeep(root, "Canvas");
                if (canvas != null) break;
            }
            if (canvas == null) { Debug.LogError("[M3FinalCloseout] 未找到 Canvas，中止。"); return; }

            EnsureAngleSlider(canvas);
            EnsureProbeBg(canvas);
            EnsureRulerBgAndHome(canvas);
            var completionPanel = EnsureCompletionPanel(canvas);
            var currentDistanceText = EnsureCurrentDistanceText(canvas);
            var helpPanel = EnsureHelpPanel(canvas);
            EnsureRaycastTargets(canvas);
            MountAndWire(canvas, completionPanel, currentDistanceText, helpPanel);

            EditorSceneManager.SaveScene(scene);
            var after = Sha256(ScenePath);
            Debug.Log($"[M3FinalCloseout] 完成。before={before}");
            Debug.Log($"[M3FinalCloseout] 完成。after ={after}");
        }

        private static void EnsureAngleSlider(GameObject root)
        {
            var track = FindDeep(root, "AngleTrack");
            if (track == null) { Debug.LogError("[M3FinalCloseout] 未找到 AngleTrack。"); return; }
            var slider = track.GetComponent<Slider>();
            if (slider == null) slider = track.AddComponent<Slider>();
            slider.minValue = 0f; slider.maxValue = 20f; slider.value = 0f;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = FindDeep(track, "Fill")?.transform as RectTransform;
            slider.handleRect = FindDeep(track, "Handle")?.transform as RectTransform;
            slider.targetGraphic = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
            EditorUtility.SetDirty(track);
        }

        private static void EnsureBgChild(GameObject node)
        {
            var bg = node.transform.Find("bg");
            if (bg == null)
            {
                var go = new GameObject("bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(node.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var src = node.GetComponent<Image>();
                var dst = go.GetComponent<Image>();
                if (src != null)
                {
                    dst.sprite = src.sprite; dst.color = src.color; dst.type = src.type;
                    dst.preserveAspect = src.preserveAspect; dst.raycastTarget = src.raycastTarget;
                    UnityEngine.Object.DestroyImmediate(src);
                }
            }
            EditorUtility.SetDirty(node);
        }

        private static void EnsureProbeBg(GameObject root)
        {
            var probe = FindDeep(root, "Probe");
            if (probe == null) { Debug.LogError("[M3FinalCloseout] 未找到 Probe。"); return; }
            EnsureBgChild(probe);
        }

        private static void EnsureRulerBgAndHome(GameObject root)
        {
            var ruler = FindDeep(root, "Ruler");
            var home = FindDeep(root, "RulerHome");
            if (ruler == null || home == null) { Debug.LogError("[M3FinalCloseout] 未找到 Ruler/RulerHome。"); return; }
            EnsureBgChild(ruler);
            var rt = ruler.transform as RectTransform;
            var homeRt = home.transform as RectTransform;
            if (rt.parent != homeRt)
            {
                rt.SetParent(homeRt, false);
                rt.anchorMin = new Vector2(.5f, .5f); rt.anchorMax = new Vector2(.5f, .5f);
                rt.pivot = new Vector2(.5f, .5f); rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(150f, 32f);
            }
            var bgImg = ruler.transform.Find("bg")?.GetComponent<Image>();
            if (bgImg != null) bgImg.color = new Color(.55f, .57f, .6f, .62f);
            EditorUtility.SetDirty(ruler);
        }

        private static GameObject EnsureCompletionPanel(GameObject root)
        {
            var dock = FindDeep(root, "ControlDock_D");
            if (dock == null) { Debug.LogError("[M3FinalCloseout] 未找到 ControlDock_D。"); return null; }
            var panel = dock.transform.Find("CompletionPanel");
            if (panel != null) return panel.gameObject;
            var go = new GameObject("CompletionPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(dock.transform, false);
            var prt = go.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(.97f, .98f, .985f, 1f);

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            var textGo = new GameObject("CompletionText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, .5f); trt.anchorMax = new Vector2(1f, .5f);
            trt.offsetMin = new Vector2(24f, 0f); trt.offsetMax = new Vector2(-360f, 0f);
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text = "下一模块待接入"; tmp.fontSize = 32; tmp.alignment = TextAlignmentOptions.MidlineLeft; tmp.color = new Color(.12f, .15f, .18f); tmp.font = font;

            var btnGo = new GameObject("EnterNextButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(go.transform, false);
            var brt = btnGo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(1f, .5f); brt.anchorMax = new Vector2(1f, .5f); brt.pivot = new Vector2(1f, .5f);
            brt.anchoredPosition = new Vector2(-24f, 0f); brt.sizeDelta = new Vector2(240f, 76f);
            var bimg = btnGo.GetComponent<Image>(); bimg.color = new Color(.08f, .42f, .66f);
            btnGo.GetComponent<Button>().targetGraphic = bimg;
            var btxt = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            btxt.transform.SetParent(btnGo.transform, false);
            var trt2 = btxt.GetComponent<RectTransform>();
            trt2.anchorMin = Vector2.zero; trt2.anchorMax = Vector2.one; trt2.offsetMin = trt2.offsetMax = Vector2.zero;
            var t2 = btxt.GetComponent<TextMeshProUGUI>(); t2.text = "进入下一模块"; t2.fontSize = 30; t2.alignment = TextAlignmentOptions.Center; t2.color = Color.white; t2.font = font;
            go.SetActive(false);
            EditorUtility.SetDirty(dock);
            return go;
        }

        private static TMP_Text EnsureCurrentDistanceText(GameObject root)
        {
            var header = FindDeep(root, "WaveHeader");
            if (header == null) { Debug.LogError("[M3FinalCloseout] 未找到 WaveHeader。"); return null; }
            var existing = header.transform.Find("CurrentDistance");
            if (existing != null) return existing.GetComponent<TMP_Text>();
            var go = new GameObject("CurrentDistance", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(header.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, .5f); rt.anchorMax = new Vector2(1f, .5f); rt.pivot = new Vector2(1f, .5f);
            rt.anchoredPosition = new Vector2(-12f, 0f);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = "150mm"; tmp.fontSize = 28; tmp.alignment = TextAlignmentOptions.Right; tmp.color = new Color(.34f, .92f, .62f);
            tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            EditorUtility.SetDirty(header);
            return tmp;
        }

        private static GameObject EnsureHelpPanel(GameObject root)
        {
            var dock = FindDeep(root, "ControlDock_D");
            if (dock == null) return null;
            var panel = dock.transform.Find("HelpPanel");
            if (panel != null) return panel.gameObject;
            var go = new GameObject("HelpPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(dock.transform, false);
            var prt = go.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(.97f, .98f, .985f, .95f);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            var textGo = new GameObject("HelpText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, .5f); trt.anchorMax = new Vector2(1f, .5f);
            trt.offsetMin = new Vector2(24f, 0f); trt.offsetMax = new Vector2(-560f, 0f);
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text = "需要帮助吗？"; tmp.fontSize = 30; tmp.alignment = TextAlignmentOptions.MidlineLeft; tmp.color = new Color(.12f, .15f, .18f); tmp.font = font;
            var autoBtn = new GameObject("AutoDemoButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            autoBtn.transform.SetParent(go.transform, false);
            var ab = autoBtn.GetComponent<RectTransform>();
            ab.anchorMin = new Vector2(1f, .5f); ab.anchorMax = new Vector2(1f, .5f); ab.pivot = new Vector2(1f, .5f);
            ab.anchoredPosition = new Vector2(-300f, 0f); ab.sizeDelta = new Vector2(160f, 60f);
            var abImg = autoBtn.GetComponent<Image>(); abImg.color = new Color(.08f, .42f, .66f);
            autoBtn.GetComponent<Button>().targetGraphic = abImg;
            var abText = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            abText.transform.SetParent(autoBtn.transform, false);
            var abt = abText.GetComponent<RectTransform>(); abt.anchorMin = Vector2.zero; abt.anchorMax = Vector2.one; abt.offsetMin = abt.offsetMax = Vector2.zero;
            var abTmp = abText.GetComponent<TextMeshProUGUI>(); abTmp.text = "自动演示"; abTmp.fontSize = 26; abTmp.alignment = TextAlignmentOptions.Center; abTmp.color = Color.white; abTmp.font = font;
            var tryBtn = new GameObject("TryAgainButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            tryBtn.transform.SetParent(go.transform, false);
            var tb = tryBtn.GetComponent<RectTransform>();
            tb.anchorMin = new Vector2(1f, .5f); tb.anchorMax = new Vector2(1f, .5f); tb.pivot = new Vector2(1f, .5f);
            tb.anchoredPosition = new Vector2(-120f, 0f); tb.sizeDelta = new Vector2(120f, 60f);
            var tbImg = tryBtn.GetComponent<Image>(); tbImg.color = new Color(.58f, .61f, .65f);
            tryBtn.GetComponent<Button>().targetGraphic = tbImg;
            var tbText = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            tbText.transform.SetParent(tryBtn.transform, false);
            var tbt = tbText.GetComponent<RectTransform>(); tbt.anchorMin = Vector2.zero; tbt.anchorMax = Vector2.one; tbt.offsetMin = tbt.offsetMax = Vector2.zero;
            var tbTmp = tbText.GetComponent<TextMeshProUGUI>(); tbTmp.text = "再试试"; tbTmp.fontSize = 26; tbTmp.alignment = TextAlignmentOptions.Center; tbTmp.color = Color.white; tbTmp.font = font;
            go.SetActive(false);
            EditorUtility.SetDirty(dock);
            return go;
        }

        private static void MountAndWire(GameObject canvas, GameObject completionPanel, TMP_Text currentDistanceText, GameObject helpPanel)
        {
            var safeArea = FindDeep(canvas, "SafeArea");
            var mount = safeArea != null ? safeArea : canvas;
            var flow = EnsureComponent<M3FlowController>(mount);
            var idle = EnsureComponent<M3IdleHelp>(mount);
            var audio = EnsureComponent<AudioSource>(mount);
            if (audio != null) { audio.playOnAwake = false; audio.spatialBlend = 0f; }

            var probe = FindDeep(canvas, "Probe");
            var ruler = FindDeep(canvas, "Ruler");
            var waveGrid = FindDeep(canvas, "WaveGrid");
            var probeDrag = probe != null ? EnsureComponent<M3ProbeDrag>(probe) : null;
            var rulerDrag = ruler != null ? EnsureComponent<M3RulerDrag>(ruler) : null;
            var waveform = waveGrid != null ? EnsureComponent<M2WaveformFx>(waveGrid) : null;

            flow.probeDrag = probeDrag;
            flow.rulerDrag = rulerDrag;
            flow.waveformFx = waveform;
            flow.beamLayer = FindDeep(canvas, "BeamLayer");
            flow.railPerspective = FindDeep(canvas, "RailPerspective");
            flow.damageMarker = FindDeep(canvas, "DamageMarker");
            flow.detectionBanner = FindDeep(canvas, "DetectionBanner");
            flow.completionPanel = completionPanel;
            flow.measurementBubble = FindDeep(canvas, "MeasurementBubble");
            flow.couplantOverlay = FindDeep(canvas, "CouplantOverlay")?.GetComponent<RectTransform>();
            flow.railBg = FindDeep(canvas, "RailNormal")?.GetComponent<RectTransform>();
            flow.normalBtnImg = FindDeep(canvas, "NormalButton")?.GetComponent<Image>();
            flow.perspectiveBtnImg = FindDeep(canvas, "PerspectiveButton")?.GetComponent<Image>();
            flow.instructionText = FindDeep(canvas, "Hint")?.GetComponent<TMP_Text>();
            flow.stepProgressText = FindDeep(canvas, "StepProgress")?.GetComponentInChildren<TMP_Text>();
            flow.completionText = completionPanel != null ? completionPanel.transform.Find("CompletionText")?.GetComponent<TMP_Text>() : null;
            flow.resetButton = FindDeep(canvas, "ResetButton")?.GetComponent<Button>();
            flow.enterNextButton = completionPanel != null ? completionPanel.transform.Find("EnterNextButton")?.GetComponent<Button>() : null;
            flow.sfx = audio;
            flow.beepClip = LoadClip(BeepClipPath);
            flow.correctClip = LoadClip(CorrectClipPath);
            flow.idleHelp = idle;

            if (probeDrag != null)
            {
                probeDrag.probeRt = probe.GetComponent<RectTransform>();
                probeDrag.probeVisual = probe.transform.Find("bg") as RectTransform;
                probeDrag.railViewport = FindDeep(canvas, "RailViewport")?.transform as RectTransform;
                probeDrag.beamLine = FindDeep(canvas, "IncidentBeam")?.transform as RectTransform;
                probeDrag.angleSlider = FindDeep(canvas, "AngleTrack")?.GetComponent<Slider>();
                probeDrag.angleValueText = FindDeep(canvas, "AngleValue")?.GetComponent<TMP_Text>();
                probeDrag.angleStatusText = FindDeep(canvas, "AngleLabel")?.GetComponent<TMP_Text>();
            }
            if (rulerDrag != null)
            {
                rulerDrag.rulerRt = ruler.GetComponent<RectTransform>();
                rulerDrag.railViewport = FindDeep(canvas, "RailViewport")?.transform as RectTransform;
                rulerDrag.weldLineRt = FindDeep(canvas, "WeldLine")?.transform as RectTransform;
                rulerDrag.rulerHome = FindDeep(canvas, "RulerHome")?.transform as RectTransform;
                rulerDrag.rulerImage = ruler.transform.Find("bg")?.GetComponent<Image>();
            }
            idle.flow = flow;
            idle.probeDrag = probeDrag;
            idle.helpPanel = helpPanel;
            idle.helpText = helpPanel != null ? helpPanel.transform.Find("HelpText")?.GetComponent<TMP_Text>() : null;
            idle.autoDemoButton = helpPanel != null ? helpPanel.transform.Find("AutoDemoButton")?.GetComponent<Button>() : null;
            idle.tryAgainButton = helpPanel != null ? helpPanel.transform.Find("TryAgainButton")?.GetComponent<Button>() : null;

            if (waveform != null)
            {
                waveform.scanMinMm = 0f; waveform.scanMaxMm = 200f;
                waveform.appearMm = 160f; waveform.peakMm = 123f; waveform.stopMm = 120f;
            }
            EditorUtility.SetDirty(mount);
        }

        private static void EnsureRaycastTargets(GameObject root)
        {
            foreach (var btn in root.GetComponentsInChildren<Button>(true))
            {
                var img = btn.targetGraphic as Image;
                if (img != null) { img.raycastTarget = true; EditorUtility.SetDirty(img.gameObject); }
            }
            foreach (var slider in root.GetComponentsInChildren<Slider>(true))
            {
                if (slider.handleRect != null) { var h = slider.handleRect.GetComponent<Image>(); if (h != null) h.raycastTarget = true; }
            }
        }

        private static AudioClip LoadClip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }
        private static GameObject FindDeep(GameObject root, string name)
        {
            foreach (Transform child in root.transform)
            {
                if (child.name == name) return child.gameObject;
                var hit = FindDeep(child.gameObject, name); if (hit != null) return hit;
            }
            return null;
        }
        private static string Sha256(string path)
        {
            try { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
            catch (Exception e) { Debug.LogError("[M3FinalCloseout] 读取失败：" + e.Message); return "<unavailable>"; }
        }
    }
}
