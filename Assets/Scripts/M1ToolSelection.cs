using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace M1
{
    /// <summary>
    /// M1-1/M1-2 探测仪器选择交互。挂在场景 "画板" 上，运行时空自动按物体名解析，
    /// 无需在 Inspector 手工拖引用。
    ///
    /// M1-1（规则已与用户确认）：
    ///  - 正确工具：手推式钢轨探伤仪
    ///  - 选错：AI 回答框显示 "选择错了，请重新选择"，错误工具不抖动
    ///  - 选对：正确图标抖动，AI 回答框显示 "选择正确"，锁定全部工具按钮，显示"点击继续"
    ///  - 点击"点击继续"：进入 M1-2（隐藏工具容器，显示探头容器）
    ///
    /// M1-2（规格书 3.1.2）：
    ///  - 探头按钮：K1/K2.5/K3/0度（M2物品 容器），正确探头 K2.5
    ///  - 点对：抖动 + 正确音效 + "选择正确！"，显示"开始探测"
    ///  - 点错：抖动 + 错误音效 + "请选择K2.5探头"
    ///  - 防卡死：probeIdleTimeout 秒无操作自动高亮 K2.5 并完成选择
    ///  - "开始探测"：按 nextSceneName 配置加载 M2 场景（空则保持占位，不跳转）
    /// </summary>
    public class M1ToolSelection : MonoBehaviour
    {
        [Header("场景解析路径（相对本物体）")]
        [Tooltip("M1-1 工具按钮所在容器")]
        public string toolsRootPath = "白板背景/M1物品";
        [Tooltip("AI 回答文本框")]
        public string aiAnswerPath = "白板背景/数字人/对话框/AI回答";
        [Tooltip("点击继续按钮（M1-1 选对后显示）")]
        public string continueButtonPath = "点击继续";

        [Header("M1-1 工具选择（防卡死）")]
        [Tooltip("工具选择无操作自动完成秒数（防卡死，0=关闭；完成后自动进入 M1-2）")]
        public float toolIdleTimeout = 20f;

        [Header("M1-2 探头选择（阶段切换）")]
        [Tooltip("M1-1 工具容器（进入 M1-2 时隐藏）")]
        public string m1ItemsPath = "白板背景/M1物品";
        [Tooltip("M1-2 探头容器（进入 M1-2 时显示）")]
        public string m2ItemsPath = "白板背景/M2物品";
        [Tooltip("探头按钮物体名（按名称匹配容器内物体）")]
        public string[] probeNames = { "K2.5", "K3", "K1", "0度" };
        [Tooltip("正确探头物体名")]
        public string correctProbeName = "K2.5";
        [Tooltip("开始探测按钮（M1-2 选对后显示）")]
        public string startButtonPath = "开始探测";
        [Tooltip("点击开始探测后加载的场景名（Inspector 可配置；空则不跳转，保持占位）")]
        public string nextSceneName = "M2";
        [Tooltip("探头选择无操作自动完成秒数（防卡死，0=关闭）")]
        public float probeIdleTimeout = 20f;
        [Tooltip("防卡死自动高亮颜色（金色脉动）")]
        public Color autoHighlightColor = new Color(1f, 0.85f, 0.3f, 1f);

        [Header("判定与文案")]
        [Tooltip("正确工具物体名")]
        public string correctToolName = "超声波焊缝探伤仪";
        public string textInitial = "请选择钢轨探伤工具";
        public string textWrong = "选择错了，请重新选择";
        public string textCorrect = "选择正确";
        [Tooltip("M1-2 初始提示文案")]
        public string textM2Initial = "请选择探头";
        [Tooltip("M1-2 选错提示文案")]
        public string textProbeWrong = "请选择K2.5探头";
        [Tooltip("M1-2 选对提示文案")]
        public string textProbeCorrect = "选择正确！";

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
        [Tooltip("M1-2 点击开始探测进入下一模块时播放的通关音效（2026-08-18 老板定稿：与选择正确音效同素材）")]
        public AudioClip passClip;
        [Tooltip("M1-1 点击继续进入 M1-2 时播放的通关音效（2026-08-18 老板定稿：与选择正确音效同素材）")]
        public AudioClip pass2Clip;
        [Tooltip("所有音效播放音量（2026-08-18 老板要求整体调小）")]
        public float sfxVolume = 0.4f;

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
        private readonly List<Button> _probeButtons = new List<Button>();
        private RectTransform _correctToolRect;
        private RectTransform _correctProbeRect;
        private TextMeshProUGUI _aiAnswer;
        private Button _continueButton;
        private Button _startButton;
        private Transform _m1Items;
        private Transform _m2Items;
        [SerializeField] private AudioSource _audioSource;
        private bool _solved;
        private bool _probeSolved;
        private bool _phase2;
        private Coroutine _toolTimeout;
        private Coroutine _probeTimeout;

        private void Awake()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource != null) _audioSource.spatialBlend = 0f; // 强制 2D：画板为 UI 场景，避免 3D 距离衰减导致听不见

            // 初始阶段：M1-1 可见、M1-2 隐藏（运行时兜底，与 Setup 幂等状态一致）
            _m1Items = FindDeep(transform, m1ItemsPath);
            _m2Items = FindDeep(transform, m2ItemsPath);
            if (_m1Items != null) _m1Items.gameObject.SetActive(true);
            if (_m2Items != null) _m2Items.gameObject.SetActive(false);

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

            // M1-2 探头按钮绑定（容器初始隐藏，Transform 遍历不受影响）
            if (_m2Items != null)
            {
                foreach (var probeName in probeNames)
                {
                    var child = FindChildByName(_m2Items, probeName);
                    if (child == null)
                    {
                        Debug.LogWarning("[M1ToolSelection] 未找到探头物体：" + probeName);
                        continue;
                    }
                    var btn = child.GetComponent<Button>();
                    if (btn == null)
                    {
                        Debug.LogWarning("[M1ToolSelection] 探头物体缺少 Button：" + probeName);
                        continue;
                    }
                    _probeButtons.Add(btn);
                    var name = child.name; // 闭包捕获
                    btn.onClick.AddListener(() => OnProbeClicked(name, btn));
                    if (name == correctProbeName)
                        _correctProbeRect = child.GetComponent<RectTransform>();
                }
            }

            // 开始探测按钮（默认隐藏，M1-2 选对后显示）
            var startGo = FindDeep(transform, startButtonPath);
            if (startGo != null) _startButton = startGo.GetComponent<Button>();
            if (_startButton != null)
            {
                _startButton.onClick.RemoveAllListeners(); // 统一收敛到本脚本
                _startButton.onClick.AddListener(OnStartClicked);
                _startButton.gameObject.SetActive(false);
            }

            var contGo = FindDeep(transform, continueButtonPath);
            if (contGo != null) _continueButton = contGo.GetComponent<Button>();
            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveAllListeners(); // 清掉历史持久化占位监听，统一收敛到本脚本
                _continueButton.onClick.AddListener(OnContinueClicked);
            }

            if (_aiAnswer != null) _aiAnswer.text = textInitial;
            if (_continueButton != null) _continueButton.gameObject.SetActive(false);

            // M1-1 防卡死（规格书 3.1.1：20 秒无操作自动选对并进入 M1-2）
            StartToolTimeout();
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

        /// <summary>点击“点击继续”：进入 M1-2 探头选择（隐藏工具容器，显示探头容器）。</summary>
        private void OnContinueClicked()
        {
            if (_phase2) return; // 已进入 M1-2，忽略重复
            _phase2 = true;
            StopToolTimeout();
            PlaySfx(pass2Clip);
            if (_m1Items != null) _m1Items.gameObject.SetActive(false);
            if (_m2Items != null) _m2Items.gameObject.SetActive(true);
            if (_continueButton != null) _continueButton.gameObject.SetActive(false);
            if (_aiAnswer != null) _aiAnswer.text = textM2Initial;
            StartProbeTimeout();
        }

        /// <summary>点击探头：K2.5 正确（抖动+音效+锁定+显示开始探测），其余错误提示。</summary>
        private void OnProbeClicked(string probeName, Button btn)
        {
            if (_probeSolved) return; // 已选对，忽略后续点击

            if (probeName == correctProbeName)
            {
                _probeSolved = true;
                StopProbeTimeout();
                PlaySfx(correctClip);
                if (_aiAnswer != null) _aiAnswer.text = textProbeCorrect;
                if (_correctProbeRect != null)
                    StartCoroutine(Shake(_correctProbeRect, shakeDuration, shakeAmplitude));
                foreach (var b in _probeButtons)
                {
                    if (b != null) b.interactable = false;
                }
                if (_startButton != null) _startButton.gameObject.SetActive(true);
            }
            else
            {
                PlaySfx(wrongClip);
                if (_aiAnswer != null) _aiAnswer.text = textProbeWrong;
                if (btn != null)
                    StartCoroutine(Shake(btn.GetComponent<RectTransform>(), shakeDuration, shakeAmplitude));
            }
        }

        /// <summary>点击“开始探测”：播放通关音效，播完后再加载下一场景（默认 M2）。</summary>
        private void OnStartClicked()
        {
            if (string.IsNullOrEmpty(nextSceneName))
            {
                Debug.Log("[M1-2] 开始探测：nextSceneName 未配置，保持占位不跳转。");
                return;
            }
            Debug.Log("[M1-2] 开始探测：播放通关音效后加载场景 " + nextSceneName);
            PlaySfx(passClip);
            // 同步 LoadScene 会立即销毁当前场景音源，导致通关音效被截断；先等音效播完再切场景。
            StartCoroutine(LoadSceneAfterSfx(passClip != null ? passClip.length : 0f));
        }

        /// <summary>等待通关音效播完再切场景，避免同步 LoadScene 销毁音源截断音效。</summary>
        private System.Collections.IEnumerator LoadSceneAfterSfx(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            SceneManager.LoadScene(nextSceneName);
        }

        private void StartToolTimeout()
        {
            if (toolIdleTimeout <= 0f) return;
            StopToolTimeout();
            _toolTimeout = StartCoroutine(ToolTimeoutFlow());
        }

        private void StopToolTimeout()
        {
            if (_toolTimeout != null)
            {
                StopCoroutine(_toolTimeout);
                _toolTimeout = null;
            }
        }

        /// <summary>M1-1 防卡死：超时无操作则金色脉动高亮正确工具，自动选对并进入 M1-2。</summary>
        private IEnumerator ToolTimeoutFlow()
        {
            yield return new WaitForSeconds(toolIdleTimeout);
            if (_solved || _phase2 || _correctToolRect == null) yield break;
            PulseHighlight(_correctToolRect, () =>
            {
                if (_solved || _phase2) return;
                var btn = _correctToolRect.GetComponent<Button>();
                if (btn != null) OnToolClicked(correctToolName, btn);
                OnContinueClicked(); // 完成选择并进入 M1-2
            });
        }

        private void StartProbeTimeout()
        {
            if (probeIdleTimeout <= 0f || _probeSolved) return;
            StopProbeTimeout();
            _probeTimeout = StartCoroutine(ProbeTimeoutFlow());
        }

        private void StopProbeTimeout()
        {
            if (_probeTimeout != null)
            {
                StopCoroutine(_probeTimeout);
                _probeTimeout = null;
            }
        }

        /// <summary>防卡死：超时无操作则金色脉动高亮正确探头，并自动完成选择。</summary>
        private IEnumerator ProbeTimeoutFlow()
        {
            yield return new WaitForSeconds(probeIdleTimeout);
            if (_probeSolved || _correctProbeRect == null) yield break;
            PulseHighlight(_correctProbeRect, () =>
            {
                if (_probeSolved) return;
                var probeBtn = _correctProbeRect.GetComponent<Button>();
                if (probeBtn != null) OnProbeClicked(correctProbeName, probeBtn);
            });
        }

        /// <summary>金色脉动高亮目标（约 0.9 秒），结束后执行 onDone。</summary>
        private void PulseHighlight(RectTransform target, System.Action onDone)
        {
            if (target == null)
            {
                onDone?.Invoke();
                return;
            }
            StartCoroutine(PulseFlow(target, onDone));
        }

        private IEnumerator PulseFlow(RectTransform target, System.Action onDone)
        {
            var img = target.GetComponent<Image>();
            var original = img != null ? img.color : Color.white;
            var elapsed = 0f;
            while (elapsed < 0.9f)
            {
                elapsed += Time.deltaTime;
                var t = (Mathf.Sin(elapsed * 12f) + 1f) * 0.5f;
                if (img != null) img.color = Color.Lerp(original, autoHighlightColor, t);
                yield return null;
            }
            if (img != null) img.color = original;
            onDone?.Invoke();
        }

        /// <summary>播放音效；素材或 AudioSource 缺失时静默跳过，不报错。统一按 sfxVolume 缩小音量。</summary>
        private void PlaySfx(AudioClip clip)
        {
            if (clip == null || _audioSource == null) return;
            _audioSource.PlayOneShot(clip, sfxVolume);
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

