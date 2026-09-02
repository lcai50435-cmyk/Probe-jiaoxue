using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace M5
{
    /// <summary>M5 无操作帮助：统一右下提示，并演示一次完整擦拭。</summary>
    public class M5IdleHelp : MonoBehaviour
    {
        public M5FlowController flow;
        public M5RagDrag ragDrag;
        public float idleTimeout = 60f, demoDuration = 1f;
        private GameObject _panel;
        private TMP_Text _text;
        private bool _demoRunning, _paused;
        private float _idle;

        public void Initialize(M5FlowController owner)
        {
            flow = owner; ragDrag = owner.ragDrag;
            CreatePanel(owner.transform, owner.instructionText != null ? owner.instructionText.font : null);
            _panel.SetActive(false);
        }

        private void Update()
        {
            if (_paused || _demoRunning || flow == null || flow.CurrentStage != M5FlowController.Stage.Wipe) return;
            _idle += Time.deltaTime;
            if (_idle >= idleTimeout && _panel != null && !_panel.activeSelf) { _text.text = "需要帮助吗？"; _panel.SetActive(true); _idle = 0f; }
        }

        public void SetPaused(bool value) { _paused = value; }
        public void ResetIdle() { _idle = 0f; if (_panel != null && !_demoRunning) _panel.SetActive(false); }
        public void ResetAll() { StopAllCoroutines(); _demoRunning = false; _idle = 0f; if (_panel != null) _panel.SetActive(false); }
        private void TryAgain() { if (_panel != null) _panel.SetActive(false); _idle = 0f; }
        private void AutoDemo() { if (!_demoRunning) StartCoroutine(DemoRoutine()); }

        private IEnumerator DemoRoutine()
        {
            _demoRunning = true; if (_panel != null) _panel.SetActive(false);
            if (ragDrag != null) ragDrag.AutoSetProgress(0f);
            for (var t = 0f; t < demoDuration; t += Time.unscaledDeltaTime)
            { ragDrag?.AutoSetProgress(t / demoDuration); yield return null; }
            ragDrag?.AutoSetProgress(1f);
            _demoRunning = false; _idle = 0f;
        }

        private void CreatePanel(Transform parent, TMP_FontAsset font)
        {
            _panel = new GameObject("~M5HelpPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _panel.hideFlags = HideFlags.DontSave; _panel.transform.SetParent(parent, false);
            _panel.GetComponent<Image>().color = new Color(.97f, .98f, .985f, .95f);
            _text = AddText("HelpText", _panel.transform, "需要帮助吗？", font, 30f);
            AddButton("NeedButton", _panel.transform, "需要", new Vector2(-300f, 0f), new Color(.08f, .42f, .66f), AutoDemo, font);
            AddButton("NoNeedButton", _panel.transform, "不需要", new Vector2(-120f, 0f), new Color(.58f, .61f, .65f), TryAgain, font);
            ModuleHintOverlay.MoveHelpPanel(_panel);
        }

        private static TMP_Text AddText(string name, Transform parent, string value, TMP_FontAsset font, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(0f, .5f); rt.anchorMax = new Vector2(1f, .5f); rt.offsetMin = new Vector2(24f, 0f); rt.offsetMax = new Vector2(-560f, 0f);
            var text = go.GetComponent<TextMeshProUGUI>(); text.text = value; text.font = font; text.fontSize = size; text.alignment = TextAlignmentOptions.MidlineLeft; text.color = new Color(.12f, .15f, .18f); return text;
        }

        private static void AddButton(string name, Transform parent, string value, Vector2 pos, Color color, UnityEngine.Events.UnityAction action, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(1f, .5f); rt.pivot = new Vector2(1f, .5f); rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(160f, 60f);
            var image = go.GetComponent<Image>(); image.color = color; var button = go.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            var text = AddText("Text", go.transform, value, font, 26f); text.alignment = TextAlignmentOptions.Center; text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
