# 探测模块 UGUI 统一模板（M2/M3 Scene 冻结基线）

> 状态：Active。2026-08-12 起，当前 `Assets/Settings/Scenes/M2.unity` 与 `M3.unity` 是冻结后的唯一视觉权威；两份 Setup 已缩减为只读打开器，不含生成或保存能力。M4、M5 默认参考本规范，但不得反向改写 M2/M3。
> 适用范围：钢轨探伤教学模块的横屏 UGUI、Editor Setup、自适应截图和数字人/QA 布局。

## 1. 复用边界

复用页面骨架、稳定尺寸、视觉令牌、组件状态和数字人/QA 公共链路，不复制任一模块的业务状态机。

未冻结模块通过自己的幂等 `M{n}Setup.cs` 生成 Scene，并配置模块专属标题、步骤、角度、距离、素材、波形 profile、交互坐标和完成出口。M2/M3 已冻结，后续功能只允许 runtime 动态绑定、复用已有节点或添加不改变视觉的组件。

禁止复制：

- `M2FlowController` 或 M2 的 10°、150→100mm、110mm 合同。
- `M3Setup` 中的静态占位内容作为 runtime 状态机。
- Scene YAML、fileID 或整套 runtime 脚本。
- DeepSeek、QAPanel、数字人 Presenter 和视频控制逻辑副本。
- 参考图中的参数编辑器、密集下拉框或开发工具式表单。

## 2. 权威页面骨架

真实基线直接见冻结的 `Assets/Settings/Scenes/M2.unity` 与 `M3.unity`；Setup 不再定义视觉：

```text
Canvas (1920x1080 / Match 0.5)
└── SafeArea
    ├── Background
    ├── HeaderBar
    │   ├── ModuleTitle
    │   └── ResetButton
    ├── MainScene
    │   ├── RailArea
    │   │   ├── ToolShelf
    │   │   ├── RailViewport
    │   │   └── PerspectiveBar_C
    │   └── SupportArea
    │       └── WaveformArea_B
    ├── ControlDock_D
    │   ├── InstructionArea
    │   ├── PositioningControls / StepControlArea
    │   └── StepProgress
    ├── QALayer
    │   ├── Blocker
    │   └── QAPanel
    ├── DigitalHumanStage
    │   └── FullBodyPreview / FullBodyView
    └── ModalLayer
```

层级顺序固定：

```text
Background < HeaderBar < MainScene < ControlDock_D
           < QALayer < DigitalHumanStage < ModalLayer
```

QA Blocker 不得压暗或拦截数字人，ModalLayer 必须覆盖所有业务层。

## 3. 1920 基准布局

| 项目 | 固定值/范围 |
|---|---:|
| CanvasScaler | Scale With Screen Size |
| Reference Resolution | 1920x1080 |
| Match | 0.5 |
| 页面边距 | 24px |
| 区域间距 | 16px |
| HeaderBar | 80px |
| ControlDock_D | 176px，浅色教学面 |
| SupportArea | 576px |
| DigitalHumanStage | 约 320px 宽，辅助区上约 2/3，右对齐 |
| WaveformArea_B | 460x240，辅助区底部右对齐 |
| ToolShelf | RailArea 左上局部约 372x88，不横贯主场景 |
| PerspectiveBar_C | 364x64，RailArea 左下 |
| 普通触控控件 | 高度 >=64px |
| 主步骤按钮 | 64-72px |

数字人和波形在 SupportArea 内上下组合、右边缘一致，禁止横向并排。宽屏新增空间优先给 RailArea；SupportArea、人物、波形和 Dock 不横向拉伸。1280x720 由 CanvasScaler 整体缩放，不按 viewport 缩放字号。

`RailViewport` 是白色、无装饰边框的主教学面；钢轨、探头、尺子和教学标记是第一视觉信号。`ToolShelf` 只是局部器具暂存区，不得重新做成全宽栏。

## 4. M2/M3 冻结视觉令牌

以下数值记录自老板批准时的冻结 Scene，仅用于 M4/M5 参考；不得用这些数值回写 M2/M3：

| 令牌 | Unity Color | 近似色值 | 用途 |
|---|---|---|---|
| Page | `(0.925, 0.935, 0.945)` | `#ECEEF1` | 页面浅灰背景 |
| Surface | `(0.975, 0.980, 0.985)` | `#F9FAFB` | Header、RailViewport、浅色 Dock |
| Ink | `(0.120, 0.150, 0.180)` | `#1F262E` | 浅底主文字 |
| Muted | `(0.380, 0.420, 0.460)` | `#616B75` | 次级提示、未选文字 |
| Primary | `(0.080, 0.420, 0.660)` | `#146BA8` | 标题、主操作、选中状态 |
| Accent | `(0.930, 0.550, 0.120)` | `#ED8C1F` | 角度、定位和教学强调 |
| Screen | `(0.090, 0.110, 0.120)` | `#171C1F` | 波形仪器底色 |
| ScreenGrid | `(0.420, 0.550, 0.530, 0.220)` | 半透明灰绿 | 波形网格 |
| Wave | `(0.340, 0.920, 0.620)` | `#57EB9E` | 波形和基线 |

