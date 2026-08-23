# Design：M5 擦拭耦合剂 = 复用 M2 UGUI 骨架 + 单步擦拭交互

## 1. 总策略

老板定稿：M5 仅"擦拭耦合剂"单步交互，结束模块。**UGUI 复用 M2 骨架与视觉令牌**（非复制冻结的 M2.unity——M2 已冻结，且 M5 无探测流程，复制后需删除大量流程节点，YAML 手术风险高）。M5 是未冻结模块，走项目既定路径：**M5Setup.cs 从零生成精简 M5.unity**（参照 M1Setup 生成器 + ugui-module-template.md 视觉令牌），复用 M2 素材（钢轨）与耦合剂视觉（铁轨形状蓝色薄膜）。

```
M2 视觉骨架/令牌/素材 ──复用──> M5Setup.cs ──生成──> M5.unity
M2CouplantFx Sprite 切割思路 ──复刻──> M5CouplantFx.cs（铺满 + 擦拭递减）
rag.png（m4 分支并入）──> 工具架新增槽位 + M5RagDrag.cs
M3DigitalHumanBootstrap（可选）──> 扩展场景名 M5 装配 QA/数字人
```

## 2. Scene 结构（M5Setup 生成）

复用 M2 UGUI 骨架（ugui-module-template.md §2），仅保留擦拭交互所需层级：

```
Canvas (1920x1080 / Match 0.5)
└── SafeArea
    ├── Background                     # 页面浅灰 #ECEEF1
    ├── HeaderBar (80px)
    │   ├── ModuleTitle                # "M5 擦拭耦合剂" Primary 36px
    │   └── ResetButton
    ├── MainScene
    │   └── RailArea
    │       ├── ToolShelf (372x88 左上) # 含 擦拭布槽位 RagHome（新增）
    │       ├── RailViewport           # 白色教学面
    │       │   ├── RailBackground     # 俯视角.png（普通视图钢轨，复用 M2 素材）
    │       │   ├── CouplantOverlay    # CanvasGroup + CouplantMask(Image Filled)
    │       │   ├── RailPerspective    # 俯视角透视.png（透视视图，无耦合剂层）
    │       │   └── Rag                # 擦拭布（初始在 RagHome，工作态挂 RailViewport）
    │       └── PerspectiveBar_C (364x64) # 普通视图/透视视图分段（复用 M2 行为）
    ├── ControlDock_D (176px)
    │   ├── InstructionArea            # 步骤提示文字
    │   └── StepProgress               # "步骤 1/1 · 擦拭耦合剂"
    ├── QALayer                        # 可选：QA 面板（默认复用 Bootstrap 装配）
    ├── DigitalHumanStage              # 可选：数字人（默认复用 Bootstrap 装配）
    └── ModalLayer                     # Reset 确认弹窗
```

不保留：WaveformArea_B（无探测）、Probe/Ruler 工具与流程节点、WeldLine 等。

## 3. Runtime 脚本（均 ≤150 行，namespace M5）

### 3.1 M5FlowController.cs（~120 行）

- `enum Stage { Wipe, Completed }`；字段：`ragDrag`、`couplantFx`、`railBg`、`couplantOverlay/mask`、`normalBtnImg/perspectiveBtnImg`、`instructionText/stepProgressText/completionText`、`resetButton/enterNextButton`、`stepPanels`、`sfx/correctClip`、`onCompleted`。
- Awake：绑定 Reset/普通/透视按钮（幂等 RemoveListener+AddListener）；`ragDrag.Bind(this)`；初始 `couplantFx.Init()`（铺满 fillAmount=1）；`UpdateUi()`。
- `NotifyRagOut()`：擦拭布进入钢轨顶面（Wipe 阶段提示不变或更新）。
- `NotifyWipeProgress(float p)`：`couplantFx.SetWipeProgress(p)`；p 首次 >0 时播放音效（可选）；p≥1 时 `CompleteWipe()`。
- `CompleteWipe()`：锁定（`ragDrag.SetInputLocked(true)`）+ 正确音效 + `Go(Stage.Completed)`。
- Reset：`ragDrag.ResetTool()`、`couplantFx.Reset()`、恢复普通视图、`Go(Stage.Wipe)`。
- 普通/透视切换：复用 M2 行为（railBg vs railPerspective 显隐 + 按钮着色）；透视时耦合剂层隐藏。
- 完成面板：`completionText.text = "M5 擦拭耦合剂完成"`（结束模块，enterNextButton 不显示）。
- 文案：`DefaultHints = { "请将擦拭布拖至钢轨顶面，由左至右擦拭" }`，`StageNames = { "擦拭耦合剂" }`。

### 3.2 M5RagDrag.cs（~130 行）

参照 M2RulerDrag 拖拽模式（IBeginDragHandler/IDragHandler）：

