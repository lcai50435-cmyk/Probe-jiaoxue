using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_WEBGL && !UNITY_EDITOR
using WeChatWASM;
#endif

namespace M1
{
    /// <summary>M5 完成后仅在下一次离开并返回应用时重开 M1；中途切后台保留当前模块。</summary>
    public sealed class ExperienceReplayOnResume : MonoBehaviour
    {
        private const string IntroSeenKey = "M1_Intro_Seen";
        private static ExperienceReplayOnResume _instance;
        private bool _completed;
        private bool _leftAfterCompletion;
        private bool _restarting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (_instance != null) return;
            var host = new GameObject("~ExperienceReplayOnResume") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<ExperienceReplayOnResume>();
        }

        public static void MarkCompleted()
        {
            if (_instance == null) Create();
            _instance._restarting = false;
            _instance._completed = true;
        }

        public static void CancelCompleted()
        {
            if (_instance == null) return;
            _instance._completed = false;
            _instance._leftAfterCompletion = false;
        }

        private void OnEnable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WX.OnHide(OnWxHide);
            WX.OnShow(OnWxShow);
#endif
        }

        private void OnDisable()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WX.OffHide(OnWxHide);
            WX.OffShow(OnWxShow);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void OnWxHide(GeneralCallbackResult _) => MarkLeft();
        private void OnWxShow(OnShowListenerResult _) => TryRestart();
#endif

        private void OnApplicationPause(bool paused)
        {
#if !(UNITY_WEBGL && !UNITY_EDITOR)
            if (paused) MarkLeft();
            else TryRestart();
#endif
        }

        private void OnApplicationFocus(bool focused)
        {
#if !(UNITY_WEBGL && !UNITY_EDITOR)
            if (!focused) MarkLeft();
            else TryRestart();
#endif
        }

        private void MarkLeft()
        {
            if (_completed) _leftAfterCompletion = true;
        }

        private void TryRestart()
        {
            if (SceneManager.GetActiveScene().name != "M5")
            {
                _completed = false;
                _leftAfterCompletion = false;
                return;
            }

            if (!_completed || !_leftAfterCompletion || _restarting) return;
            _restarting = true;
            _completed = false;
            _leftAfterCompletion = false;
            Time.timeScale = 1f;
            PlayerPrefs.DeleteKey(IntroSeenKey);
            PlayerPrefs.Save();
            Debug.Log("[ExperienceReplayOnResume] M5 完成后返回，清除首次标记并重载 M1 重播引导。");
            SceneManager.LoadScene("M1");
        }
    }
}
