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
        [Tooltip("强制使用 URL 视频源（M3-M5 本地 StreamingAssets，WebGL 自动使用 CloudBase）。")]
        public bool forceUrlPlayback;
        [Tooltip("微信 WebGL 透明帧图集播放器（Awake 自动创建于 FullBodyView；仅微信端使用，替代第二个 VideoPlayer）")]
        public M1DigitalHumanFramePlayer framePlayer;
        [HideInInspector] public string idleUrl;
        [HideInInspector] public string thinkingUrl;
        [HideInInspector] public string speakingUrl;
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
        private bool _urlPlayback;
        private bool _useFrameAtlas;
        private int _urlVideoWidth = 1080;
        private int _urlVideoHeight = 1450;
        private bool _pendingAfterIntro;
        private bool _webGlVideoBlocked;

        private void Awake()
        {
            Bind(fullBodyPress, true);
            Bind(avatarPress, true);
            if (qaPanel != null)
            {
                qaPanel.OnAnswerStateChanged += OnAnswerState;
                qaPanel.OnPanelVisibilityChanged += OnPanelVisibility;
            }
            var delivery = VideoDeliveryConfig.Load();
            _urlPlayback = forceUrlPlayback || (delivery != null && delivery.UseRemoteVideo);
            _webGlVideoBlocked = _urlPlayback && !VideoDeliveryConfig.IsWechatAndroid();
            if (delivery != null)
            {
                if (string.IsNullOrEmpty(idleUrl)) idleUrl = delivery.IdleUrl;
                if (string.IsNullOrEmpty(thinkingUrl)) thinkingUrl = delivery.ThinkingUrl;
                if (string.IsNullOrEmpty(speakingUrl)) speakingUrl = delivery.SpeakingUrl;
                _urlVideoWidth = delivery.videoWidth;
                _urlVideoHeight = delivery.videoHeight;
            }
            // 微信端改透明帧图集后端（配置缺失时回退视频 URL 路径）；帧图集自带 Alpha，移除黑底抠像材质
            _useFrameAtlas = Application.platform == RuntimePlatform.WebGLPlayer && SetupFramePlayer();
            if (_useFrameAtlas && rawImage != null) rawImage.material = null;
            else if (Application.platform == RuntimePlatform.WebGLPlayer && rawImage != null && rawImage.material != null)
            {
                if (rawImage.material.HasProperty("_VideoInputIsSRGB")) rawImage.material.SetFloat("_VideoInputIsSRGB", 1f);
                if (rawImage.material.HasProperty("_VideoInputHasAlpha")) rawImage.material.SetFloat("_VideoInputHasAlpha", 0f);
            }
            if (player != null)
            {
                player.playOnAwake = false;
                player.isLooping = true;
                player.audioOutputMode = VideoAudioOutputMode.None; // 运行时兜底静音，不依赖导入配置
                if (_webGlVideoBlocked)
                {
                    // iOS 常驻数字人必须使用透明帧图集；配置缺失时退头像，绝不再创建第二个 WKVideo。
                    player.enabled = false;
                }
                else
                {
                    player.skipOnDrop = true;
                    player.waitForFirstFrame = true;
                    player.playbackSpeed = 1f;
                    player.sendFrameReadyEvents = true;
                    player.frameReady += OnFrameReady;
                    if (_urlPlayback) player.prepareCompleted += OnUrlPrepared;
                }
            }
            // 视频经 RenderTexture 由 RawImage 显示（复用开场引导链路）
            var clip = idleClip != null ? idleClip : thinkingClip != null ? thinkingClip : speakingClip;
            if (!_urlPlayback && clip != null && player != null) SetupRenderTexture((int)clip.width, (int)clip.height);
        }

        private void Start()
        {
            // 引导遮罩期间保持图集后端空闲；结束后由 ResumeAfterIntro 强制绑定待机首帧再显示。
            if (_useFrameAtlas)
            {
                if (WebGlVideoPlaybackGate.IntroActive)
                {
                    _pendingAfterIntro = true;
                    return;
                }
                ApplyMode(DisplayMode.FullBody);
                return;
            }
            if (_webGlVideoBlocked)
            {
                ApplyMode(DisplayMode.Avatar);
                return;
            }
            // 微信小游戏解码器只支持一个视频：M1 引导结束前不得创建常驻数字人视频实例。
            if (_urlPlayback && WebGlVideoPlaybackGate.IntroActive)
            {
                _pendingAfterIntro = true;
                return;
            }
            ApplyMode(DisplayMode.FullBody); // 默认全身待机（内部按当前状态播放）
        }

        /// <summary>微信帧图集后端装配：挂 RawImage 同节点并注入 target；配置资产缺失返回 false（回退视频路径）。</summary>
        private bool SetupFramePlayer()
        {
            if (rawImage == null) return false;
            framePlayer = rawImage.GetComponent<M1DigitalHumanFramePlayer>();
            if (framePlayer == null) framePlayer = rawImage.gameObject.AddComponent<M1DigitalHumanFramePlayer>(); // Unity 6 伪 null：必须 if == null 分步
            framePlayer.target = rawImage;
            if (!framePlayer.HasConfig()) return false;
            framePlayer.PlaybackFailed -= OnFramePlaybackFailed;
            framePlayer.PlaybackFailed += OnFramePlaybackFailed;
            return true;
        }

        private void OnFramePlaybackFailed() => ShowAvatar();

        public void ResumeAfterIntro()
        {
            // 引导会临时禁用 RawImage；帧图集后端不依赖 pending 标志，结束时必须主动恢复可见性与待机帧。
            if (_useFrameAtlas)
            {
                _pendingAfterIntro = false;
                PlayFrameState(true);
                return;
            }
            if (_webGlVideoBlocked)
            {
                ApplyMode(DisplayMode.Avatar);
                return;
            }
            if (!_pendingAfterIntro) return;
            _pendingAfterIntro = false;
            ApplyMode(DisplayMode.FullBody);
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
            if (player != null)
            {
                player.frameReady -= OnFrameReady;
                player.prepareCompleted -= OnUrlPrepared;
            }
            if (framePlayer != null) framePlayer.PlaybackFailed -= OnFramePlaybackFailed;
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
            if (_useFrameAtlas)
            {
                if (mode == DisplayMode.FullBody) PlayFrameState(false);
                else ShowAvatar();
                return;
            }
            if (mode == DisplayMode.Avatar)
            {
                ShowAvatar();
                return;
            }
            _mode = DisplayMode.FullBody;
            if (fullBodyView != null) fullBodyView.SetActive(true);
            if (avatarView != null) avatarView.SetActive(false);
            PlayClip(ClipForState(_answer)); // R14：回全身按当前状态恢复播放，防停帧
        }

        private VideoClip ClipForState(AnswerState state)
            => state == AnswerState.Thinking ? thinkingClip
            : state == AnswerState.Speaking ? speakingClip : idleClip;

        private string UrlForState(AnswerState state)
            => state == AnswerState.Thinking ? thinkingUrl
            : state == AnswerState.Speaking ? speakingUrl : idleUrl;

        private string KeyForState(AnswerState state)
            => state == AnswerState.Thinking ? "thinking"
            : state == AnswerState.Speaking ? "speaking" : "idle";

        private void PlayClip(VideoClip clip)
        {
            // 微信帧图集后端：状态机/长按问答行为与视频后端完全一致，仅切换播放源
            if (_useFrameAtlas)
            {
                PlayFrameState(false);
                return;
            }
            if (_webGlVideoBlocked) return;
            if (player == null) return;
            if (_urlPlayback)
            {
                var url = UrlForState(_answer);
                if (string.IsNullOrEmpty(url))
                {
                    Debug.LogWarning("[M1DigitalHumanPresenter] 未配置视频 URL。");
                    return;
                }
                if (player.url == url && (player.isPlaying || player.isPrepared)) return;
                player.Stop();
                player.source = VideoSource.Url;
                player.url = url;
                SetupRenderTexture(_urlVideoWidth, _urlVideoHeight);
                player.Prepare();
                return;
            }
            if (clip == null) return;
            if (player.clip == clip && player.isPlaying) return;
            player.Stop();
            player.source = VideoSource.VideoClip;
            player.clip = clip;
            player.Play(); // 从头播放并循环（R1）
        }

        /// <summary>帧图集缺失/加载失败时使用已有头像视图，避免空 RawImage 绘制为白块。</summary>
        private bool PlayFrameState(bool forceReload)
        {
            if (framePlayer != null && framePlayer.PlayState(KeyForState(_answer), forceReload))
            {
                if (!WebGlVideoPlaybackGate.IntroActive) ShowFullBody();
                return true;
            }
            ShowAvatar();
            return false;
        }

        private void ShowFullBody()
        {
            _mode = DisplayMode.FullBody;
            if (rawImage != null) rawImage.enabled = true;
            if (fullBodyView != null) fullBodyView.SetActive(true);
            if (avatarView != null) avatarView.SetActive(false);
        }

        private void ShowAvatar()
        {
            _mode = DisplayMode.Avatar;
            if (WebGlVideoPlaybackGate.IntroActive) return;
            if (fullBodyView != null) fullBodyView.SetActive(false);
            if (avatarView != null) avatarView.SetActive(true);
        }

        private void OnUrlPrepared(VideoPlayer source)
        {
            if (!_urlPlayback) return;
            SetupRenderTexture((int)source.width, (int)source.height);
            source.Play();
        }

        private void SetupRenderTexture(int width, int height)
        {
            if (_rt != null || player == null) return;
            _rt = new RenderTexture(Mathf.Max(1, width), Mathf.Max(1, height), 0)
            { useMipMap = true, autoGenerateMips = false, filterMode = FilterMode.Bilinear };
            player.targetTexture = _rt;
            if (rawImage != null) rawImage.texture = _rt;
        }
    }
}