- 两态：`Mode.Home`（父级=RagHome 槽位，置灰锁定 `color=(.55,.57,.6,.62)`）→ `Mode.Wiping`（父级=RailViewport、锚定 railViewport.pivot、跟手、正常色）。
- `Bind(M5FlowController owner)`：缓存 Scene Home 初态（位置/尺寸/颜色），订阅事件。
- `OnBeginDrag`：Home 态解锁后拖出进入工作态（`EnterWorkFromPointer`，同 M2RulerDrag 模式）；Wiping 态直接跟手。
- `OnDrag`：Wiping 态 x 限制在钢轨顶面擦拭区间 `[left, right]`（railViewport 局部像素），y 贴顶面中心线；`progress = clamp((x-left)/(right-left))`；`flow.NotifyWipeProgress(progress)`；`idleHelp.ResetIdle()`（如用）。
- `SetInputLocked(bool)`：锁拖拽；`ResetTool()`：恢复 Home 初态。
- 擦拭区间字段 `wipeLeftUv/wipeRightUv`（相对 railBg 底左归一化，默认 M2CouplantFx coverRect 的 x 范围 `0.005~0.993` 对应 railViewport 坐标，Inspector 可调）。

### 3.3 M5CouplantFx.cs（~70 行）

复刻 M2CouplantFx 的 Sprite 切割（从 `俯视角.png` 切 coverRect 子矩形，保留铁轨形状与羽化），状态相反：

- `Bind(RailBg, MaskRt, Film, Group)`；字段 `filmColor=(.55,.8,.96,.45)`（M2 同款浅蓝）、`coverRect=(.005,.222,.993,.553)`（铁轨主体，覆盖轨顶中央大部分，老板确认口径）。
- `Init()`：切 Sprite + `fillOrigin=1`（右对齐剩余）+ `fillAmount=1`（铺满）。
- `SetWipeProgress(p)`：`fillAmount = 1 - p`（已擦左侧消失，剩余在右侧）。
- `Reset()`：`fillAmount=1`、alpha=1、mask 激活。

## 4. Editor 工具

| 工具 | 说明 |
|---|---|
| `M5Setup.cs` | 生成 M5.unity（幂等 Ensure，参照 M1Setup 模式）；含 RagHome 槽位、耦合剂层、控制条、完成面板、ModalLayer；素材注入（钢轨 `俯视角.png`/`俯视角透视.png`、擦拭布 rag.png）；`EnsureImage` 只查直接子节点 |
| `M5RuntimeSmoke.cs` | Play Mode 断言：初始耦合剂 fillAmount=1；拖擦拭布进度→fillAmount 递减；100% 后锁定 + Completed + 完成文案；Reset 恢复；普通/透视切换 |
| `M5Shot.cs` | 三视口截图（1920x1080/1280x720/2436x1125），像素差异断言，finally 恢复，不保存 Scene |

## 5. 素材与并入

- `rag.png`（2010×2048 浅灰白超细纤维布）+ `.meta`（guid `1f261a28628cf5848968598f20b6d0c2`，Multiple rag_0）从 `feature/m4-rail-web` 分支并入（`git checkout feature/m4-rail-web -- Assets/probeFootage/rag.png Assets/probeFootage/rag.png.meta`）。
- 钢轨：复用 M2（`俯视角.png` / `俯视角透视.png`，Resources.LoadAll 运行时换图，同 M2 SwapRailSprites 模式）。
- 擦拭布工作态尺寸：按钢轨顶面比例（Inspector 可调，默认约 260×120 左右，最终以老板目视为准）。

## 6. 边界与复用

- M2.unity / M2 脚本 / M2 Editor 三套**只读不写**（实施前后校验 M2.unity SHA-256）。
- M5 新脚本独立（M5 命名空间），不复用 M2 流程机（M2FlowController 是四阶段探测流程，M5 只有擦拭）。
- M2CouplantFx 逻辑（涂抹动画）与 M5（初始铺满+擦拭递减）方向相反，不复用其 Play/Reset，仅复刻 Sprite 切割思路。
- QA/数字人：**老板确认保留全套**——复用 `M3DigitalHumanBootstrap`（`M3DigitalHumanBootstrap.cs` 支持场景名 M3/M4，扩展 M5 场景名装配 QA 面板 + 数字人三态动画 + 长按开关；M5Scene 需提供 QAPanel 壳与 Stage 节点，同 M3/M4 结构）。
- M5 为未冻结模块：Scene 可自由修改/重建，运行时 Bind 覆盖 Scene 旧值不写回。

## 7. 风险

- 擦拭区间几何（coverRect x 范围 → railViewport 像素）若与钢轨渲染位置偏差，擦拭布活动范围会溢出钢轨——像素标定 + Smoke 断言 + 老板目视。
- rag.png 大图（2010×2048）可能被降采样模糊——meta 已配置（m4 分支自带 maxTextureSize 设置），确认无压缩。
- 擦拭进度"跟手"语义：擦拭布中心 x 映射进度；若老板期望"擦拭布左缘"起算，留 Inspector 偏移字段微调。
