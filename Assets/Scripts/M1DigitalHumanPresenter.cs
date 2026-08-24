using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace M1
{
    /// <summary>
    /// 数字人全身/头像展示与问答动画联动（唯一新增 runtime 组件，素材/尺寸/引用全部 Inspector 注入）：
    /// 短按切全身/头像（面板打开忽略）；长按记形态并开面板（头像态自动展开）；三态循环待机/思考/讲解视频；面板关闭后恢复提问前形态（请求中延后）。
    /// </summary>
    public class M1DigitalHumanPresenter : MonoBehaviour
    {
        [Header("引用（M1QASetup 注入）")]
        public M1QAPanel qaPanel;
        public VideoPlayer player;
        public RawImage rawImage;
        public GameObject fullBodyView;
        public GameObject avatarView;
        public M1PressDetector fullBodyPress;
        public M1PressDetector avatarPress;
        public VideoClip idleClip;
        public VideoClip thinkingClip;
        public VideoClip speakingClip;
        /// <summary>长按阻塞委托：非空且返回 true 时不响应长按（老板 2026-08-23：气泡台词说完前不能长按开输入界面）。</summary>
        public System.Func<bool> longPressBlocked;

        private enum DisplayMode { FullBody, Avatar }

        private DisplayMode _mode = DisplayMode.FullBody;
        private DisplayMode _modeBeforePanel = DisplayMode.FullBody;
        private bool _restorePending;
        private bool _panelOpen;
        private AnswerState _answer = AnswerState.Idle;
        private RenderTexture _rt;
        private bool _shortPressEnabled; // 全模块禁用点击切换全身/折叠头像，长按开问答面板保留

        private void Awake()
        {
            Bind(fullBodyPress, true);
            Bind(avatarPress, true);
            if (qaPanel != null)
            {
                qaPanel.OnAnswerStateChanged += OnAnswerState;
                qaPanel.OnPanelVisibilityChanged += OnPanelVisibility;
            }
            if (player != null)
            {
                player.playOnAwake = false;
                player.isLooping = true;
                player.audioOutputMode = VideoAudioOutputMode.None; // 运行时兜底静音，不依赖导入配置
                player.skipOnDrop = true;
                player.sendFrameReadyEvents = true;
                player.frameReady += OnFrameReady;
            }
            // 视频经 RenderTexture 由 RawImage 显示（复用开场引导链路）
            var clip = idleClip != null ? idleClip : thinkingClip != null ? thinkingClip : speakingClip;
            if (clip != null && player != null)
            {
                // 原生分辨率 RT + mip 链：缩小显示由 mip 采样避免白描边锯齿；VideoPlayer 只写基级，mip 需在 Update 显式重建
                _rt = new RenderTexture((int)clip.width, (int)clip.height, 0)
                { useMipMap = true, autoGenerateMips = false, filterMode = FilterMode.Bilinear };
                player.targetTexture = _rt;
                if (rawImage != null) rawImage.texture = _rt;
            }
        }

        private void Start()
        {
            ApplyMode(DisplayMode.FullBody); // 默认全身待机（内部按当前状态播放）
        }

        private void OnFrameReady(VideoPlayer source, long frame)
        {
            if (_rt != null) _rt.GenerateMips(); // 新帧已写入基级后再生成，避免首帧前调用失败
        }

        private void OnDestroy()
        {
            Bind(fullBodyPress, false);
            Bind(avatarPress, false);
            if (qaPanel != null)
            { qaPanel.OnAnswerStateChanged -= OnAnswerState; qaPanel.OnPanelVisibilityChanged -= OnPanelVisibility; }
            if (player != null) player.frameReady -= OnFrameReady;
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }

        private void Bind(M1PressDetector detector, bool on)
        {
            if (detector == null) return;
            if (on)
            {
                if (_shortPressEnabled) detector.OnShortPress += OnShortPress;
                detector.OnLongPress += OnLongPress;
            }
            else { detector.OnShortPress -= OnShortPress; detector.OnLongPress -= OnLongPress; }
        }

        /// <summary>保留兼容入口；默认全模块禁用短按切换，长按问答不受影响。</summary>
        public void SetShortPressEnabled(bool enabled)
        {
            if (_shortPressEnabled == enabled) return;
            _shortPressEnabled = enabled;
            if (fullBodyPress != null) { if (enabled) fullBodyPress.OnShortPress += OnShortPress; else fullBodyPress.OnShortPress -= OnShortPress; }
            if (avatarPress != null) { if (enabled) avatarPress.OnShortPress += OnShortPress; else avatarPress.OnShortPress -= OnShortPress; }
        }

        private void OnShortPress()
        {
            if (_panelOpen || _answer != AnswerState.Idle || _restorePending) return; // 回答期间锁定全身（R6/R7）
            ApplyMode(_mode == DisplayMode.FullBody ? DisplayMode.Avatar : DisplayMode.FullBody);
        }

        private void OnLongPress()
        {
            if (longPressBlocked != null && longPressBlocked()) return; // 台词播放中：长按不弹输入界面（老板 2026-08-23）
            // 面板打开或仍有待恢复形态时，再次长按不得覆盖首次记录（R5：头像恢复不被后续长按破坏）
            if (!_panelOpen && !_restorePending && _answer == AnswerState.Idle)
            {
                _modeBeforePanel = _mode;
                if (_mode == DisplayMode.Avatar) ApplyMode(DisplayMode.FullBody); // 头像态长按自动展开（R5）
            }
            if (qaPanel != null) qaPanel.Open();
        }

        private void OnAnswerState(AnswerState state)
        {
            _answer = state;
            PlayClip(ClipForState(state));
            if (state == AnswerState.Idle && _restorePending && !_panelOpen)
            { _restorePending = false; ApplyMode(_modeBeforePanel); } // 请求结束回 Idle：执行待恢复形态（R7）
        }

        /// <summary>云朵台词驱动（M2-M5 数字人气泡逐字期间播说话动画，结束后回待机；M1 AI 回答仍由 QAPanel 驱动，不受影响）。</summary>
        public void SetSpeechState(bool speaking) => OnAnswerState(speaking ? AnswerState.Speaking : AnswerState.Idle);

        private void OnPanelVisibility(bool open)
        {
            _panelOpen = open;
            if (open) return;
            // 面板完全关闭：Idle 立即恢复；请求未结束则等回 Idle 后恢复（R5/R7）
            if (_answer == AnswerState.Idle)
            {
                _restorePending = false;
                ApplyMode(_modeBeforePanel);
            }
            else _restorePending = true;
        }

        private void ApplyMode(DisplayMode mode)
        {
            _mode = mode;
            if (fullBodyView != null) fullBodyView.SetActive(mode == DisplayMode.FullBody);
            if (avatarView != null) avatarView.SetActive(mode == DisplayMode.Avatar);
            if (mode == DisplayMode.FullBody) PlayClip(ClipForState(_answer)); // R14：回全身按当前状态恢复播放，防停帧
        }

        private VideoClip ClipForState(AnswerState state)
            => state == AnswerState.Thinking ? thinkingClip
            : state == AnswerState.Speaking ? speakingClip : idleClip;

        private void PlayClip(VideoClip clip)
        {
            if (clip == null || player == null) return;
            if (player.clip == clip && player.isPlaying) return;
            player.Stop();
            player.clip = clip;
            player.Play(); // 从头播放并循环（R1）
        }
    }
}
