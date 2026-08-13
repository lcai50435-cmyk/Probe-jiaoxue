# M2 功能总收口技术设计

## 1. 坐标校准

透明钢轨素材 `2455x608` 中红色伤损中心约为 `(1178,190)`，归一化 X 约 `0.480`。Rail 图以 `891x220` 居中在 RailViewport 内，Setup 将素材坐标换算为 RailViewport 锚点：

```text
railLeft = (RailViewportWidth - RailWidth) / 2
damageX = (railLeft + RailWidth * damageTextureNormalizedX) / RailViewportWidth
```

为保持 `150→100mm` 线性映射，110mm 必须位于扫描进度：

```text
targetProgress = InverseLerp(150, 100, 110) = 0.8
scanEndX = scanStartX + (damageX - scanStartX) / 0.8
```

冻结决定后，Setup 不再维护或写回这些坐标。实现必须读取当前 Scene 已有节点/序列化值，并在 runtime 的 ProbeDrag、Waveform 和 Flow 中复用同一距离参数，不新增第二套判定；自动演示继续调用 `AutoMoveToMm(110)`，自然落到相同视觉位置。首次输入若单帧跨过 110mm，统一移动路径先钳在目标点完成峰值/蜂鸣/检出，下一次移动才继续到 100mm，避免在峰后位置迟发检出。若当前 Scene 缺少满足校准所需的视觉节点或坐标，只能由老板手工修改 Scene。

## 2. 偏角视觉

`Probe` 保持 RectTransform 拖拽根。冻结后不得通过 Setup 新增/Ensure `ProbeVisual`；仅当当前 Scene 已有合适图片子节点时，`M2ProbeDrag` 才可在 runtime 查找/绑定并旋转它。若节点不存在且需求涉及视觉层级变化，交由老板手工添加。`OnAngleChanged` 只设置图片子节点的 `localEulerAngles.z`，不旋转根节点。

原始规格是竖直剖面“向上偏转 10°”，俯视图 Z 轴旋转仅为二维代理反馈。当前运行时按 0°→20°映射为 0°→20°同向旋转，目标 10°显示 10°，让滑块数值与视觉反馈直接对应；不改变探头根节点和扫描方向。

`BeamLine` 保持现有子节点，随探头锚点移动；其局部 Z 角度使用角度值驱动，并按固定长度显示。手动滑块、自动演示和 Reset 都走 `OnAngleChanged`/统一视觉方法。

## 3. QA 复用（2026-08-12 一次性定点解冻后已完成）

不复制 `M1QAPanel` 或 DeepSeek runtime。老板授权本轮一次性定点解冻 M2 Scene（仅限 QA 子树/组件引用、正式尺子与 Build Settings），由一次性 Editor 工具 `M2FinalCloseout` 在隔离副本 `E:/Project/UnityGame/Probe-jiaoxue-m2-review` 执行并回拷，完成后重新冻结。老板随后要求 Scene 与 Game 初态完全一致，本任务将尺子正式序列化为 `RulerHome` 最后子节点，并固化 Game 已验收的归槽 Rect；M2 最终哈希为 `3ef75ced…`。

工具行为：QALayer 下创建 `ChatArea`（右侧预留 336px，为数字人 320 舞台让位）→ 复用冻结空根 `QAPanel`（重挂 ChatArea 下，pivot(1,0.5)、宽 580、右边缘 1584）→ 构建 `Header/MessageList/InputRow` 完整子树（结构与 M1 面板一致，路径无虚构层）→ `Blocker` 本体挂全透明 Image + Button（点击关闭，视觉由既有 bg 承担）→ QALayer 挂 `M1QAPanel` + `M1DeepSeekClient`（apiKey 留空，不复制 M1 配置）→ 注入 `Presenter.qaPanel`。QAPanel 初始隐藏。

路径字段以 M2 真实层级为准，运行时 `M1QAPanel.FindDeep` 能递归解析。层级保持 `QALayer < DigitalHumanStage < ModalLayer`（Blocker 不压暗数字人）。

## 3.1 正式尺子接入

`Ruler/bg` 接入 `多功能尺子.png`（2102x455，比例 4.62；Multiple Sprite），开启 preserveAspect 并显式注入 `rulerImage`。Scene 初态直接序列化为 `ToolShelf/RulerHome/Ruler`，Ruler 是 Home 最后子节点，中心锚点、`anchoredPosition=(0,10)`、`sizeDelta=(150,32)`、置灰锁定；非 Play Mode 与 Game 首帧一致。`M2RulerDrag` 首次绑定只校验并缓存该 Scene 初态，不再运行时自愈。Step 4 由 `Show` 重挂 `RailViewport`，恢复 `420x91` 测量尺寸并解锁。0 刻度每次按当前 Rect、Sprite 比例和 preserveAspect 计算实际渲染图像左缘。占位 ScaleText 禁用。

