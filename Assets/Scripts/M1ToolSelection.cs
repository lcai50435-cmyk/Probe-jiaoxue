using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace M1
{
    /// <summary>
    /// M1-1 探伤工具选择交互。
    /// 挂到场景 "画板" 上。运行时空自动按物体名解析 6 个工具按钮与 AI 回答文本框，
    /// 无需在 Inspector 手工拖引用。
    ///
    /// 规则（已与用户确认）：
    ///  - 正确工具：手推式钢轨探伤仪
    ///  - 选错：AI 回答框显示 "选择错了，请重新选择"，错误工具不抖动
    ///  - 选对：正确图标抖动，AI 回答框显示 "选择正确"，锁定全部工具按钮，显示"点击继续"占位按钮
    ///  - "点击继续" 仅占位，不跳转（M1-2 后续实现）
    /// </summary>
    public class M1ToolSelection : MonoBehaviour
    {
        [Header("场景解析路径（相对本物体）")]
        [Tooltip("工具按钮所在容器")]
        public string toolsRootPath = "白板背景/物品";
        [Tooltip("AI 回答文本框")]
        public string aiAnswerPath = "白板背景/数字人/对话框/AI回答";
        [Tooltip("点击继续按钮（占位，选对后显示）")]
        public string continueButtonPath = "点击继续";

        [Header("判定与文案")]
        [Tooltip("正确工具物体名")]
        public string correctToolName = "手推式钢轨探伤仪";
        public string textInitial = "请选择钢轨探伤工具";
        public string textWrong = "选择错了，请重新选择";
        public string textCorrect = "选择正确";

        [Header("抖动参数")]
        [Tooltip("抖动时长（秒）")]
        public float shakeDuration = 0.4f;
        [Tooltip("抖动幅度（像素）")]
        public float shakeAmplitude = 12f;

        [Header("点击音效（可留空，留空则静默跳过）")]
        [Tooltip("选对工具时播放的正确提示音")]
        public AudioClip correctClip;
        [Tooltip("选错工具时播放的错误提示音")]
        public AudioClip wrongClip;
        [Tooltip("点击继续时播放的通关音效")]
        public AudioClip passClip;

        private static readonly string[] ToolNames =
        {
            "超声波焊缝探伤仪",
            "手推式钢轨探伤仪",
            "双轨式探伤仪",
            "轨距尺",
            "钢轨打磨机",
            "内燃威客镐"
        };

        private readonly List<Button> _toolButtons = new List<Button>();
        private RectTransform _correctToolRect;
        private TextMeshProUGUI _aiAnswer;
        private Button _continueButton;
        [SerializeField] private AudioSource _audioSource;
        private bool _solved;

        private void Awake()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource != null) _audioSource.spatialBlend = 0f; // 强制 2D：画板为 UI 场景，避免 3D 距离衰减导致听不见

            var toolsRoot = FindDeep(transform, toolsRootPath);
            if (toolsRoot == null)
            {
                Debug.LogError("[M1ToolSelection] 未找到工具容器：" + toolsRootPath);
            }
            else
            {
                foreach (var toolName in ToolNames)
                {
                    var child = FindDeep(toolsRoot, toolName);
                    if (child == null)
                    {
                        Debug.LogWarning("[M1ToolSelection] 未找到工具物体：" + toolName);
                        continue;
                    }
                    var btn = child.GetComponent<Button>();
                    if (btn == null)
                    {
                        Debug.LogWarning("[M1ToolSelection] 工具物体缺少 Button：" + toolName);
                        continue;
                    }
                    _toolButtons.Add(btn);
                    var name = child.name; // 闭包捕获
                    btn.onClick.AddListener(() => OnToolClicked(name, btn));
                    if (toolName == correctToolName)
                        _correctToolRect = child.GetComponent<RectTransform>();
                }
            }

            var aiGo = FindDeep(transform, aiAnswerPath);
            if (aiGo == null)
            {
                // 兼容旧路径：按名称在整个画板下查找
                aiGo = FindChildByName(transform, "AI回答");
                if (aiGo != null)
                    Debug.LogWarning("[M1ToolSelection] aiAnswerPath 未命中，已按名称找到 AI 回答文本框：" + aiAnswerPath);
            }
            if (aiGo != null) _aiAnswer = aiGo.GetComponent<TextMeshProUGUI>();
            if (_aiAnswer == null)
                Debug.LogError("[M1ToolSelection] 未找到 AI 回答文本框：" + aiAnswerPath);

            var contGo = FindDeep(transform, continueButtonPath);
            if (contGo != null) _continueButton = contGo.GetComponent<Button>();
            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveAllListeners(); // 清掉历史持久化占位监听，统一收敛到本脚本
                _continueButton.onClick.AddListener(OnContinueClicked);
            }

            if (_aiAnswer != null) _aiAnswer.text = textInitial;
            if (_continueButton != null) _continueButton.gameObject.SetActive(false);
        }

        private void OnToolClicked(string toolName, Button btn)
        {
            if (_solved) return; // 已选对，忽略后续点击

            if (toolName == correctToolName)
            {
                _solved = true;
                PlaySfx(correctClip);
                if (_aiAnswer != null) _aiAnswer.text = textCorrect;
                if (_correctToolRect != null)
                    StartCoroutine(Shake(_correctToolRect, shakeDuration, shakeAmplitude));
                foreach (var b in _toolButtons)
                {
                    if (b != null) b.interactable = false;
                }
                if (_continueButton != null) _continueButton.gameObject.SetActive(true);
            }
            else
            {
                PlaySfx(wrongClip);
                if (_aiAnswer != null) _aiAnswer.text = textWrong;
            }
        }

        /// <summary>点击“点击继续”：播放通关音效（M1-2 跳转后续实现，当前仅占位）。</summary>
        private void OnContinueClicked()
        {
            PlaySfx(passClip);
            Debug.Log("[M1-1] 点击继续：M1-2 尚未实现（占位）。");
        }

        /// <summary>播放音效；素材或 AudioSource 缺失时静默跳过，不报错。</summary>
        private void PlaySfx(AudioClip clip)
        {
            if (clip == null || _audioSource == null) return;
            _audioSource.PlayOneShot(clip);
        }

        private static IEnumerator Shake(RectTransform rt, float duration, float amplitude)
        {
            if (rt == null) yield break;
            var original = rt.anchoredPosition;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = 1f - Mathf.Clamp01(elapsed / duration); // 衰减
                var ox = (Random.value * 2f - 1f) * amplitude * t;
                var oy = (Random.value * 2f - 1f) * amplitude * t;
                rt.anchoredPosition = original + new Vector2(ox, oy);
                yield return null;
            }
            rt.anchoredPosition = original;
        }

        /// <summary>递归查找子物体（包含未激活的物体）。</summary>
        private static Transform FindDeep(Transform root, string pathOrName)
        {
            if (root == null) return null;
            if (pathOrName.Contains("/"))
            {
                var parts = pathOrName.Split('/');
                var cur = root;
                foreach (var p in parts)
                {
                    cur = FindChildByName(cur, p);
                    if (cur == null) return null;
                }
                return cur;
            }
            return FindChildByName(root, pathOrName);
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var hit = FindChildByName(child, name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}

