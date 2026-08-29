using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace M1
{
    /// <summary>
    /// 开场引导动画控制器（挂在"引导遮罩"Canvas 上）：
    /// 场景加载即 timeScale=0 时间静止并提前 Prepare 预解码视频（避免开头卡顿），
    /// 显示半黑遮罩 + 居中播放竖屏引导视频（方案 A：高度适配，两侧留黑），
    /// 播完自动恢复游戏；PlayerPrefs 记录首次进入——首次不可跳过，非首次可点击遮罩/跳过按钮跳过。
    /// </summary>
    public class M1IntroVideo : MonoBehaviour
    {
        [Tooltip("引导遮罩 Canvas 根物体（播放期间激活，结束/跳过时隐藏）")]
        public GameObject overlay;

        [Tooltip("播放引导视频的 VideoPlayer（挂在引导视频 RawImage 上）")]
        public VideoPlayer player;

        [Tooltip("显示视频画面的 RawImage")]
        public RawImage videoImage;

        [Tooltip("右上角跳过按钮（首次进入自动隐藏）")]
        public Button skipButton;

        [Tooltip("非首次进入时是否允许点击跳过")]
        public bool allowSkipOnReplay = true;

        [Tooltip("PlayerPrefs 首次进入标记 key")]
        public string seenPrefsKey = "M1_Intro_Seen";

        [Tooltip("引导播放期间需要一并暂停的视频（如常驻数字人待机，VideoPlayer 不受 timeScale 影响），结束/跳过时恢复")]
        public VideoPlayer[] pauseWhilePlaying;

        [Tooltip("运行时兜底：pauseWhilePlaying 未配置时按此路径自动发现常驻数字人视频（Setup 注入后此项失效；M2 无此路径自动跳过）")]
        public string digitalHumanPath = "画板/DigitalHumanStage/FullBodyView";

        [Tooltip("引导播放期间隐藏、结束/跳过时恢复的对象（如常驻数字人全身和对白框）：禁用对象及其子级 Graphic；无 Graphic 才 SetActive(false)")]
        public GameObject[] hideWhilePlaying;

        [Tooltip("运行时兜底：按此路径自动发现常驻数字人全身并补入隐藏列表（Setup 注入后仍会补全缺失项）")]
        public string hideStagePath = "画板/DigitalHumanStage/FullBodyView";

        [Tooltip("运行时兜底：按此路径自动发现白板数字人对白框并补入隐藏列表（Setup 注入后仍会补全缺失项）")]
        public string hideDialoguePath = "画板/白板背景/数字人/对话框";

        [Tooltip("引导字幕 TMP（2026-08-18 老板定稿：视频静音、解说词改字幕；Setup 注入，可为空则不显示）")]
        public TextMeshProUGUI subtitleText;

        [Tooltip("引导视频等比缩放（2026-08-18：数字人缩小、与字幕分离；Setup 已设值时不覆盖）")]
        public float introVideoScale = 0.78f;

        [Tooltip("移除引导视频内的纯绿色分隔线")]
        public bool removeGreenGuide = true;

        [Tooltip("运行时兜底：subtitleText 未注入时按此路径自动发现（Setup 注入后此项失效）")]
        public string subtitlePath = "画板/引导遮罩/引导字幕";

        [Tooltip("字幕分段台词（对应引导视频解说词，Inspector 可改）")]
        public string[] subtitleSegments =
        {
            "叮咚！AI 智能陪练铁小探上线啦～",
            "今天我们要用“三位一体、交叉验证”新工艺，完成对铝热焊缝轨头下颚伤损的探测。",
            "我会全程贴身陪练，遇到难题随时为大家答疑。准备好，我们这就开启今天的探测啦！"
        };

        [Tooltip("每段字幕起始秒（视频约 15.2 秒，Inspector 可微调对帧）")]
        public float[] subtitleTimes = { 0.5f, 4.2f, 10.2f };

        private int _subtitleIndex = -1;

        private bool[] _hiddenActive;
        private Graphic[][] _hiddenGraphics;
        private bool[][] _hiddenGraphicEnabled;

        [Header("微信 WebGL 布局（2026-08-28 真机定稿：字幕在人物正下方居中）")]
        [Tooltip("微信端引导人物等比缩放（从 0.70 起调；仅 WebGL 覆盖，Android 保持 Setup 的 0.78）")]
        public float webglIntroVideoScale = 0.70f;

        [Tooltip("微信端人物缩放下限：20px 字幕间距与底部安全区冲突时逐步缩小人物，禁止把字幕压回人物")]
        public float webglIntroVideoMinScale = 0.60f;

        [Tooltip("微信端字幕与人物画面底边的可见间距（px，1920x1080 业务坐标）")]
        public float webglSubtitleGap = 20f;

        [Tooltip("微信端单行字幕宽度（px），超长台词通过自动字号缩小兜底")]
        public float webglSubtitleWidth = 1700f;

        [Tooltip("微信端单行字幕矩形高度（px）")]
        public float webglSubtitleHeight = 52f;

        [Tooltip("微信端字幕最大字号；过长时自动缩小至 24，禁止换行")]
        public float webglSubtitleFontSize = 30f;

        [Tooltip("微信端字幕矩形最低底部位置（px）：低于此值上移，避开底部手势区")]
        public float webglSubtitleMinBottom = 40f;

        [Tooltip("预解码超时兜底（秒）：超过仍未准备好则直接播放")]
        public float prepareTimeout = 5f;

        [Tooltip("微信 WebGL 视频准备超时（秒）：CDN 长时间未准备完成时关闭引导遮罩；0=关闭")]
        public float webglPrepareTimeout = 30f;

        [Tooltip("微信 WebGL 视频启动超时（秒）：准备完成后播放时间仍未推进时关闭引导遮罩；0=关闭")]
        public float webglPlaybackStartTimeout = 8f;

        [Header("微信分包视频（2026-08-30：引导视频随包分发，进游戏即播）")]
        [Tooltip("分包就绪等待上限（秒）：导出注入的 JS 启动加载 videos 分包并写 storage 信号，超时或失败回退 CDN URL")]
        public float webglPackageWaitTimeout = 4f;

        [Tooltip("CDN 兜底准备超时（秒）：分包源失败转 CDN 播放后，仍无准备完成则转海报字幕引导；0=关闭")]
        public float webglFallbackPrepareTimeout = 6f;

        [Tooltip("海报兜底引导时长（秒）：视频完全不可用时以海报+字幕完成引导，到时自动恢复游戏")]
        public float webglPosterFallbackDuration = 16f;

        /// <summary>JS 注入侧写入的分包就绪信号 storage key（1=就绪 2=失败）；Editor 导出工具按同名注入 game.js。</summary>
        public const string PackageReadyKey = "__wxIntroVideoPkg";

        private RenderTexture _rt;
        private bool _firstTime;
        private bool _started;
        private bool _finished;
        private int _lastScreenW;
        private int _lastScreenH;
        private bool _urlPlayback;
        private bool _prepared;
        private bool _playbackTimeoutStarted;
        private float _playbackStartRealtime = -1f;
        private int _remoteVideoWidth = 1080;
        private int _remoteVideoHeight = 1450;
        private Image _dimOverlay;
        private Color _dimOverlayColor;
        private RawImage _posterImage;
        private bool _firstFrameShown;
        private VideoDeliveryConfig _delivery;
        private bool _usePackageSource;      // WebGL 且配置了 videos 分包内引导视频
        private bool _introSourceAvailable;  // 存在任一可播放视频源（包内/CDN/本地 clip）
        private bool _posterFallback;        // 海报+字幕兜底引导进行中
        private float _posterClockStart = -1f;

        private void Awake()
        {
            // 运行时自愈：引导遮罩必须 Overlay + sortingOrder 100 才能盖过主画板内所有 UI（含数字人舞台）；
            // 防场景被误存为 WorldSpace/sortingOrder 0（此时 effective=0 与画板同层，Stage 按兄弟顺序浮于遮罩之上）
            var canvas = GetComponent<Canvas>();
            if (canvas != null && (canvas.renderMode != RenderMode.ScreenSpaceOverlay || canvas.sortingOrder != 100))
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
            }
            // 运行时兜底：Setup 未注入暂停列表时，按路径自动发现常驻数字人视频（防引导期间数字人继续播放）
            if ((pauseWhilePlaying == null || pauseWhilePlaying.Length == 0) && !string.IsNullOrEmpty(digitalHumanPath))
            {
                var dh = GameObject.Find(digitalHumanPath);
                var vp = dh != null ? dh.GetComponent<VideoPlayer>() : null;
                if (vp != null) pauseWhilePlaying = new[] { vp };
            }
            // 运行时兜底：补齐全身数字人与白板对白框，防旧场景未重跑 Setup 时在半黑遮罩下透出。
            EnsureHideTargets();
            // 运行时兜底：Setup 未注入字幕引用时，按路径自动发现（防引导视频无字幕）
            if (subtitleText == null && !string.IsNullOrEmpty(subtitlePath))
            {
                var sub = GameObject.Find(subtitlePath);
                if (sub != null) subtitleText = sub.GetComponent<TextMeshProUGUI>();
            }
            // 先确定播放后端：动态字幕创建必须立即使用正确的平台布局，不能等创建后再赋值。
            var delivery = VideoDeliveryConfig.Load();
            _delivery = delivery;
            _urlPlayback = delivery != null && delivery.UseRemoteVideo;
            // 运行时兜底二：场景无字幕节点时动态创建（挂遮罩底部，字体从跳过按钮复制），保证不跑 Setup 也显示字幕
            if (subtitleText == null) subtitleText = CreateRuntimeSubtitle();
            // 运行时兜底：引导视频等比缩小（数字人变小；Setup 已设 0.78 则不覆盖）
            if (videoImage != null && Mathf.Approximately(videoImage.rectTransform.localScale.x, 1f))
                videoImage.rectTransform.localScale = new Vector3(introVideoScale, introVideoScale, 1f);
            // 微信端人物缩放按真机定稿覆盖（字幕在人物正下方，0.70 起调；Android 保持 Setup 的 0.78）
            if (Application.platform == RuntimePlatform.WebGLPlayer && videoImage != null)
                videoImage.rectTransform.localScale = new Vector3(webglIntroVideoScale, webglIntroVideoScale, 1f);
            if (videoImage != null && videoImage.material != null)
            {
                if (videoImage.material.HasProperty("_RemoveGreenGuide"))
                    videoImage.material.SetFloat("_RemoveGreenGuide", removeGreenGuide ? 1f : 0f);
                if (Application.platform == RuntimePlatform.WebGLPlayer && videoImage.material.HasProperty("_VideoInputIsSRGB"))
                    videoImage.material.SetFloat("_VideoInputIsSRGB", 1f);
            }

            _firstTime = PlayerPrefs.GetInt(seenPrefsKey, 0) == 0;
            if (player == null)
            {
                Debug.LogError("[M1IntroVideo] 未配置 VideoPlayer。");
                return;
            }
            if (_urlPlayback)
            {
                _remoteVideoWidth = delivery.videoWidth;
                _remoteVideoHeight = delivery.videoHeight;
                CacheDimOverlay();
                SetDimOverlayVisible(false); // 下载首帧期间保留正常教学画面，避免空黑遮罩
                CreatePoster();
                StartCoroutine(ApplyWebGlSubtitleLayoutWhenReady()); // 等 Canvas/ARF 布局完成后再定位字幕
                // 2026-08-30：包内 720p 视频优先（videos 分包随包分发，进游戏即播）；未配置则维持 CDN 直连
                _usePackageSource = !string.IsNullOrEmpty(delivery.wechatIntroPackageFile);
                if (!_usePackageSource)
                {
                    player.source = VideoSource.Url;
                    player.url = delivery.IntroUrl;
                }
            }
            _introSourceAvailable = _urlPlayback
                ? _usePackageSource || !string.IsNullOrEmpty(delivery.IntroUrl)
                : player.clip != null;
            if (!_introSourceAvailable)
            {
                Debug.LogError("[M1IntroVideo] 未配置可播放的引导视频源，转海报字幕兜底引导。");
                Time.timeScale = 0f;
                StartPosterFallback();
                return;
            }

            // 视频渲染到 RenderTexture，再由 RawImage 显示（保证视频层叠在遮罩之上）
            if (_urlPlayback) SetupRenderTexture(_remoteVideoWidth, _remoteVideoHeight);
            else SetupRenderTexture((int)player.clip.width, (int)player.clip.height);
            player.loopPointReached += OnVideoEnd;
            player.prepareCompleted += OnPrepared;
            player.errorReceived += OnVideoError;
            player.sendFrameReadyEvents = true;
            player.frameReady += OnFrameReady;
            player.waitForFirstFrame = true;
            player.playbackSpeed = 1f;
            player.audioOutputMode = VideoAudioOutputMode.None; // 老板 2026-08-18：引导视频静音，解说词改字幕（防场景旧序列化 Direct 覆盖）

            // 微信小游戏视频解码器只支持一个实例；先阻止常驻数字人启动，直到引导播放器完全释放。
            if (_urlPlayback) WebGlVideoPlaybackGate.IntroActive = true;

            // 场景加载即冻结游戏 + 后台预解码：避免玩家在准备期间操作，也避免播放开头卡顿
            Time.timeScale = 0f;
            if (_usePackageSource) StartCoroutine(WaitWebGlPackageSource());
            else player.Prepare();
            StartCoroutine(PrepareTimeout());
        }

        /// <summary>引导播放期间强制冻结附带视频：VideoPlayer 不受 timeScale=0 影响，Presenter 可能在 Start 后重新播放，故每帧保持暂停。同时按播放进度切换字幕。</summary>
        private void Update()
        {
            CheckWebGlSubtitleRelayout();
            UpdateSubtitle();
            // 海报兜底引导到时自动结束（realtime 计时，不受 timeScale=0 影响）
            if (_posterFallback && !_finished && _posterClockStart >= 0f
                && Time.realtimeSinceStartup - _posterClockStart >= webglPosterFallbackDuration)
            {
                _finished = true;
                FinishIntro();
                return;
            }
            if (_finished || pauseWhilePlaying == null) return;
            foreach (var p in pauseWhilePlaying)
                if (p != null && p.isPlaying) p.Pause();
        }

        private void Start()
        {
            PlayerPrefs.SetInt(seenPrefsKey, 1);
            PlayerPrefs.Save();

            // 先显示遮罩（预解码期间盖住画面，防止穿帮）
            var canSkip = !_firstTime && allowSkipOnReplay;
            overlay.SetActive(true);
            if (skipButton != null) skipButton.gameObject.SetActive(canSkip);
            var overlayButton = overlay.GetComponent<Button>();
            if (overlayButton != null) overlayButton.interactable = canSkip;

            // 有视频源或海报兜底时隐藏常驻数字人（结束后 RestoreWhilePlaying 恢复）；完全无引导内容时不隐藏，防止数字人永久消失
            if (player != null && (_introSourceAvailable || _posterFallback))
            {
                HideWhilePlaying();
                if (!_urlPlayback && !_posterFallback) TryPlay(); // 远程视频必须等待 prepareCompleted，避免首播速度异常
                if (Application.platform == RuntimePlatform.WebGLPlayer && webglPrepareTimeout > 0f && !_usePackageSource && !_posterFallback)
                    StartCoroutine(WebGlPrepareTimeout());
            }
        }

        private void OnPrepared(VideoPlayer vp)
        {
            if (_posterFallback) return; // 兜底已接管，忽略迟到的视频准备
            _prepared = true;
            // 包内视频 720×966 与配置声明的 1080×1450 不同：按实际解码尺寸重建 RenderTexture，避免画面只占一角
            if (_urlPlayback && vp.width > 0 && vp.height > 0 && (_rt == null || _rt.width != (int)vp.width || _rt.height != (int)vp.height))
            {
                if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
                SetupRenderTexture((int)vp.width, (int)vp.height);
            }
            TryPlay();
            if (_urlPlayback && webglPlaybackStartTimeout > 0f && !_playbackTimeoutStarted)
            {
                _playbackTimeoutStarted = true;
                StartCoroutine(WebGlFirstFrameTimeout());
            }
        }

        private void OnFrameReady(VideoPlayer vp, long frame)
        {
            if (!_urlPlayback || _firstFrameShown) return;
            _firstFrameShown = true;
            if (_posterImage != null) _posterImage.enabled = false;
            SetDimOverlayVisible(true);
            if (subtitleText != null) subtitleText.enabled = true;
        }

        private void OnVideoError(VideoPlayer vp, string message)
        {
            Debug.LogWarning("[M1IntroVideo] 视频播放失败：" + message);
            if (Application.platform != RuntimePlatform.WebGLPlayer || _finished || _posterFallback) return;
            if (_firstFrameShown) { _finished = true; FinishIntro(); return; } // 已开播后失败：直接放行进游戏
            StartPosterFallback(); // 首帧前失败：快速转海报字幕引导，不再干等超时
        }

        /// <summary>预解码超时兜底：Prepare 长时间未完成（解码异常）时强制播放，避免永久黑屏。</summary>
        private IEnumerator PrepareTimeout()
        {
            yield return new WaitForSecondsRealtime(prepareTimeout); // 不受 timeScale=0 影响
            if (!_urlPlayback) TryPlay();
        }

        private void TryPlay()
        {
            if (_started || _finished) return;
            _started = true;
            _playbackStartRealtime = Time.realtimeSinceStartup;
            player.Play();
        }

        private IEnumerator WebGlPrepareTimeout()
        {
            yield return new WaitForSecondsRealtime(webglPrepareTimeout);
            if (_finished || _prepared || _posterFallback) yield break;

            Debug.LogWarning("[M1IntroVideo] 微信 WebGL 视频准备超时，跳过引导以避免黑屏。");
            _finished = true;
            FinishIntro();
        }

        private IEnumerator WebGlFirstFrameTimeout()
        {
            yield return new WaitForSecondsRealtime(webglPlaybackStartTimeout);
            if (_finished || _firstFrameShown || _posterFallback) yield break;

            Debug.LogWarning("[M1IntroVideo] 微信 WebGL 视频首帧超时，跳过引导以避免永久冻结。");
            _finished = true;
            FinishIntro();
        }

        /// <summary>轮询导出注入 JS 写入的 videos 分包就绪信号：就绪用包内路径播放；失败/超时回退 CDN URL。</summary>
        private IEnumerator WaitWebGlPackageSource()
        {
            var deadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, webglPackageWaitTimeout);
            var ready = false;
            while (Time.realtimeSinceStartup < deadline)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                var flag = WeChatWASM.WXBase.StorageGetStringSync(PackageReadyKey, "0");
#else
                var flag = "2"; // 非 WebGL 编译目标不含 WX 接口：视为分包不可用，直接走 CDN 兜底
#endif
                if (flag == "1") { ready = true; break; }
                if (flag == "2") break;
                yield return new WaitForSecondsRealtime(0.2f);
            }
            if (ready)
            {
                player.source = VideoSource.Url;
                player.url = _delivery.wechatIntroPackageFile; // 代码包相对路径，随包秒取
                player.Prepare();
                yield break;
            }
            Debug.LogWarning("[M1IntroVideo] 分包引导视频未就绪，回退 CDN URL 播放。");
            FallbackToCdn();
        }

        /// <summary>分包源失败后的 CDN 兜底：配置了较短准备超时，仍失败则进海报字幕引导。</summary>
        private void FallbackToCdn()
        {
            var url = _delivery != null ? _delivery.IntroUrl : string.Empty;
            if (string.IsNullOrEmpty(url)) { StartPosterFallback(); return; }
            player.source = VideoSource.Url;
            player.url = url;
            player.Prepare();
            if (Application.platform == RuntimePlatform.WebGLPlayer && webglFallbackPrepareTimeout > 0f)
                StartCoroutine(WebGlFallbackPrepareTimeout());
        }

        private IEnumerator WebGlFallbackPrepareTimeout()
        {
            yield return new WaitForSecondsRealtime(webglFallbackPrepareTimeout);
            if (_finished || _prepared || _posterFallback) yield break;

            Debug.LogWarning("[M1IntroVideo] CDN 兜底视频准备超时，转海报字幕兜底引导。");
            StartPosterFallback();
        }

        /// <summary>视频链路完全不可用时的兜底引导：半黑遮罩 + 海报 + 按 realtime 时间轴播放字幕，到时自动恢复游戏。</summary>
        private void StartPosterFallback()
        {
            if (_posterFallback || _finished) return;
            _posterFallback = true;
            Debug.LogWarning("[M1IntroVideo] 启用海报+字幕兜底引导（" + webglPosterFallbackDuration + " 秒）。");
            SetDimOverlayVisible(true);
            if (_posterImage != null) _posterImage.enabled = true;
            if (subtitleText != null) subtitleText.enabled = true;
            _posterClockStart = Time.realtimeSinceStartup;
        }

        /// <summary>跳过引导（遮罩/跳过按钮点击触发；首次进入时不可用）。</summary>
        public void Skip()
        {
            if (_finished) return;
            _finished = true;
            FinishIntro();
        }

        private void OnVideoEnd(VideoPlayer vp)
        {
            if (_finished) return;
            _finished = true;
            FinishIntro();
        }

        private void FinishIntro()
        {
            if (player != null)
            {
                player.Stop();
                player.loopPointReached -= OnVideoEnd;
                player.prepareCompleted -= OnPrepared;
                player.errorReceived -= OnVideoError;
                player.frameReady -= OnFrameReady;
                // Stop 不会释放微信底层解码器；禁用组件后才能启动常驻数字人。
                if (_urlPlayback) player.enabled = false;
            }
            WebGlVideoPlaybackGate.IntroActive = false;
            RestoreWhilePlaying(); // 引导结束：先恢复常驻数字人显示
            if (pauseWhilePlaying != null)
                foreach (var p in pauseWhilePlaying)
                {
                    if (p == null) continue;
                    var presenter = p.GetComponentInParent<M1DigitalHumanPresenter>();
                    if (presenter != null) presenter.ResumeAfterIntro();
                    else if (p.isPaused) p.Play();
                }
            SetDimOverlayVisible(true);
            if (subtitleText != null) subtitleText.text = ""; // 字幕清空
            overlay.SetActive(false);
            Time.timeScale = 1f; // 恢复游戏
        }

        private void SetupRenderTexture(int width, int height)
        {
            if (_rt != null || player == null) return;
            _rt = new RenderTexture(Mathf.Max(1, width), Mathf.Max(1, height), 0);
            player.targetTexture = _rt;
            if (videoImage != null) videoImage.texture = _rt;
        }

        /// <summary>按视频播放进度切换字幕分段；未到第一段或已结束时清空。</summary>
        private void UpdateSubtitle()
        {
            if (_finished || subtitleText == null || player == null || subtitleSegments == null || subtitleSegments.Length == 0) return;
            // 海报兜底模式：字幕跟兜底时钟走（视频未参与）；正常模式：URL 播放用 realtime，本地 clip 用视频时间
            var t = _posterFallback && _posterClockStart >= 0f
                ? Time.realtimeSinceStartup - _posterClockStart
                : _urlPlayback && _playbackStartRealtime >= 0f
                    ? Time.realtimeSinceStartup - _playbackStartRealtime
                    : (float)player.time;
            var idx = -1;
            if (subtitleTimes != null)
                for (var i = subtitleTimes.Length - 1; i >= 0; i--)
                    if (t >= subtitleTimes[i]) { idx = i; break; }
            if (idx < 0)
            {
                if (_subtitleIndex != -1) { _subtitleIndex = -1; subtitleText.text = ""; }
                return;
            }
            if (idx >= subtitleSegments.Length) idx = subtitleSegments.Length - 1;
            if (idx != _subtitleIndex)
            {
                _subtitleIndex = idx;
                subtitleText.text = subtitleSegments[idx];
            }
        }

        /// <summary>场景无字幕节点时运行时动态创建（挂引导遮罩底部：白字描边，无背景条；字体从跳过按钮 TMP 复制）。
        /// 仅当 Setup 未创建/未注入字幕时才走此兜底；动态节点为 DontSave，不写入场景。</summary>
        private TextMeshProUGUI CreateRuntimeSubtitle()
        {
            if (overlay == null) return null;
            var textGo = new GameObject("~IntroSubtitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.hideFlags = HideFlags.DontSave;
            textGo.transform.SetParent(overlay.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            if (_urlPlayback)
            {
                // 微信初始布局（ApplyWebGlSubtitleLayout 会按视频底边精调，这里给同合同默认值）
                trt.anchorMin = new Vector2(0.5f, 0f);
                trt.anchorMax = new Vector2(0.5f, 0f);
                trt.pivot = new Vector2(0.5f, 0f);
                trt.anchoredPosition = new Vector2(0f, webglSubtitleMinBottom);
                trt.sizeDelta = new Vector2(webglSubtitleWidth, webglSubtitleHeight);
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 24f;
                tmp.fontSizeMax = webglSubtitleFontSize;
            }
            else
            {
                trt.anchorMin = new Vector2(0.5f, 0f);
                trt.anchorMax = new Vector2(0.5f, 0f);
                trt.pivot = new Vector2(0.5f, 0f);
                trt.anchoredPosition = new Vector2(0f, 16f);
                trt.sizeDelta = new Vector2(1100f, 150f);
                tmp.alignment = TextAlignmentOptions.Bottom;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
            }
            tmp.fontSize = _urlPlayback ? webglSubtitleFontSize : 34f;
            tmp.color = Color.white;
            if (_urlPlayback) tmp.enabled = false;
            if (skipButton != null)
            {
                var src = skipButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (src != null && src.font != null) tmp.font = src.font;
            }
            if (tmp.font == null)
            {
                foreach (var candidate in FindObjectsOfType<TextMeshProUGUI>(true))
                    if (candidate.font != null) { tmp.font = candidate.font; break; }
            }
            var ol = textGo.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.9f);
            ol.effectDistance = new Vector2(2f, -2f);
            var sh = textGo.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.7f);
            sh.effectDistance = new Vector2(2f, -3f);
            return tmp;
        }

        /// <summary>微信 WebGL 字幕合同（2026-08-29 真机修复）：人物正下方、水平居中、强制单行。
        /// 字幕上缘锚定引导视频画面底边下方（按 webglSubtitleGap 留可见间距），随缩放/宽屏自适应；不越底部手势区。</summary>
        private void ApplyWebGlSubtitleLayout()
        {
            if (subtitleText == null) return;
            var rt = subtitleText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(webglSubtitleWidth, webglSubtitleHeight);
            // 字幕矩形顶边 = 视频画面底边 - 间距；冲突时先缩小人物（至下限），禁止把字幕向上压回人物
            var scale = webglIntroVideoScale;
            var bottom = ComputeVideoBottomLocalY();
            var y = bottom - webglSubtitleGap - webglSubtitleHeight;
            while (y < webglSubtitleMinBottom && videoImage != null && scale > webglIntroVideoMinScale + 0.001f)
            {
                scale = Mathf.Max(scale - 0.02f, webglIntroVideoMinScale);
                videoImage.rectTransform.localScale = new Vector3(scale, scale, 1f);
                Canvas.ForceUpdateCanvases();
                bottom = ComputeVideoBottomLocalY();
                y = bottom - webglSubtitleGap - webglSubtitleHeight;
            }
            rt.anchoredPosition = new Vector2(0f, Mathf.Max(y, webglSubtitleMinBottom));
            subtitleText.fontSize = webglSubtitleFontSize;
            subtitleText.enableAutoSizing = true;
            subtitleText.fontSizeMin = 24f;
            subtitleText.fontSizeMax = webglSubtitleFontSize;
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.textWrappingMode = TextWrappingModes.NoWrap;
            // 布局协程可能晚于首帧回调或海报兜底执行；可见性必须由首帧/兜底状态决定，不能无条件再次隐藏。
            subtitleText.enabled = _firstFrameShown || _posterFallback;
        }

        /// <summary>Canvas/AspectRatioFitter 布局完成后再应用字幕几何；分辨率或方向变化时幂等重算。</summary>
        private IEnumerator ApplyWebGlSubtitleLayoutWhenReady()
        {
            yield return null; // 等首帧布局
            Canvas.ForceUpdateCanvases();
            ApplyWebGlSubtitleLayout();
            yield return null; // AspectRatioFitter 可能晚一帧稳定，再算一次（幂等）
            Canvas.ForceUpdateCanvases();
            ApplyWebGlSubtitleLayout();
        }

        /// <summary>分辨率/方向变化时重算字幕（ApplyWebGlSubtitleLayout 幂等；MonoBehaviour 无 Rect 变化回调，用屏幕尺寸轮询）。</summary>
        private void CheckWebGlSubtitleRelayout()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer || _finished || subtitleText == null) return;
            if (Screen.width == _lastScreenW && Screen.height == _lastScreenH) return;
            _lastScreenW = Screen.width;
            _lastScreenH = Screen.height;
            Canvas.ForceUpdateCanvases();
            ApplyWebGlSubtitleLayout();
            // ApplyWebGlSubtitleLayout 按首帧状态恢复可见性，分辨率重算不会把字幕永久隐藏。
        }

        /// <summary>引导视频画面底边在字幕父级（引导遮罩）本地坐标系中的 Y（含 webglIntroVideoScale 缩放）。</summary>
        private float ComputeVideoBottomLocalY()
        {
            if (videoImage == null)
                return webglSubtitleMinBottom + webglSubtitleHeight + webglSubtitleGap; // 无视频引用退化为固定位置
            var corners = new Vector3[4];
            videoImage.rectTransform.GetWorldCorners(corners); // 0=左下 1=左上 2=右上 3=右下
            var bottomCenter = (corners[0] + corners[3]) * 0.5f;
            return transform.InverseTransformPoint(bottomCenter).y;
        }

        private void CreatePoster()
        {
            if (videoImage == null || videoImage.transform.parent == null) return;
            var texture = Resources.Load<Texture2D>("DigitalHuman/IntroPoster");
            if (texture == null) return;
            var poster = new GameObject("~IntroPoster", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            poster.hideFlags = HideFlags.DontSave;
            poster.transform.SetParent(videoImage.transform.parent, false);
            var target = videoImage.rectTransform;
            var rt = poster.GetComponent<RectTransform>();
            rt.anchorMin = target.anchorMin;
            rt.anchorMax = target.anchorMax;
            rt.pivot = target.pivot;
            rt.anchoredPosition = target.anchoredPosition;
            rt.sizeDelta = target.sizeDelta;
            rt.localScale = target.localScale;
            poster.transform.SetSiblingIndex(videoImage.transform.GetSiblingIndex() + 1);
            _posterImage = poster.GetComponent<RawImage>();
            _posterImage.texture = texture;
            _posterImage.raycastTarget = false;
        }

        private void CacheDimOverlay()
        {
            var dim = overlay != null ? overlay.transform.Find("半黑遮罩") : null;
            if (dim == null) return;
            _dimOverlay = dim.GetComponent<Image>();
            if (_dimOverlay != null) _dimOverlayColor = _dimOverlay.color;
        }

        private void SetDimOverlayVisible(bool visible)
        {
            if (_dimOverlay == null) return;
            var color = _dimOverlayColor;
            color.a = visible ? _dimOverlayColor.a : 0f;
            _dimOverlay.color = color;
        }

        /// <summary>隐藏引导期间需暂隐的对象：禁用对象及子级 Graphic，保证对白框文字不会残留；无 Graphic 才 SetActive(false)。</summary>
        private void HideWhilePlaying()
        {
            if (hideWhilePlaying == null) return;
            _hiddenActive = new bool[hideWhilePlaying.Length];
            _hiddenGraphics = new Graphic[hideWhilePlaying.Length][];
            _hiddenGraphicEnabled = new bool[hideWhilePlaying.Length][];
            for (int i = 0; i < hideWhilePlaying.Length; i++)
            {
                var go = hideWhilePlaying[i];
                if (go == null) continue;
                _hiddenActive[i] = go.activeSelf;
                var graphics = go.GetComponentsInChildren<Graphic>(true);
                if (graphics.Length == 0)
                {
                    go.SetActive(false);
                    continue;
                }
                _hiddenGraphics[i] = graphics;
                _hiddenGraphicEnabled[i] = new bool[graphics.Length];
                for (var j = 0; j < graphics.Length; j++)
                {
                    _hiddenGraphicEnabled[i][j] = graphics[j].enabled;
                    graphics[j].enabled = false;
                }
            }
        }

        /// <summary>恢复引导前被隐藏的对象（还原原状态）。</summary>
        private void RestoreWhilePlaying()
        {
            if (hideWhilePlaying == null) return;
            for (int i = 0; i < hideWhilePlaying.Length; i++)
            {
                var go = hideWhilePlaying[i];
                if (go == null) continue;
                var graphics = _hiddenGraphics != null && i < _hiddenGraphics.Length ? _hiddenGraphics[i] : null;
                if (graphics == null)
                {
                    if (_hiddenActive != null && i < _hiddenActive.Length && _hiddenActive[i]) go.SetActive(true);
                    continue;
                }
                var enabled = _hiddenGraphicEnabled[i];
                for (var j = 0; j < graphics.Length; j++)
                    if (graphics[j] != null) graphics[j].enabled = enabled != null && j < enabled.Length && enabled[j];
            }
        }

        /// <summary>合并场景已配置对象与运行时路径发现结果，保证旧场景不重跑 Setup 也能隐藏完整数字人区域。</summary>
        private void EnsureHideTargets()
        {
            var targets = new List<GameObject>();
            if (hideWhilePlaying != null)
                foreach (var target in hideWhilePlaying)
                    if (target != null && !targets.Contains(target)) targets.Add(target);
            AddHideTarget(targets, hideStagePath);
            AddHideTarget(targets, hideDialoguePath);
            hideWhilePlaying = targets.ToArray();
        }

        private static void AddHideTarget(List<GameObject> targets, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var target = GameObject.Find(path);
            if (target != null && !targets.Contains(target)) targets.Add(target);
        }

        private void OnDestroy()
        {
            WebGlVideoPlaybackGate.IntroActive = false;
            if (player != null)
            {
                player.loopPointReached -= OnVideoEnd;
                player.prepareCompleted -= OnPrepared;
                player.errorReceived -= OnVideoError;
                player.frameReady -= OnFrameReady;
            }
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
            }
        }
    }
}