## 4. 暂停与重置

QA 继续由 `M1QAPanel.pauseGameOnOpen` 管理全局 timeScale。`M2FlowController.SetDialog` 同步调用 `idleHelp.SetPaused(visible)`；ResetAll 在 `idleHelp.ResetAll()` 后关闭 Dialog，避免恢复旧 idle。

## 5. Scene 冻结与 Build Settings（2026-08-12 收口后状态）

当前 M2/M3 Scene 是视觉权威，冻结的 FullBodyView `anchoredPosition=(-124,-35)` 保留在 Scene 中，不再写回 Setup。M2Setup/M3Setup 只能打开或检测现有目标并返回，不执行 Ensure、MarkSceneDirty 或 SaveScene；任一 Scene 缺失时只报错，均不得创建。

2026-08-12 老板授权一次性定点解冻：仅 `M2FinalCloseout` 工具（隔离副本执行）允许修改 M2 Scene（QA 子树/组件引用、正式尺子、ScaleText 禁用），尺子 Scene/Game 初态同步后，M2 重新冻结于 `3ef75ced51304258b5bde9b43be8f354b247753801a708ae52b922b5829c990b`；Scene Rect 是唯一初态权威；M1 `10884e91…` 与 M3 `f5446de3…` 全程字节不变。工具幂等（重复执行哈希稳定）但不再是持续生成入口，冻结后不得再次运行。

Build Settings 已执行 `BuildScenesSetup.EnsureBuildScenes`：M1/M2 为 index 0/1，失效 SampleScene 已移除；该步骤只修改 Build Settings，不保存 M2/M3 Scene。

## 6. 文件边界

允许修改（本轮实际改动）：

- `Assets/Editor/M2FinalCloseout.cs`（新增，一次性收口工具，执行后已降级为只读哈希验收）
- `Assets/Settings/Scenes/M2.unity`（老板授权同步尺子 Scene/Game 初态：`RulerHome/Ruler`、150x32、y=10、最后 sibling；完成后重新冻结 `3ef75ced…`）
- `Assets/Scripts/M2RulerDrag.cs`（扩展既有组件：运行时归槽、测量态重挂、preserveAspect 零点计算；未新增 runtime 脚本）
- `Assets/Editor/M2RuntimeSmoke.cs`（Editor-only Play Mode 自动验收，不进 Player、不保存 Scene）
- `Assets/Editor/M2Shot.cs`（修复 URP batchmode 空截图，加入非空像素断言、CanvasScaler 三视口映射和尺子状态恢复）
- `ProjectSettings/EditorBuildSettings.asset`（M1/M2 index 0/1，移除 SampleScene）
- 当前任务及父任务文档

未改动：M1/M3 Scene、`M2Setup.cs`/`M3Setup.cs`/`M1QASetup.cs`（保持只读打开器与 M1 原样）、DeepSeek 请求逻辑。不新增 M2 runtime 脚本。

## 7. 验证策略

1. 静态检查目标坐标公式：110mm 映射到伤损 X，150/100 对应新轨迹两端（`M2ProbeDrag.CalibrateTrack` 运行时计算）。
2. 记录 M1/M2/M3 SHA-256：收口前后 M1/M3 字节哈希完全一致；M2 由 `2275b218…` 经 QA/尺子接入、老板手调及尺子 Scene/Game 初态同步变为 `3ef75ced…`；只读入口以该哈希验收且不写回。
3. 副本 batchmode 执行 M2FinalCloseout（主项目被 Unity 占用，不并发）；Unity 编译无 `error CS`。
4. 场景 YAML 校验：509 块头、0 孤立块体行；QA 节点单实例；M1QAPanel 路径无虚构层；cnFont/Presenter/DeepSeek/Blocker 引用完整。
5. GPU batchmode 三视口截图通过非空像素断言；CanvasScaler 的 1920x1080、1280x720、2436x1125 映射无裁切，截图工具恢复状态且 Scene 哈希不变。`-nographics` 下 URP 2D RenderTexture 不可用，工具现会拒绝保存纯色空图。
6. Editor Play Mode 自动烟测通过：QA 暂停恢复、四阶段、0°/10°视觉、110mm 线性检出、峰后100mm、尺子吸附、完成与重置；M1 生产入口实际加载 M2 且初态一致。
7. 保留人工体验检查：数字人实际视频画质、长按手感和配置 API Key 后的真实 DeepSeek 回复。
