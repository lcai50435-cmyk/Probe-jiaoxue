using System.Collections;
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

        [Tooltip("预解码超时兜底（秒）：超过仍未准备好则直接播放")]
        public float prepareTimeout = 5f;

        private RenderTexture _rt;
        private bool _firstTime;
        private bool _started;
        private bool _finished;

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

            _firstTime = PlayerPrefs.GetInt(seenPrefsKey, 0) == 0;
            var clip = player != null ? player.clip : null;
            if (clip == null)
            {
                Debug.LogError("[M1IntroVideo] 未配置 VideoClip，引导动画无法播放。请检查 Setup 是否正确执行。");
                return;
            }

            // 视频渲染到 RenderTexture，再由 RawImage 显示（保证视频层叠在遮罩之上）
            _rt = new RenderTexture((int)clip.width, (int)clip.height, 0);
            player.targetTexture = _rt;
            videoImage.texture = _rt;
            player.loopPointReached += OnVideoEnd;
            player.prepareCompleted += OnPrepared;

            // 场景加载即冻结游戏 + 后台预解码：避免玩家在准备期间操作，也避免播放开头卡顿
            Time.timeScale = 0f;
            player.Prepare();
            StartCoroutine(PrepareTimeout());
        }

        /// <summary>引导播放期间强制冻结附带视频：VideoPlayer 不受 timeScale=0 影响，Presenter 可能在 Start 后重新播放，故每帧保持暂停。</summary>
        private void Update()
        {
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

            TryPlay(); // 若已准备好立即播放；否则等 prepareCompleted
        }

        private void OnPrepared(VideoPlayer vp)
        {
            TryPlay();
        }

        /// <summary>预解码超时兜底：Prepare 长时间未完成（解码异常）时强制播放，避免永久黑屏。</summary>
        private IEnumerator PrepareTimeout()
        {
            yield return new WaitForSecondsRealtime(prepareTimeout); // 不受 timeScale=0 影响
            TryPlay();
        }

        private void TryPlay()
        {
            if (_started || _finished) return;
            _started = true;
            player.Play();
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
            player.Stop();
            // 恢复引导期间被冻结的视频（数字人等）：从暂停位置继续播放
            if (pauseWhilePlaying != null)
                foreach (var p in pauseWhilePlaying)
                    if (p != null && p.isPaused) p.Play();
            overlay.SetActive(false);
            Time.timeScale = 1f; // 恢复游戏
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.loopPointReached -= OnVideoEnd;
                player.prepareCompleted -= OnPrepared;
            }
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
            }
        }
    }
}
