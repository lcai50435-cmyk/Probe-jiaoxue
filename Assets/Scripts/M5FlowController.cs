using System.Linq;
using M2;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace M5
{
    /// <summary>
    /// M5 擦拭耦合剂流程：Wipe(玩家拖擦拭布从左到右擦，进度跟手) → Completed(结束模块)。
    /// 复用 M2 UGUI 骨架与行为（Reset 确认弹窗、普通/透视视图切换、完成面板）；无探测流程、无下一模块。
    /// 耦合剂视觉走 M5CouplantFx（初始铺满，擦拭 fillAmount 递减）。
    /// </summary>
    public class M5FlowController : MonoBehaviour
    {
        public enum Stage { Wipe, Completed }
        public M5RagDrag ragDrag;
        [System.NonSerialized] public ModuleSpeechBubble speechBubble; // 数字人台词气泡（运行时挂载）
        public M5CouplantFx couplantFx;            // Scene 序列化（M5 未冻结）
        public RectTransform railBg, couplantOverlay;
        public GameObject railPerspective, completionPanel;
        public Image normalBtnImg, perspectiveBtnImg;
        public TMP_Text instructionText, stepProgressText, completionText;
        public Button resetButton, enterNextButton;
        public GameObject[] stepPanels;
        public AudioSource sfx;
        public AudioClip correctClip;
        public UnityEvent onCompleted;
        public Stage CurrentStage { get; private set; } = Stage.Wipe;
        public bool Wiped;
        private float _timeScaleBeforeDialog = 1f;
        private static readonly string[] DefaultHints = { "请将擦拭布拖至钢轨顶面，由左至右擦拭" };
        private static readonly string[] StageNames = { "擦拭耦合剂" };
        private const string InitialSpeech = "根据《安规》规定“\n焊缝探伤作业后钢轨顶面上的焊缝探伤耦合剂必须擦除干净。”";
        private const string CompletedSpeech = "恭喜你！完整掌握了“三位一体、交叉验证”新工艺！";

        private void Awake()
        {
            Bind(resetButton, ShowResetDialog);
            Bind(FindButton("ConfirmButton"), ResetAll);
            Bind(FindButton("CancelButton"), HideResetDialog);
            Bind(FindButton("NormalButton"), SetNormalView);
            Bind(FindButton("PerspectiveButton"), SetPerspectiveView);
            ragDrag?.Bind(this);
            couplantFx?.Init();
            SwapRailSprites();
            ApplyView(false);
            UpdateUi();
            // 复用 M2-M4 的场景云朵：仅运行时创建文字，不改 M5 Scene。
            speechBubble = gameObject.AddComponent<ModuleSpeechBubble>();
            speechBubble.segmentInterval = 1f;
            if (instructionText != null) speechBubble.SetFont(instructionText.font);
            var stage = FindDeep(transform, "DigitalHumanStage");
            var dialog = stage != null ? FindDeep(stage, "dialog") : null;
            if (dialog != null)
            {
                speechBubble.SetAnchor(dialog);
                speechBubble.useExistingCloud = true;
                speechBubble.anchorOffset = new Vector2(-384f, 1f);
                speechBubble.bubbleSize = new Vector2(264f, 198f);
                speechBubble.Show(InitialSpeech);
            }
            else speechBubble.createOnlyWhenAnchored = true;
        }

        private Button FindButton(string name) => GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name);
        private static void Bind(Button button, UnityAction action) { if (button != null) { button.onClick.RemoveListener(action); button.onClick.AddListener(action); } }

        /// <summary>擦拭进度回调（M5RagDrag 拖动时）：耦合剂递减；到达 100% 触发完成。</summary>
        public void NotifyWipeProgress(float p)
        {
            if (CurrentStage != Stage.Wipe) return;
            couplantFx?.SetWipeProgress(p);
            if (p >= 1f - .001f) CompleteWipe();
        }

        private void CompleteWipe()
        {
            if (Wiped || CurrentStage != Stage.Wipe) return;
            Wiped = true;
            ragDrag?.SetInputLocked(true); // 擦完锁定
            if (sfx != null && correctClip != null) sfx.PlayOneShot(correctClip);
            Go(Stage.Completed);
        }

        public void ShowResetDialog() => SetDialog(true);
        public void HideResetDialog() => SetDialog(false);
        private void SetDialog(bool visible)
        {
            var modal = FindDeep(transform, "ModalLayer")?.gameObject;
            var wasOpen = modal != null && modal.activeSelf;
            if (visible && !wasOpen) { _timeScaleBeforeDialog = Time.timeScale; Time.timeScale = 0f; }
            else if (!visible && wasOpen) Time.timeScale = _timeScaleBeforeDialog;
            if (modal != null) modal.SetActive(visible);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                var hit = FindDeep(child, name); if (hit != null) return hit;
            }
            return null;
        }

        public void SetNormalView() => ApplyView(false);
        public void SetPerspectiveView() => ApplyView(true);

        private void SwapRailSprites()
        {
            SwapSprite(railBg != null ? railBg.GetComponentInChildren<Image>(true) : null, "俯视角");
            SwapSprite(railPerspective != null ? railPerspective.GetComponentInChildren<Image>(true) : null, "俯视角透视");
        }
        private static void SwapSprite(Image image, string name) { if (image == null) return; var s = Resources.LoadAll<Sprite>(name); if (s != null && s.Length > 0) image.sprite = s[0]; }

        private void ApplyView(bool on)
        {
            if (railBg != null) railBg.gameObject.SetActive(!on);
            if (railPerspective != null) railPerspective.SetActive(on);
            if (couplantOverlay != null) couplantOverlay.gameObject.SetActive(!on); // 透视视图无耦合剂层（擦拭发生在普通视图）
            Color selected = new Color(.08f, .42f, .66f), idle = new Color(.58f, .61f, .65f);
            if (normalBtnImg != null) { normalBtnImg.color = on ? idle : selected; SetButtonText(normalBtnImg, on ? new Color(.12f, .15f, .18f) : Color.white); }
            if (perspectiveBtnImg != null) { perspectiveBtnImg.color = on ? selected : idle; SetButtonText(perspectiveBtnImg, on ? Color.white : new Color(.12f, .15f, .18f)); }
        }
        private static void SetButtonText(Image image, Color color) { if (image == null) return; var text = image.GetComponentInChildren<TMP_Text>(true); if (text != null) text.color = color; }

        public void ResetAll()
        {
            Wiped = false;
            ragDrag?.ResetTool();
            couplantFx?.Reset();
            ApplyView(false); SetDialog(false); Go(Stage.Wipe);
            speechBubble?.Show(InitialSpeech);
        }

        private void Go(Stage stage)
        {
            CurrentStage = stage;
            if (stage == Stage.Completed)
            {
                onCompleted?.Invoke();
                speechBubble?.Show(CompletedSpeech);
            }
            UpdateUi();
        }

        private void UpdateUi()
        {
            var i = Mathf.Min((int)CurrentStage, StageNames.Length - 1);
            var done = CurrentStage == Stage.Completed;
            // 擦拭完成（Completed）后不显示步骤提示词（老板 2026-08-23 定稿）
            if (instructionText != null) instructionText.text = done ? string.Empty : DefaultHints[i];
            if (stepProgressText != null) stepProgressText.text = done ? string.Empty : $"步骤1：{StageNames[i]}"; // 2026-08-23 按 台词.pptx：去 /1、中文冒号，改“步骤1：阶段名”
            foreach (var panel in stepPanels ?? new GameObject[0]) if (panel != null) panel.SetActive(panel == stepPanels[i % stepPanels.Length]);
            if (completionPanel != null) completionPanel.SetActive(false); // 老板 2026-08-23：完成面板（"M5 擦拭耦合剂完成"）不显示
            if (enterNextButton != null) enterNextButton.gameObject.SetActive(false); // 结束模块：无下一模块按钮
            if (done && completionText != null) completionText.text = "M5 擦拭耦合剂完成";
        }
    }
}
