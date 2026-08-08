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

        [Tooltip("预解码超时兜底（秒）：超过仍未准备好则直接播放")]
        public float prepareTimeout = 5f;

        private RenderTexture _rt;
        private bool _firstTime;
        private bool _started;
        private bool _finished;

        private void Awake()
        {
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