红色只用于伤损、错误或危险状态；黄色只用于目标距离和峰值。不得使用深色 Dock、整页单一蓝色、渐变背景、装饰性浮卡或营销式构图。

## 5. 组件合同

### Header

只保留模块标题和“重置流程”。标题使用 Primary、36px、常规字重；不添加返回按钮、文字 QA 入口或参数编辑器。

### ToolShelf

局部工具架位于 RailArea 左上。工具槽为白底、1px 中性描边和浅底深字标签；素材与状态文字由模块替换。不得使用贯穿 RailArea 的工具栏或深色状态条。

### 普通/透视分段控件

- 总尺寸 364x64，两个 182x64 等宽段。
- 选中：Primary + 白字。
- 未选：中性灰 + Ink。
- 切换时背景色和文字色同步更新。
- 只改变显示层，不改变流程状态或交互坐标。

### 可拖测量工具：暂存态与工作态

尺子等可拖工具必须有两套明确状态，禁止把 Scene 中临时摆放坐标同时当作测量坐标：

- 暂存态：父级为模块工具槽（如 `RulerHome`），工具全程可见但置灰锁定；尺寸受槽位约束，标签不得遮挡素材。
- 工作态：进入对应步骤时重挂到 `RailViewport`，恢复配置化测量尺寸并解锁；Reset/完成出口归回工具槽。
- 拖拽坐标统一使用 `RailViewport` 中心原点局部像素：工作态 anchor 固定为 `railViewport.pivot`，`anchoredPosition` 承载位置，禁止混用归一化 anchor 与局部像素造成双重偏移。
- 零刻度若来自 preserveAspect Sprite，必须按实际渲染图像边缘计算，不能硬编码为 `-rect.width/2`：

```csharp
var renderedWidth = Mathf.Min(rect.width, rect.height * sprite.rect.width / sprite.rect.height);
zeroAnchorLocal.x = rect.center.x - renderedWidth * 0.5f;
```

验证矩阵：初态断言 `parent == RulerHome && !unlocked`；工作态断言 `parent == RailViewport && size == measureSize`；吸附断言实际图像左缘与焊缝重合；Reset 再断言归槽。Scene 中用户手调 Rect 改变后，运行时零点仍必须自动适配。

### 波形仪器

`WaveformArea_B` 固定 460x240，使用 Screen 底色、低对比 ScreenGrid 和单条 Wave 曲线。标题区显示波形状态、目标距离和当前距离；主体显示扫描刻度。动态模块可使用自定义 `Graphic`，静态线框可使用 Image 线段，但视觉合同相同。

### ControlDock_D

Dock 使用 Surface 浅底、Ink 主文字、Muted 次文字和 Primary 主操作；教学角度/定位可用 Accent。保持左侧提示、中间当前操作、右侧步骤进度三栏，阶段切换不重排结构。

### 冻结 Scene 的可拖拽工具初态

- 工具的起始位置、尺寸、锚点、Pivot、缩放与旋转以冻结 Scene 当前序列化值为权威；runtime 在首次 `Awake`/绑定时缓存，Reset 恢复缓存，禁止用代码常量覆盖老板手工布局。
- 工具初态必须在 Scene 中直接序列化为 `Home` 的最后子节点，使非 Play Mode 与 Game 首帧一致；runtime 首次绑定只校验父级/sibling 并缓存，禁止用 `SetParent` 或硬编码 Rect 自愈错误 Scene。
- 工具进入业务交互层后可以采用模块配置的测量态布局；退出或 Reset 必须恢复上述 Scene 初态和 Home 内最高 sibling。静态验收需检查 Scene 父子双向引用，Play Mode 验收需断言初态父级/sibling，并在完成一次交互后断言世界位置与尺寸恢复。

### 数字人与 QA

运行时模块复用：

```text
M1DigitalHumanPresenter
+ VideoPlayer
+ RenderTexture
+ RawImage
+ UI-LumaKey-DigitalHuman
+ M1PressDetector
```

QAPanel 从数字人左侧展开；打开时全局暂停，关闭恢复打开前的 `Time.timeScale`。禁止用静态图替换运行时数字人，禁止复制网络或视频逻辑。静态 M3 线框使用全身预览图只用于构图验收，不是 runtime 替代方案。

## 6. 模块替换表

| 配置 | 每个模块必须提供 |
|---|---|
| ModuleTitle | 模块编号与名称 |
| Steps | 步骤数、阶段名、提示、按钮文案 |
| Probe Profile | 探头类型、目标角度与方向 |
| Scan Profile | 起止距离、目标距离、峰值窗口 |
| Visual Assets | 钢轨、探头、尺子、伤损等素材 |
| Waveform Profile | 波形区间、状态文案和数据映射 |
| Interaction | RailViewport 归一化坐标、容差、锁定规则 |
| Completion | Inspector 配置的 UnityEvent 出口 |

