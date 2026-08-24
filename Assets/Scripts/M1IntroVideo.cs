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
            // 运行时兜底：补齐全身数字人与白板对白框，防旧场景未重跑 Setup 时在半黑遮罩下透出。
            EnsureHideTargets();
            // 运行时兜底：Setup 未注入字幕引用时，按路径自动发现（防引导视频无字幕）
            if (subtitleText == null && !string.IsNullOrEmpty(subtitlePath))
            {
                var sub = GameObject.Find(subtitlePath);
                if (sub != null) subtitleText = sub.GetComponent<TextMeshProUGUI>();
            }
            // 运行时兜底二：场景无字幕节点时动态创建（挂遮罩底部，字体从跳过按钮复制），保证不跑 Setup 也显示字幕
            if (subtitleText == null) subtitleText = CreateRuntimeSubtitle();
            // 运行时兜底：引导视频等比缩小（数字人变小；Setup 已设 0.78 则不覆盖）
            if (videoImage != null && Mathf.Approximately(videoImage.rectTransform.localScale.x, 1f))
                videoImage.rectTransform.localScale = new Vector3(introVideoScale, introVideoScale, 1f);
            if (videoImage != null && videoImage.material != null && videoImage.material.HasProperty("_RemoveGreenGuide"))
                videoImage.material.SetFloat("_RemoveGreenGuide", removeGreenGuide ? 1f : 0f);

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
            player.audioOutputMode = VideoAudioOutputMode.None; // 老板 2026-08-18：引导视频静音，解说词改字幕（防场景旧序列化 Direct 覆盖）

            // 场景加载即冻结游戏 + 后台预解码：避免玩家在准备期间操作，也避免播放开头卡顿
            Time.timeScale = 0f;
            player.Prepare();
            StartCoroutine(PrepareTimeout());
        }

        /// <summary>引导播放期间强制冻结附带视频：VideoPlayer 不受 timeScale=0 影响，Presenter 可能在 Start 后重新播放，故每帧保持暂停。同时按播放进度切换字幕。</summary>
        private void Update()
        {
            UpdateSubtitle();
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

            // 视频可播放才隐藏常驻数字人（避免半黑遮罩两侧透出）；缺失时不隐藏，防止播放逻辑不触发导致永久消失
            if (player != null && player.clip != null)
            {
                HideWhilePlaying();
                TryPlay(); // 若已准备好立即播放；否则等 prepareCompleted
            }
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
            RestoreWhilePlaying(); // 引导结束：恢复常驻数字人显示
            if (subtitleText != null) subtitleText.text = ""; // 字幕清空
            overlay.SetActive(false);
            Time.timeScale = 1f; // 恢复游戏
        }

        /// <summary>按视频播放进度切换字幕分段；未到第一段或已结束时清空。</summary>
        private void UpdateSubtitle()
        {
            if (_finished || subtitleText == null || player == null || subtitleSegments == null || subtitleSegments.Length == 0) return;
            var t = player.time;
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
            trt.anchorMin = new Vector2(0.5f, 0f);
            trt.anchorMax = new Vector2(0.5f, 0f);
            trt.pivot = new Vector2(0.5f, 0f);
            trt.anchoredPosition = new Vector2(0f, 16f);
            trt.sizeDelta = new Vector2(1200f, 100f);
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 28;
            tmp.alignment = TextAlignmentOptions.Bottom; // 垂直底部对齐：文字贴底显示，与数字人脚部错开
            tmp.enableWordWrapping = false; // 2026-08-18：单行显示，不换行
            tmp.color = Color.white;
            if (skipButton != null)
            {
                var src = skipButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (src != null && src.font != null) tmp.font = src.font; // 复用跳过按钮同款中文字体
            }
            var ol = textGo.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.9f);
            ol.effectDistance = new Vector2(2f, -2f);
            var sh = textGo.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.7f);
            sh.effectDistance = new Vector2(2f, -3f);
            return tmp;
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
