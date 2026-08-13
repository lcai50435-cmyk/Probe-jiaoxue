# M2 功能总收口执行清单

> 2026-08-12 产品决定：当前 M2/M3 Scene 立即冻结。以下涉及 M2Setup 重生成、自愈、写回或保存 Scene 的旧计划均已移除；视觉变化只能由老板手工完成。
> 2026-08-12 二次授权：老板为本轮最终收口明确授权**一次性定点解冻 M2 Scene**，仅限正式尺子、完整 QA 子树/组件引用与 Build Settings；完成后以新哈希重新冻结。执行由一次性 Editor 工具 `M2FinalCloseout` 在隔离副本 `E:/Project/UnityGame/Probe-jiaoxue-m2-review` 完成（主 Unity Editor 占用项目），产物回拷后重新冻结。

## 1. 基线与合同

- [x] 记录 M2/M3 冻结基线 SHA-256：M3 `f5446de3f50ca95bb989405a260ff538ca10c193cdedcb91c298717a24078a61` 全程未变；M2 收口前实际权威为 `2275b21836fb5d6b28cc6ee49617803e753191ce609ae856c1c211d0d685d14f`（旧清单 `6691cf38` 为更早基线，已被后续视觉任务合法演进覆盖）。
- [x] 记录 M1 `10884e91f9436dac533ed5059da25db5dfc6a1a23e1c471cb9db9be2b393af62` 全程未变；Build Settings 收口前仅含失效 SampleScene。
- [x] 固化伤损素材中心（俯视角透明.png `(1177,189)`，归一化 `(0.4794,0.3109)`）、RailViewport 映射和 110mm 线性进度公式（targetProgress=0.8）。
- [x] 确认当前 Unity 占用状态（主 Editor PID 26836），全程不并发 batchmode，改在隔离副本执行。

## 2. 视觉坐标与偏角

- [x] 通过 runtime 读取/复用冻结 Scene 已有坐标，使 110mm 与红色伤损 X/Y 重合；禁止 M2Setup 写回。
- [x] 仅复用冻结 Scene 已有 `Probe/bg` 探头图片节点，不新增/迁移视觉节点。
- [x] M2ProbeDrag 在 runtime 驱动已有探头图片节点旋转（`localEulerAngles.z`，0°→20° 同向），Reset 恢复 0°。
- [x] BeamLine 复用已有节点并在 runtime 跟随相同角度源和探头位置；普通隐藏、透视显示。
- [x] 手动与 AutoMoveToMm 共用视觉位置/距离/波形/检出路径（均走 `MoveToProgress`/`OnDistanceChanged`）。

## 3. 剩余功能收口（2026-08-12 一次性定点解冻完成）

- [x] 保持复用 M1 QA runtime 逻辑（M1QAPanel/M1DeepSeekClient/M1DigitalHumanPresenter/M1PressDetector 零复制），面板缺失时不得先暂停以及销毁恢复暂停保护（M1QAPanel 既有逻辑）。
- [x] `M2FinalCloseout` 工具在隔离副本执行：QALayer 下建 ChatArea（右侧预留 336px）→ 复用冻结空根 QAPanel → 构建 Header/MessageList/InputRow 完整子树 → Blocker 本体挂透明 Image+Button（点击关闭）→ QALayer 挂 M1QAPanel+M1DeepSeekClient（apiKey 留空）→ 注入 Presenter.qaPanel。QAPanel 初始隐藏、pivot(1,0.5) 右边缘 1584（与数字人左侧 336px 预留一致）。
- [x] 层级验证：QALayer(含 QAPanel/Blocker/ChatArea) < DigitalHumanStage < ModalLayer 保持冻结顺序；Blocker 不压暗数字人（stage 在其上）。
- [x] 正式尺子接入：Ruler/bg 换 `多功能尺子.png` + preserveAspect；Scene 初态直接序列化为 `RulerHome/Ruler`（150x32、中心锚点、y=10、最后 sibling、置灰锁定），非 Play Mode 与 Game 首帧一致；runtime 只校验/缓存，不再首帧自愈。Step 4 重挂 `RailViewport`（420x91、解锁）；零点按实际渲染图像左缘动态计算；ScaleText 禁用。最终冻结 Scene 哈希为 `3ef75ced51304258b5bde9b43be8f354b247753801a708ae52b922b5829c990b`。
- [x] 重置确认打开时暂停 M2IdleHelp（`SetDialog`→`idleHelp.SetPaused`），取消/确认后恢复/重置（ResetAll 先 `idleHelp.ResetAll()` 再关 Dialog）。
- [x] 保持冻结 Scene 中 FullBodyView `x=-124, y=-35`，Setup 不写回。

