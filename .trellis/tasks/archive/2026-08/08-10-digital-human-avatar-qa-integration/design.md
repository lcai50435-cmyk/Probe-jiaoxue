# 数字人全身头像与问答动画联动 - 技术设计

## 1. 边界与依赖

本任务只改 M1 展示与交互层，不改变 DeepSeek 请求协议。`M1QAPanel` 继续拥有问答生命周期，新增数字人组件只消费状态和面板可见性事件。

前置依赖：`.trellis/tasks/08-10-digital-human-three-animations` 负责三个 MP4 的 `importAudio: 0`。本任务仍在运行时设置 `VideoAudioOutputMode.None`，但不重复编辑导入元数据。

## 2. 场景结构

```text
画板
├── 白板背景
│   └── 数字人
│       ├── 对话框                 既有 M1 工具提示，保留
│       └── 背景圆/大头            既有静态形象，由 Setup 隐藏
├── Blocker                         既有全屏挡板
├── ChatArea                        新增，右侧为数字人预留空间
│   └── QAPanel                     既有面板重挂到此处
└── DigitalHumanStage               新增并保持最后一个兄弟节点
    ├── FullBodyView                RawImage + VideoPlayer + PressDetector
    └── AvatarView                  Image + PressDetector
```

`DigitalHumanStage` 是画板直接子节点并置于最后，保证它在 Blocker/QAPanel 之上。旧 `对话框` 保持原路径，避免破坏 `M1ToolSelection.aiAnswerPath`。

## 3. 布局合同

以 1920x1080 为基准，全部尺寸由 Setup 常量或 Inspector 字段配置：

- `DigitalHumanStage`：右侧宽约 320px，安全边距 24px，全身高度适配并保持比例。
- `ChatArea`：全屏拉伸，但右侧 offset 约 340px。
- `QAPanel`：宽 560-600px，锚定 ChatArea 右侧，沿用本地 `x=0 -> hiddenOffsetX` 滑动。
- `AvatarView`：112-128px，与 FullBodyView 使用相同中心锚点和偏移，不放在屏幕右上角。

通过 ChatArea 预留空间保持 `M1QAPanel` 现有滑动目标值，无需把全身数字人的偏移硬编码进运行时脚本。

## 4. 状态模型

```text
DisplayMode = FullBody | Avatar
AnswerState = Idle | Thinking | Speaking
PanelState  = Closed | Open
```

`M1DigitalHumanPresenter` 持有 `_modeBeforePanel` 和 `_restoreAvatarPending`：

| 事件 | 转换 |
|---|---|
| 初始 | FullBody + Idle |
| 短按且 Panel Closed | FullBody/Avatar 互切 |
| 长按 Avatar | 记录 Avatar -> FullBody -> Open |
| 长按 FullBody | 记录 FullBody -> Open |
| Panel Open 时短按 | 忽略 |
| AnswerState Thinking | 播放思考循环 |
| AnswerState Speaking | 播放讲解循环 |
| AnswerState Idle | 播放待机；若 Panel Closed 且待恢复则切 Avatar |
| Panel 完全关闭且当前 Idle | 立即恢复进入前形态 |
| Panel 完全关闭且非 Idle | 设置待恢复，生命周期回 Idle 后执行 |

## 5. 组件合同

### M1PressDetector

新增 `OnShortPress`。PointerUp 只在未触发长按、仍处于有效按压时发布短按；长按触发后设置标记，抑制本次 PointerUp。

### M1QAPanel

- 保留 `OnAnswerStateChanged`。
- 新增 `OnPanelVisibilityChanged(bool)`：Open 开始时发布 true，滑出完成并隐藏时发布 false。
- 增加 `bindPressTarget` 兼容开关；Setup 关闭旧自动绑定，由数字人 Presenter 统一处理两个显示形态的输入。
- `Open/Close` 仍为公开命令，网络和消息逻辑不变。

### M1DigitalHumanPresenter

唯一新增 runtime 组件，职责：

- 订阅 FullBodyView/AvatarView 的短按与长按。
- 调用 `M1QAPanel.Open()`，管理自动展开和恢复。
- 订阅 AnswerState 和 PanelVisibility 事件。
- 在一个 VideoPlayer 上切换 Idle/Thinking/Speaking VideoClip。
- 创建/释放 RenderTexture，设置 RawImage 纹理，强制静音与循环。
- 只切换视图显隐和状态，不包含素材路径、布局尺寸或文案。

### M1QASetup

- Ensure ChatArea、DigitalHumanStage、FullBodyView、AvatarView，重复执行自愈布局和层级。
- 重挂既有 QAPanel，不重建其消息子树。
- 仅加载用户指定的三个 MP4；不选择对应 WebM。仅当 Presenter 字段为空时注入。
- 注入 `UI-LumaKey.mat`、折叠头像、QAPanel 和两个 PressDetector。
- 设置状态 VideoPlayer `playOnAwake=false`、`isLooping=true`、`audioOutputMode=None`。
- 隐藏旧 `背景圆/大头`，保留旧 `对话框` 和路径。

## 6. 视频切换

使用一条 `VideoPlayer -> RenderTexture -> RawImage(UI-LumaKey)` 链路。抽帧结果确认三个 MP4 是无 Alpha 的 H.264 `yuv420p` 黑底视频，因此 LumaKey 只在显示阶段去除纯黑背景，不修改或重新处理原文件。常驻数字人使用独立材质资产（不改开场引导材质），并为 RenderTexture 配置适合显著缩小显示的过滤/mipmap；独立材质默认收窄 Key Smooth，减少白描边发糊。状态改变时 Stop、切 Clip、从 0 播放并循环；FullBodyView 重新激活时按当前 AnswerState 再次调用 PlayClip，避免停帧。

讲解片段本任务固定使用已确认的 `讲解动画2`；轮换多个讲解片段不属于验收要求。

## 7. 兼容与风险

- `M1LayoutPolish` 仍含历史虚构路径 `QAPanel/Panel/...`，本任务需同步改为真实层级或避免其覆盖 QAPanel 路径字段。
- QAPanel 正处于用户未提交改动中，修改前必须基于工作树现状做定点编辑，不还原其他改动。
- `Assets/DigitalHuman/` 当前未跟踪；实现和本地验证可使用，但交付时必须连同所引用 `.meta` 纳入版本控制。
- 场景中存在已提交的 API Key 是独立安全问题；本任务不得打印、复制或修改该值，提交前需另行轮换与迁移。
- 开场引导使用独立 VideoPlayer 和 Direct 音频，不得被 Presenter 或 Setup 扫描式修改。

## 8. 回滚形状

代码回滚限于新 Presenter、PressDetector/QAPanel 小幅事件扩展及 QA Setup/LayoutPolish 定点改动。重跑旧 Setup 不应删除用户内容；新 Setup 必须只管理命名明确的 ChatArea/DigitalHumanStage 节点。
