using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M1
{
    /// <summary>AI 回答生命周期状态（数字人动画组件订阅 OnAnswerStateChanged 切换 思考/说话/待机 动画）。</summary>
    public enum AnswerState
    {
        Idle,      // 无回答进行中（待机）
        Thinking,  // 请求已发出，等待 AI 回复（思考）
        Speaking   // 回复开始逐字显示（说话/讲解）
    }

    /// <summary>
    /// M1 AI 提问面板（原型）：右侧抽屉式。
    ///  - 长按数字人头像（数字人/背景圆/大头）打开
    ///  - 文字输入（最多 200 字）+ 语音按钮占位 + 发送
    ///  - 用户消息左对齐蓝气泡；AI 回复右对齐白气泡 + 逐字显示（真实 DeepSeek 回复）
    ///  - 回答生命周期通过 OnAnswerStateChanged 事件对外发布，供数字人动画消费
    ///  - 挡板点击 / 关闭按钮关闭；打开时挡板锁定底层交互
    /// 挂到场景 "画板" 上，运行时空自动按物体名解析引用。
    /// </summary>
    public class M1QAPanel : MonoBehaviour
    {
        [Header("场景解析路径（相对本物体）")]
        [Tooltip("面板根物体")]
        public string panelPath = "QAPanel";
        [Tooltip("全屏挡板（点击关闭）")]
        public string blockerPath = "Blocker";
        [Tooltip("右上角关闭按钮")]
        public string closeButtonPath = "QAPanel/Header/CloseButton";
        [Tooltip("消息列表 Content")]
        public string messageContentPath = "QAPanel/MessageList/Viewport/Content";
        [Tooltip("文字输入框")]
        public string inputFieldPath = "QAPanel/InputRow/InputField";
        [Tooltip("语音按钮（占位）")]
        public string voiceButtonPath = "QAPanel/InputRow/VoiceButton";
        [Tooltip("发送按钮")]
        public string sendButtonPath = "QAPanel/InputRow/SendButton";
        [Tooltip("字数计数文本")]
        public string counterTextPath = "QAPanel/InputRow/CounterText";
        [Tooltip("长按目标（数字人头像）")]
        public string pressTargetPath = "数字人/背景圆/大头";
        [Tooltip("是否由本组件自动绑定长按入口（Setup 会关闭，改由数字人 Presenter 统一处理两个显示形态的输入）")]
        public bool bindPressTarget = true;

        [Header("AI 服务")]
        [Tooltip("DeepSeek 客户端（由 Setup 注入；未配置时发送给出提示）")]
        public M1DeepSeekClient deepSeekClient;
        [Tooltip("回答状态变化事件：Thinking=等待回复 / Speaking=逐字显示中 / Idle=完成或失败（数字人动画订阅处）")]
        public event Action<AnswerState> OnAnswerStateChanged;
        [Tooltip("面板可见性变化：Open 开始发布 true；完全滑出隐藏后发布 false（数字人恢复形态的时机依据）")]
        public event Action<bool> OnPanelVisibilityChanged;

        [Header("参数")]
        [Tooltip("中文 SDF 字体（由 Setup 注入；为空则从 AI回答 复制）")]
        public TMP_FontAsset cnFont;
        [Tooltip("长按触发时长（秒）")]
        public float holdDuration = 0.5f;
        [Tooltip("输入字数上限")]
        public int maxChars = 200;
        [Tooltip("面板滑入/滑出时长（秒）")]
        public float slideDuration = 0.25f;
        [Tooltip("AI 逐字显示间隔（秒）")]
        public float typeSpeed = 0.035f;
        [Tooltip("面板隐藏时右侧偏移量（像素）")]
        public float hiddenOffsetX = 800f;
        [Tooltip("气泡最大宽度（像素，超出自动换行）")]
        public float bubbleMaxWidth = 480f;
        [Tooltip("面板打开时暂停游戏（Time.timeScale=0，关闭时恢复；数字人视频与面板动画不受影响）")]
        public bool pauseGameOnOpen = true;

        private RectTransform _panelRt;
        private GameObject _blocker;
        private Button _closeButton;
        private RectTransform _messageContent;
        private ScrollRect _scroll;
        private TMP_InputField _input;
        private Button _voiceButton;
        private Button _sendButton;
        private TextMeshProUGUI _counter;
        private M1PressDetector _pressDetector;

        private bool _isOpen;
        private bool _paused;        // 本次打开期间是否已暂停游戏（防重复设置）
        private float _timeScaleBefore = 1f; // 打开前的 timeScale，关闭时原样恢复
        private bool _busy; // 请求等待中或逐字显示中（防重入）
        private Coroutine _typingCoroutine;

        private static readonly Color UserBubbleColor = Color.white; // 微信风格：用户消息浅色气泡黑字（与 AI 同步）
        private static readonly Color AiBubbleColor = Color.white;
        private const float BubblePaddingX = 32f;
        private const float BubblePaddingY = 20f;

        private static Sprite _bubbleSprite;

        /// <summary>气泡九宫格切片图：程序化生成白色圆角（Unity 6 运行时加载不到内置 UI/Skin/UISprite.psd，故自建），静态缓存只生成一次。</summary>
        private static Sprite GetBubbleSprite()
        {
            if (_bubbleSprite != null) return _bubbleSprite;
            const int size = 12;   // 纹理边长
            const int radius = 4;  // 圆角半径（同时作为 9-slice border）
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var cx = x < radius ? radius - x - 0.5f : (x >= size - radius ? x - (size - radius - 0.5f) : 0f);
                    var cy = y < radius ? radius - y - 0.5f : (y >= size - radius ? y - (size - radius - 0.5f) : 0f);
                    var alpha = Mathf.Clamp01(radius - Mathf.Sqrt(cx * cx + cy * cy) + 0.5f); // 边缘 1px 渐变抗锯齿
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            _bubbleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return _bubbleSprite;
        }

        private void Awake()
        {
            // DeepSeek 客户端兜底：Setup 注入优先；缺失时自动挂载，保证开箱即用
            if (deepSeekClient == null)
                deepSeekClient = GetComponent<M1DeepSeekClient>() ?? gameObject.AddComponent<M1DeepSeekClient>();

            var panelGo = FindDeep(transform, panelPath)?.gameObject;
            if (panelGo == null)
            {
                Debug.LogError("[M1QAPanel] 未找到面板：" + panelPath);
                return;
            }
            _panelRt = panelGo.GetComponent<RectTransform>();

            var blockerGo = FindDeep(transform, blockerPath)?.gameObject;
            if (blockerGo == null)
            {
                Debug.LogError("[M1QAPanel] 未找到挡板：" + blockerPath);
                return;
            }
            _blocker = blockerGo;
            var blockerBtn = blockerGo.GetComponent<Button>();
            if (blockerBtn != null) blockerBtn.onClick.AddListener(Close);

            var closeGo = FindDeep(transform, closeButtonPath)?.gameObject;
            if (closeGo != null) _closeButton = closeGo.GetComponent<Button>();

            var msgGo = FindDeep(transform, messageContentPath)?.gameObject;
            if (msgGo != null)
            {
                _messageContent = msgGo.GetComponent<RectTransform>();
                _scroll = msgGo.GetComponentInParent<ScrollRect>();
            }

            var inputGo = FindDeep(transform, inputFieldPath)?.gameObject;
            if (inputGo != null) _input = inputGo.GetComponent<TMP_InputField>();

            var voiceGo = FindDeep(transform, voiceButtonPath)?.gameObject;
            if (voiceGo != null) _voiceButton = voiceGo.GetComponent<Button>();

            var sendGo = FindDeep(transform, sendButtonPath)?.gameObject;
            if (sendGo != null) _sendButton = sendGo.GetComponent<Button>();

            var counterGo = FindDeep(transform, counterTextPath)?.gameObject;
            if (counterGo != null) _counter = counterGo.GetComponent<TextMeshProUGUI>();

            // 字体兜底：从 AI 回答文本框复制
            if (cnFont == null)
            {
                var aiGo = FindDeep(transform, "白板背景/数字人/对话框/AI回答")?.gameObject;
                if (aiGo != null) cnFont = aiGo.GetComponent<TextMeshProUGUI>()?.font;
            }

            // 长按数字人头像 → 打开面板（Setup 关闭此自动绑定，改由 M1DigitalHumanPresenter 统一处理）
            if (bindPressTarget)
            {
                var targetGo = FindDeep(transform, pressTargetPath)?.gameObject;
                if (targetGo == null) targetGo = FindDeep(transform, "数字人")?.gameObject;
                if (targetGo != null)
                {
                    _pressDetector = targetGo.GetComponent<M1PressDetector>();
                    if (_pressDetector == null) _pressDetector = targetGo.AddComponent<M1PressDetector>();
                    _pressDetector.holdDuration = holdDuration;
                    _pressDetector.OnLongPress += Open;
                    // 保证长按目标可被射线命中（防止美术误关 raycastTarget 导致入口失效）
                    var targetImg = targetGo.GetComponent<Image>();
                    if (targetImg != null) targetImg.raycastTarget = true;
                }
                else
                {
                    Debug.LogError("[M1QAPanel] 未找到长按目标：" + pressTargetPath);
                }
            }

            // 按钮绑定
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
            if (_voiceButton != null) _voiceButton.onClick.AddListener(OnVoiceClicked);
            if (_sendButton != null) _sendButton.onClick.AddListener(Send);
            if (_input != null)
            {
                _input.characterLimit = maxChars;
                _input.onValueChanged.AddListener(OnInputChanged);
            }
            if (_counter != null && _input != null) _counter.text = "0/" + maxChars;

            // 初始隐藏
            if (_panelRt != null) _panelRt.gameObject.SetActive(false);
            if (_blocker != null) _blocker.SetActive(false);
            UpdateSendInteractable();
        }

        private void OnDestroy()
        {
            if (_pressDetector != null) _pressDetector.OnLongPress -= Open;
            ApplyPause(false);
        }

        // ==================== 开关 ====================

        public void Open()
        {
            if (_isOpen || _panelRt == null || _blocker == null) return;
            _isOpen = true;
            ApplyPause(true);
            _blocker.SetActive(true);
            _panelRt.gameObject.SetActive(true);
            OnPanelVisibilityChanged?.Invoke(true);
            // 请求进行中（R7 中途关闭再重开）保持忙碌防并发；空闲时 _busy 本为 false，无需重置（Close 不停止请求/逐字协程）
            UpdateSendInteractable();
            // 从屏幕外滑入
            _panelRt.anchoredPosition = new Vector2(hiddenOffsetX, _panelRt.anchoredPosition.y);
            StartCoroutine(Slide(_panelRt.anchoredPosition.x, 0f));
            if (_input != null)
            {
                _input.text = string.Empty;
                _input.ActivateInputField();
            }
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            ApplyPause(false);
            if (_panelRt == null) return;
            StartCoroutine(Slide(_panelRt.anchoredPosition.x, hiddenOffsetX));
            if (_blocker != null) _blocker.SetActive(false);
        }

        /// <summary>问答面板激活时全局暂停游戏，关闭时恢复打开前的 timeScale（数字人视频/滑入动画走 unscaled 不受影响）。</summary>
        private void ApplyPause(bool pause)
        {
            if (!pauseGameOnOpen) return;
            if (pause && !_paused)
            {
                _paused = true;
                _timeScaleBefore = Time.timeScale;
                Time.timeScale = 0f;
            }
            else if (!pause && _paused)
            {
                _paused = false;
                Time.timeScale = _timeScaleBefore;
            }
        }

        private IEnumerator Slide(float fromX, float toX)
        {
            var elapsed = 0f;
            while (elapsed < slideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / slideDuration);
                t = 1f - (1f - t) * (1f - t); // easeOutQuad
                _panelRt.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, t), _panelRt.anchoredPosition.y);
                yield return null;
            }
            _panelRt.anchoredPosition = new Vector2(toX, _panelRt.anchoredPosition.y);
            if (!_isOpen)
            {
                _panelRt.gameObject.SetActive(false);
                OnPanelVisibilityChanged?.Invoke(false); // 完全滑出并隐藏后发布，数字人据此恢复提问前形态
            }
        }

        // ==================== 输入 ====================

        private void OnInputChanged(string value)
        {
            if (_counter != null) _counter.text = value.Length + "/" + maxChars;
            UpdateSendInteractable();
        }

        private void UpdateSendInteractable()
        {
            if (_sendButton != null)
                _sendButton.interactable = _input != null && _input.text.Length > 0 && !_busy;
        }

        private void OnVoiceClicked()
        {
            Debug.Log("[M1QAPanel] 语音输入：占位，待接入录音转文字（规格书 3.3：语音输入转文字后填入输入框）。");
            AddMessage(false, "语音输入功能待接入，请先用文字输入提问。");
        }

        public void Send()
        {
            if (_busy) return;
            if (_input == null || string.IsNullOrWhiteSpace(_input.text)) return;

            var question = _input.text.Trim();
            _input.text = string.Empty;
            UpdateSendInteractable();

            AddMessage(true, question);

            // 未配置 AI 服务：给出明确提示，不发请求
            var missing = deepSeekClient == null
                ? "尚未配置 AI 服务：画板缺少 M1DeepSeekClient 组件，请运行 Setup AI 提问面板。"
                : string.IsNullOrWhiteSpace(deepSeekClient.apiKey)
                    ? "尚未配置 API Key：请在画板 Inspector 的 M1DeepSeekClient 中填写后重试。"
                    : null;
            if (missing != null)
            {
                Debug.LogWarning("[M1QAPanel] " + missing);
                StartTyping(missing);
                return;
            }

            _busy = true;
            UpdateSendInteractable();
            var thinkingBubble = AddMessage(false, "正在思考...");
            OnAnswerStateChanged?.Invoke(AnswerState.Thinking);
            StartCoroutine(ChatRoutine(question, thinkingBubble));
        }

        private IEnumerator ChatRoutine(string question, MessageBubble thinkingBubble)
        {
            yield return deepSeekClient.ChatAsync(question,
                reply =>
                {
                    OnAnswerStateChanged?.Invoke(AnswerState.Speaking);
                    StartTyping(reply, thinkingBubble); // 复用"正在思考..."气泡逐字替换
                },
                error =>
                {
                    ShowError(thinkingBubble, error);
                });
        }

        /// <summary>请求失败：气泡显示错误提示，状态回 Idle，可继续提问。</summary>
        private void ShowError(MessageBubble bubble, string error)
        {
            bubble.text.text = error;
            UpdateBubbleSize(bubble);
            ScrollToBottom();
            _busy = false;
            UpdateSendInteractable();
            OnAnswerStateChanged?.Invoke(AnswerState.Idle);
        }

        // ==================== 消息 ====================

        private void StartTyping(string text, MessageBubble reuseBubble = null)
        {
            _busy = true;
            UpdateSendInteractable();
            if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
            _typingCoroutine = StartCoroutine(TypeText(text, reuseBubble));
        }

        private IEnumerator TypeText(string fullText, MessageBubble reuseBubble = null)
        {
            var bubble = reuseBubble ?? AddMessage(false, string.Empty);
            if (bubble == null)
            {
                _busy = false;
                UpdateSendInteractable();
                yield break;
            }
            var tmp = bubble.text;
            for (var i = 1; i <= fullText.Length; i++)
            {
                tmp.text = fullText.Substring(0, i);
                UpdateBubbleSize(bubble);
                // 立即重建布局并置底：避免下一帧才重建导致气泡扩展时与相邻消息重叠
                LayoutRebuilder.ForceRebuildLayoutImmediate(_messageContent);
                if (_scroll != null) _scroll.verticalNormalizedPosition = 0f;
                yield return new WaitForSecondsRealtime(typeSpeed);
            }
            _busy = false;
            UpdateSendInteractable();
            OnAnswerStateChanged?.Invoke(AnswerState.Idle);
            ScrollToBottom();
        }

        /// <summary>添加一条消息，返回气泡的文本/尺寸引用（供逐字显示用）。</summary>
        private MessageBubble AddMessage(bool isUser, string text)
        {
            if (_messageContent == null || cnFont == null)
            {
                Debug.LogWarning("[M1QAPanel] 消息列表或字体缺失，无法显示消息。");
                return null;
            }

            // 行：行高由 LayoutElement 显式控制（不依赖布局组 preferred 缓存，逐字生长不错位），气泡手动顶部锚定
            var rowGo = new GameObject(isUser ? "UserMessage" : "AiMessage",
                typeof(RectTransform), typeof(LayoutElement));
            rowGo.transform.SetParent(_messageContent, false);
            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = Vector2.zero;
            var rowElement = rowGo.GetComponent<LayoutElement>();
            // 微信风格间距：同发送者连续消息紧凑（2+2+spacing2 ≈ 6px），切换发送者顶部加大到 14（≈ 18px）
            var last = _messageContent.childCount > 0
                ? _messageContent.GetChild(_messageContent.childCount - 1).name
                : string.Empty;
            var topPad = last == (isUser ? "UserMessage" : "AiMessage") ? 2f : 14f;

            // 气泡：顶部锚定 + 切片，左侧消息锚左、右侧消息锚右
            var bubbleGo = new GameObject("Bubble",
                typeof(RectTransform), typeof(Image));
            bubbleGo.transform.SetParent(rowGo.transform, false);
            var bubbleImg = bubbleGo.GetComponent<Image>();
            bubbleImg.sprite = GetBubbleSprite();
            bubbleImg.type = Image.Type.Sliced;
            bubbleImg.color = isUser ? UserBubbleColor : AiBubbleColor;
            var bubbleRt = bubbleGo.GetComponent<RectTransform>();
            bubbleRt.anchorMin = new Vector2(isUser ? 0f : 1f, 1f);
            bubbleRt.anchorMax = new Vector2(isUser ? 0f : 1f, 1f);
            bubbleRt.pivot = new Vector2(isUser ? 0f : 1f, 1f);
            bubbleRt.anchoredPosition = new Vector2(isUser ? 12f : -12f, -topPad);

            // 文本
            var textGo = new GameObject("Text",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(bubbleGo.transform, false);
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.font = cnFont;
            tmp.fontSize = 28;
            tmp.color = new Color(0.15f, 0.15f, 0.15f, 1f); // 两侧消息统一黑字（微信/参考图风格）
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.text = text;
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(16f, 10f);
            textRt.offsetMax = new Vector2(-16f, -10f);

            var bubble = new MessageBubble { text = tmp, rect = bubbleRt, row = rowRt, rowElement = rowElement, topPad = topPad };
            UpdateBubbleSize(bubble);
            ScrollToBottom();
            return bubble;
        }

        /// <summary>按文本实际尺寸更新气泡大小，并同步行高（LayoutElement min/preferred 同值，保证逐字生长时行高立即跟随）。</summary>
        private void UpdateBubbleSize(MessageBubble bubble)
        {
            var tmp = bubble.text;
            var size = tmp.GetPreferredValues(bubbleMaxWidth, 0f);
            var w = Mathf.Min(size.x, bubbleMaxWidth);
            var h = size.y;
            bubble.rect.sizeDelta = new Vector2(w + BubblePaddingX, h + BubblePaddingY);
            var rowH = bubble.rect.sizeDelta.y + bubble.topPad + 2f; // 气泡高 + 顶部留白 + 底部留白 2
            bubble.row.sizeDelta = new Vector2(0f, rowH);
            bubble.rowElement.minHeight = rowH;
            bubble.rowElement.preferredHeight = rowH;
        }

        private void ScrollToBottom()
        {
            if (_scroll == null) return;
            StartCoroutine(ScrollToBottomNextFrame());
        }

        private IEnumerator ScrollToBottomNextFrame()
        {
            yield return null;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_messageContent);
            if (_scroll != null) _scroll.verticalNormalizedPosition = 0f;
        }

        private class MessageBubble
        {
            public TextMeshProUGUI text;
            public RectTransform rect;
            public RectTransform row;
            public LayoutElement rowElement;
            public float topPad;
        }

        // ==================== 查找工具 ====================

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
