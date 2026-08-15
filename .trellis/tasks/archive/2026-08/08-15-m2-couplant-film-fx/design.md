# M2 耦合剂蓝色铁轨薄膜动画 技术设计

## 1. 设计原则

动画职责独立化（老板确认 B 方案）：新增 `M2CouplantFx` 组件作为薄膜动画的唯一所有者；`M2FlowController` 保持流程状态唯一所有者。组件由 Flow 在 `Awake` 运行时 `AddComponent` 并注入引用，**不写回冻结 Scene**。

复用优先：
- 薄膜形状 = `俯视角.png` 的既有 sprite（`RailBackground/bg` 已序列化引用同一 sub-sprite，`m_PreserveAspect: 0`）。
- 对齐 = 同步 `CouplantMask` 的 rect 到 `RailBackground`（同父级 `RailViewport`，均为 pivot 0.5）。
- 揭示 = UGUI `Image.type=Filled`（Horizontal / Origin Left / fillAmount 0→1），原生从左至右扫过，无需 pivot/scale 补偿。

## 2. 组件职责

### 新增 `Assets/Scripts/M2CouplantFx.cs`（~80 行）

```csharp
public class M2CouplantFx : MonoBehaviour
{
    public RectTransform railBg;          // 对齐基准（RailBackground）
    public RectTransform maskRt;          // CouplantMask（rect 同步对象）
    public Image film;                    // CouplantOverlay/bg 的 Image
    public CanvasGroup group;             // CouplantOverlay 的 CanvasGroup
    public Color filmColor = 半透明蓝;     // 默认 (0.45,0.75,0.95,0.6) 附近，可配
    public float animDuration = 2f;       // 老板确认 2s
    public float holdDuration = 0.5f;     // 老板确认 0.5s
    public float fadeDuration = 0.5f;     // 老板确认默认 0.5s
    private bool _setup, _playing;

    public void Bind(RectTransform rail, RectTransform mask, Image image, CanvasGroup cg); // 注入引用
    public void Play(System.Action onDone);   // 幂等，_playing 防重入
    public void Reset();                      // 停止协程、fillAmount=0、alpha=1、隐藏 mask
    private void Setup();                     // 首次调用：同步 rect + sprite + Filled + color
    private IEnumerator Anim(System.Action onDone);
}
```

- `Setup()`（幂等，`Play` 开头调用）：`maskRt.anchoredPosition/sizeDelta/pivot = railBg 对应值`；`film.sprite = Resources.LoadAll<Sprite>("俯视角")[0]`；`film.type = Filled; fillMethod = Horizontal; fillOrigin = 0; fillAmount = 0`；`film.color = filmColor`；`group.alpha = 1`。
- `Anim` 时序：
  1. `fillAmount` 0→1，步进 `Time.deltaTime / animDuration`（2s，从左至右铺满）；
  2. `yield return new WaitForSeconds(holdDuration)`（0.5s 完整覆盖停留）；
  3. `group.alpha` 1→0，步进 `Time.deltaTime / fadeDuration`（0.5s 淡出）；
  4. `maskRt.gameObject.SetActive(false)`；`_playing = false`；`onDone?.Invoke()`。
- 暂停合同：全链路 scaled time（`Time.deltaTime` + `WaitForSeconds`）。`Time.timeScale=0` 时 `deltaTime=0` 不推进、`WaitForSeconds` 挂起，QA/Modal 暂停期间动画冻结，恢复后继续。符合 AC5。

### `M2FlowController.cs` 改动（145 → ~135 行）

- 删：`CouplantAnim` 协程（~17 行）、`couplantAnimDuration` 字段（迁出）。
- 加：`public M2CouplantFx couplantFx;` 字段；`Awake` 末尾运行时挂载：
  ```csharp
  couplantFx = gameObject.AddComponent<M2CouplantFx>();
  var film = couplantOverlay != null ? couplantOverlay.GetComponentInChildren<Image>(true) : null;
  var cg = couplantOverlay != null ? couplantOverlay.GetComponent<CanvasGroup>() : null;
  couplantFx.Bind(railBg, couplantMask != null ? couplantMask.GetComponent<RectTransform>() : null, film, cg);
  ```
- `ApplyCouplant()`：门控不变，`couplantFx.Play(OnCouplantDone)` 替换 `StartCoroutine(CouplantAnim())`。
- 新 `OnCouplantDone()`：`_applying=false; CouplantApplied=true; applyButtonText="已涂抹"; probeDrag?.Unlock(); Go(Stage.Positioning);`（原协程尾逻辑）。
- `ResetAll()`：加 `couplantFx?.Reset()`（与现有 `couplantMask.SetActive(false)` 保留其一即可，Reset 已含隐藏）。

## 3. 对齐与视觉细节

- `CouplantMask`（891×220 居中）与 `RailBackground`（960.5×286，offset -24,-33）同父级 → 同步 `anchoredPosition/sizeDelta/pivot` 后，stretch 子节点 `CouplantOverlay`/`bg` 自动贴合，薄膜与铁轨精确重合。
- `RailBackground/bg` 的 Image：`m_PreserveAspect: 0`（拉伸填满）。薄膜 Image 同样不设 preserveAspect → 二者拉伸规则一致，重合不受分辨率影响。
- 薄膜有效 alpha 区域 = 铁轨主体 + 两轨间空隙（实心块，上下/左右边缘贴合铁轨轮廓），染半透明蓝后呈「蓝色铁轨薄膜」观感。
- 透视视图：耦合剂阶段 `ApplyView(false)` 固定普通视图，不涉及。

## 4. 行数核算

| 文件 | 当前 | 变动 | 目标 |
|---|---|---|---|
| M2CouplantFx.cs（新增） | — | +~80 | ≤150 ✓ |
| M2FlowController.cs | 145 | -17（协程）-1（字段）+1（字段）+~8（Awake/回调/Reset） | ~135 ✓ |

## 5. 烟测适配（Editor，行数豁免）

- 快进：`_flow.couplantFx.animDuration = _flow.couplantFx.holdDuration = _flow.couplantFx.fadeDuration = 0f;` 后 `ApplyCouplant()`，断言进入 Positioning。
- 新增断言：薄膜 `film.sprite != null`、`fillMethod == Horizontal`、`fillOrigin == 0`、`color` 为半透明蓝（a<1）。
- Reset 断言：再跑一次烟测（现有复跑用例）覆盖 `couplantFx.Reset` 复位。

## 6. 风险与回滚

- **Scene 污染**：实施前后计算 `M2.unity` SHA-256（基线 `1610da8a14fff92138bfc0554946d7bc909e793c8f05e6126bf8bdd9b61d209a`，= HEAD）；变化立即停止定位写入者。
- **sprite 加载失败**：`Resources.LoadAll<Sprite>("俯视角")` 为空时 `Debug.LogError` 并跳过动画（不得动态创建视觉节点）。
- **挂载顺序**：`AddComponent` 在 `Awake`，依赖 `couplantOverlay/couplantMask/railBg` 引用已序列化（Flow 现有字段）；引用缺失时 LogError 不崩溃。
- **回滚点**：新建组件 → Flow 改造 → 烟测更新，逐块验证；失败只回滚本任务代码。