## 4. 冻结保护与静态验证

- [x] M2Setup/M3Setup 均为只读打开器：对已存在 Scene 仅打开并跳过生成、自愈、保存；缺失时报错且不创建；静态检查无 Ensure/MarkSceneDirty/SaveScene。
- [x] 工具执行全程 M1/M3 Scene 字节哈希不变（工具内 SHA-256 前后对比 + 主项目复验）。
- [x] 老板授权将尺子 Game 初态正式同步进 Scene 后，M2 冻结 SHA-256：`3ef75ced51304258b5bde9b43be8f354b247753801a708ae52b922b5829c990b`；`M2FinalCloseout` 只读验收哈希已同步，禁止再次写 Scene。
- [x] 离线 Roslyn 编译（Unity 6000.3.21f1 DotNetSdkRoslyn + 项目 Assembly-CSharp/Editor 引用集）零 Error；副本 batchmode 编译无 `error CS`。
- [x] 场景 YAML 完整性：509 块头、0 孤立块体行；QAPanel/ChatArea/Header/MessageList/InputRow 均单实例；M1QAPanel 路径配置无虚构层。
- [x] 隔离副本重新打开 M2 并实际进入 Play Mode，runtime 引用和 Awake 事件绑定完整；运行前后 M2 哈希不变。
- [x] 修复 M2Shot 的 URP 空截图问题：GPU batchmode 三视口通过非空像素断言，1280 CanvasScaler 无裁切，正式尺子可见；截图期间不保存 Scene。

## 5. Play Mode 验收

- [x] Editor-only `M2RuntimeSmoke` 实际进入 Play Mode，Step 1~4、完成态和重置后初态通过。
- [x] 10° 探头图片与声束反馈通过；根节点不旋转，Reset 恢复 0°（0°/20°画面手感保留人工观察）。
- [x] 首次输入即使从 150mm 单帧跨到 100mm，也先钳在 110mm/80% 线性进度完成检出锁定；下一次移动到 100mm 后状态正确，Detected 防重复路径通过。
- [x] 尺子初态 RulerHome、Step 4 重挂 RailViewport/解锁、实际图像左缘零点、焊缝吸附、完成与重置归槽通过。
- [x] 回归断言要求 `Ruler.parent == RulerHome`、最后 sibling、中心锚点，并在完成测量后 Reset 恢复 Scene 启动快照；静态截图不再调用 `ResetTool` 掩盖错误 Scene。最新版待主 Unity Reload Scene 后执行。
- [x] M2 QA：面板引用、Open/Close、全局暂停与恢复通过；Blocker/数字人层级静态通过。
- [x] M1→M2：生产 `OnStartClicked` 实际加载 Build Settings 中 M2，M2 初态一致；M1 QA 组件/DeepSeek 引用存在。
- [ ] 人工体验检查：数字人视频实际画质与三态、长按手感、30/60秒等待时长，以及配置 API Key 后的真实 DeepSeek 网络回复。

## 6. 串联与总检查

- [x] 已执行 `BuildScenesSetup.EnsureBuildScenes`：Build Settings M1/M2 为 index 0/1，失效 SampleScene 已移除（副本执行、随收口回拷）。
- [x] M2 runtime 每个不超过 150 行（M2FlowController 150 / M2ProbeDrag 150 / M2WaveformGraphic 119 / M2RulerDrag 128 / M2IdleHelp 79），无 Editor API/Assets 路径运行时加载。
- [x] M1 Scene 无任务外变化（哈希全程 `10884e91`）；M3 冻结哈希 `f5446de3` 不变；`git diff --check` 仅 LF/CRLF 换行警告（既有）。
- [x] 从 M1“开始探测”实际进入 M2，与直接打开 M2 初态一致。
- [x] 回填当前任务最终哈希、自动验收和剩余人工体验门槛；父任务/旧子任务在本任务归档时同步收口。

## 回滚点

- 坐标校准异常：回滚 runtime 映射，不修改冻结 Scene 或钢轨 PNG。
- 旋转影响拖拽：回滚 runtime 图片旋转，不修改 Probe 根或冻结 Scene。
- QA 复用影响 M1：回滚 M2 场景中 QALayer 的 M1QAPanel/DeepSeek/ChatArea 子树（从任务前基线恢复授权范围内的 QA 子树），M1QASetup 保持原路径；不修改 M1/M3 Scene。
- Build Settings 串联失败：只回滚 Build Settings 变更（恢复旧 `EditorBuildSettings.asset`），保留已验证 M2 独立功能。
