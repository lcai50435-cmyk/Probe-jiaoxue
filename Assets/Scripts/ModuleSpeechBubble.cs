using System.Collections;
using M1;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M2
{
    /// <summary>
    /// 数字人台词气泡（台词.pptx）：运行时创建云朵气泡 + 居中文本，锚定画布（数字人旁）。
    /// 冻结 Scene 无法建节点，故由各 FlowController 在 Awake 中 AddComponent 挂载（内存态，不写回）。
    /// 通用组件：M2/M3/M4 复用；scale 恒等 1，不会压扁文字；unscaled 计时，问答面板暂停不受影响。
    /// 逐字打字机：typeSpeed>0 时文字逐字出现，期间数字人播说话动画，结束后回待机（M1 不经过本组件，保持现状）。
    /// </summary>
    public class ModuleSpeechBubble : MonoBehaviour
    {
        [Tooltip("气泡尺寸（像素；云朵图 542x479 比例）")]
        public Vector2 bubbleSize = new Vector2(320f, 283f);
        [Tooltip("锚定容器（场景 dialog 等预留节点）；为空则创建为画布子节点。气泡锚定容器中心，位置随容器（老板可调）")]
        public Transform anchor;
        [Tooltip("气泡中心相对锚定容器中心偏移（像素；无 anchor 时相对画布中心）")]
        public Vector2 anchorOffset = new Vector2(-60f, 220f);
        [Tooltip("锚定容器已自带云朵背景（老板创建）：只创建文字，不再新建云朵 Image")]
        public bool useExistingCloud;
        [Tooltip("必须锚定容器才创建（无 anchor 时 Show 静默跳过，不飘字）；等老板添加 dialog 节点后自动生效")]
        public bool createOnlyWhenAnchored;
        [Tooltip("台词字号")]
        public float fontSize = 26f;
        [Tooltip("逐字间隔秒数（0 = 一次性显示；>0 逐字出现并驱动数字人说话动画）")]
        public float typeSpeed = 0.08f; // 老板 2026-08-23：速度调慢（原 0.045）
        [Tooltip("分段台词每段停留秒数（0 = 不自动切段）")]
        public float segmentInterval = 6f;
        [Tooltip("文字左右内缩（像素）")]
        public float paddingX = 30f;
        [Tooltip("文字上下内缩（像素）")]
        public float paddingY = 22f;

        private RectTransform _rt;
        private TextMeshProUGUI _text;
        private TMP_FontAsset _font;
        private Coroutine _typing, _segments;
        private M1DigitalHumanPresenter _presenter;
        private bool _locked; // 分段台词一体播放中：忽略其他 Show（老板：播完前不插其他话）
        private static Sprite _cloudSprite;

        /// <summary>分段台词是否播放中（一体锁定：其他台词暂不显示）。</summary>
        public bool Busy => _locked;

        /// <summary>惰性创建气泡 UI（首次 Show 时；字体为空则取场景任意 TMP 字体）。</summary>
        private void EnsureCreated()
        {
            if (_rt != null) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            var go = new GameObject("~SpeechBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.hideFlags = HideFlags.DontSave;
            var parent = anchor != null ? anchor : canvas.transform;
            go.transform.SetParent(parent, false);
            _rt = (RectTransform)go.transform;
            _rt.anchorMin = _rt.anchorMax = new Vector2(.5f, .5f);
            _rt.pivot = new Vector2(.5f, .5f);
            _rt.sizeDelta = bubbleSize;
            _rt.anchoredPosition = anchorOffset;
            _rt.SetAsLastSibling();

            if (_cloudSprite == null)
            {
                var all = Resources.LoadAll<Sprite>("DigitalHuman/dialog");
                if (all != null && all.Length > 0) _cloudSprite = all[0];
            }
            var img = go.GetComponent<Image>();
            img.sprite = _cloudSprite;
            img.type = Image.Type.Simple;
            img.raycastTarget = false;

            var tgo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            tgo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)tgo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(paddingX, paddingY);
            trt.offsetMax = new Vector2(-paddingX, -paddingY);
            _text = tgo.GetComponent<TextMeshProUGUI>();
            InitText();
        }

        /// <summary>场景已自带云朵背景（老板创建）：只创建文字，锚定容器内指定位置（对齐云朵中心）。</summary>
        private void EnsureTextOnly()
        {
            if (_rt != null) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var parent = anchor != null ? anchor : canvas.transform;

            var tgo = new GameObject("~SpeechText", typeof(RectTransform), typeof(TextMeshProUGUI));
            tgo.hideFlags = HideFlags.DontSave;
            tgo.transform.SetParent(parent, false);
            _rt = (RectTransform)tgo.transform;
            _rt.anchorMin = _rt.anchorMax = new Vector2(.5f, .5f);
            _rt.pivot = new Vector2(.5f, .5f);
            _rt.anchoredPosition = anchorOffset;
            _rt.sizeDelta = bubbleSize;
            _rt.SetAsLastSibling();
            _text = tgo.GetComponent<TextMeshProUGUI>();
            InitText();
        }

        private void InitText()
        {
            if (_font == null)
            {
                var any = Object.FindFirstObjectByType<TextMeshProUGUI>();
                if (any != null) _font = any.font;
            }
            _text.font = _font;
            _text.fontSize = fontSize;
            _text.color = new Color(.1f, .16f, .26f, 1f); // 深蓝灰（云朵浅蓝底）
            _text.alignment = TextAlignmentOptions.Center;
            _text.textWrappingMode = TextWrappingModes.Normal;
        }

        public void SetFont(TMP_FontAsset font) { _font = font; if (_text != null) _text.font = font; }

        /// <summary>锚定到指定容器（如场景 dialog 节点）；未创建 UI 时生效，已创建则忽略（先 Show 前调用）。</summary>
        public void SetAnchor(Transform target) { if (_rt == null) anchor = target; }

        /// <summary>显示单条台词（替换旧内容；逐字出现，期间数字人说话动画，完后回待机；常驻直到下一条或 Hide）。
        /// 分段一体播放中（Busy）忽略调用，播完后再显示（老板 2026-08-23）。</summary>
        public void Show(string text)
        {
            if (_locked) return;
            if (createOnlyWhenAnchored && anchor == null) return; // 云朵节点未就位：静默跳过
            if (useExistingCloud) EnsureTextOnly(); else EnsureCreated();
            if (_rt == null || _text == null) return;
            StopTyping(); StopSegments();
            _text.text = string.Empty;
            _rt.gameObject.SetActive(true);
            var full = text ?? string.Empty;
            if (typeSpeed <= 0f || full.Length == 0) { _text.text = full; return; }
            _typing = StartCoroutine(TypeFlow(full));
        }

        /// <summary>分段展示长台词（PPT：一段一段展示；每段逐字，段间 unscaled 停顿；一体播放中锁定，播完前其他台词不插入）。</summary>
        public void ShowSegments(params string[] segments)
        {
            if (_locked || segments == null || segments.Length == 0) { if (segments != null && segments.Length > 0) Show(string.Empty); return; }
            if (createOnlyWhenAnchored && anchor == null) return;
            if (useExistingCloud) EnsureTextOnly(); else EnsureCreated();
            if (_rt == null || _text == null) return;
            StopTyping(); StopSegments();
            _text.text = string.Empty;
            _rt.gameObject.SetActive(true);
            _locked = true; // 一体播放：锁定直到全部段播完
            _segments = StartCoroutine(SegmentFlow(segments));
        }

        private IEnumerator TypeFlow(string full)
        {
            SetSpeaking(true); // 逐字期间：数字人说话动画
            for (var i = 1; i <= full.Length; i++)
            {
                if (_text != null) _text.text = full.Substring(0, i);
                yield return new WaitForSecondsRealtime(typeSpeed);
            }
            SetSpeaking(false); // 打完：回待机
            _typing = null;
        }

        private IEnumerator SegmentFlow(string[] segments)
        {
            SetSpeaking(true);
            for (var si = 0; si < segments.Length; si++)
            {
                if (_text != null) _text.text = string.Empty;
                if (typeSpeed <= 0f)
                {
                    if (_text != null) _text.text = segments[si];
                }
                else
                {
                    for (var i = 1; i <= segments[si].Length; i++)
                    {
                        if (_text != null) _text.text = segments[si].Substring(0, i);
                        yield return new WaitForSecondsRealtime(typeSpeed);
                    }
                }
                if (si < segments.Length - 1) yield return new WaitForSecondsRealtime(segmentInterval);
            }
            SetSpeaking(false);
            _locked = false; // 全部播完：解锁
            _segments = null;
        }

        /// <summary>驱动数字人说话/待机动画（M2-M5 云朵台词用；找不到 Presenter 则跳过）。</summary>
        private void SetSpeaking(bool on)
        {
            if (_presenter == null) _presenter = Object.FindFirstObjectByType<M1DigitalHumanPresenter>();
            if (_presenter != null) _presenter.SetSpeechState(on);
        }

        public void Hide()
        {
            StopTyping(); StopSegments();
            SetSpeaking(false);
            if (_rt != null) _rt.gameObject.SetActive(false);
        }

        private void StopTyping() { if (_typing != null) { StopCoroutine(_typing); _typing = null; } SetSpeaking(false); }
        private void StopSegments() { if (_segments != null) { StopCoroutine(_segments); _segments = null; } _locked = false; }
    }
}