不得沿用其他模块的数字、坐标或流程状态作为临时默认。

## 7. Setup 可执行合同

### M2/M3 冻结 Scene

1. M2/M3 Scene 已存在时，菜单和 batch 入口只能打开或检测，明确日志后立即返回；不得执行 Ensure、MarkSceneDirty、SaveScene 或 SaveAssets。
2. M2/M3 Scene 不存在时必须明确报错且不得创建；历史生成代码不得重新接回公开或批处理入口。
3. 禁止 Setup 重写 M2/M3 的 `RectTransform`、`Graphic`/TMP、文案、颜色、Sprite、active 状态或 sibling 顺序。视觉变化只能由老板在 Unity Scene 中手工完成。
4. 后续功能必须使用 runtime 动态绑定、已有节点和非视觉组件；运行时路径缺失应 `LogError`，不得由 Setup 自愈视觉层级。
5. 调用入口前后必须比较 Scene 字节哈希；M2/M3 文件必须完全不变。

### 未冻结模块

1. Scene 结构改动只通过模块 Setup；Setup 只打开和保存自己的 Scene。
2. Ensure helper 对已有节点自愈布局、字号、对齐、颜色、按钮、Slider 和素材引用。
3. `EnsureImage(parent, "bg", ...)` 只查找直接子节点，禁止递归命中后代同名 `bg`。
4. 背景 Graphic 固定为父节点第一子节点，避免覆盖文本、钢轨或波形。
5. RailViewport 顺序固定为背景、普通钢轨、透明钢轨、效果与交互层。
6. 用户可替换素材采用“字段为空才注入”；Setup 权威底图可每次自愈。
7. Button/Slider 监听由事件所有者在 `Awake/OnEnable` 幂等绑定，不能依赖 Editor 临时委托。
8. 连续执行 Setup 两次，Scene SHA-256、层级、组件和监听数量不得继续变化。

错误处理：中文字体缺失应 `LogError` 并停止保存不完整 Scene；可选素材缺失应 `LogWarning` 并保留占位；运行时路径缺失应 `LogError`；下一模块未接入时保持完成态，不抛异常。

## 8. 验收合同

静态必须输出并检查 1920x1080、1280x720、2436x1125 三视口：无重叠、裁切、方框字或文字溢出；钢轨第一视觉；数字人与波形上下排列；触控区 >=64px。

URP 2D 的 RenderTexture 截图不得使用 `-nographics`：该模式可能 `RenderTexture.Create failed`，旧工具会把清屏色误存为成功截图。截图工具必须：

1. 使用有图形设备的 batchmode；按 CanvasScaler `Scale With Screen Size / Match` 公式计算每个目标视口的逻辑画布尺寸。
2. 保存 PNG 前采样像素并断言存在颜色差异；纯色图直接抛错，不得报告“已保存”。
3. 在 `finally` 恢复 Canvas renderMode/camera/RectTransform/Scaler、临时隐藏的 RawImage 和工具预览状态；禁止保存 Scene。
4. 截图前后比较冻结 Scene SHA-256，必须一致。

错误矩阵：`RenderTexture.Create failed` 或像素无差异 -> 验收失败；仅文件存在/尺寸正确 -> 不算通过；三视口非空且人工无裁切 + Scene 哈希不变 -> 通过。

Play Mode 独立验收：

- 数字人全身完整，无黑框或白块，LumaKey 正常。
- QAPanel 左开，Blocker 不压暗或拦截数字人。
- 模块步骤、拖拽、角度、距离、检出、波形和完成出口正常。
- QA 打开后暂停，关闭后恢复。

静态截图可临时隐藏未播放的数字人 RawImage，但必须在 `finally` 恢复且不保存 Scene，不能代替 Play Mode 验收。

## 9. 采用范围

- M3：当前 Scene 是冻结后的权威视觉；任何视觉调整只由老板手工修改 Scene，再按需更新本规范。
- M2：当前 Scene 同样冻结；保留四阶段、俯视钢轨、10°、110mm、拖拽坐标和视频数字人合同，功能接入不得改写视觉。
- M4：默认采用 M3 基线，只替换 M4 专属流程、素材和参数。
- M5：**采用 M2 UGUI 骨架**（2026-08-18 老板定稿），单步擦拭耦合剂交互（结束模块，无探测流程/无下一模块）；Scene 由 `M5Setup.cs` 生成（未冻结，幂等）；无波形窗口/探头/尺子流程节点，新增 RagHome 槽位与耦合剂层；数字人/QA 复用 `M3DigitalHumanBootstrap`（M3/M4/M5 场景名）。
- M5：默认采用 M3 基线，只替换 M5 专属流程、素材和参数。
