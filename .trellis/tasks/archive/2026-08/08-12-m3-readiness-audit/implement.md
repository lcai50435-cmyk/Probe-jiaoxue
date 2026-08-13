# M3 未来实施清单

> 本文件是未来 M3 实现任务的执行建议，不授权当前审计任务开始编码。M2 当前修复稳定前，不提取公共探测组件。

## 1. 前置门槛

- [ ] 用户确认 M3 不重复点击涂抹耦合剂，进入时只播放 2 秒状态展示。
- [ ] 用户确认 UI 使用“向下偏转 13°”口径。
- [ ] M2 探头、字体与波形修复完成并通过回归。
- [ ] `Assets/railwayTracks/正视角.png`、`正视角透明.png` 及 `.meta` 纳入版本管理。
- [ ] 创建独立 M3 实现 Trellis 任务，引用本任务 `prd.md`、`audit.md` 和 `design.md`。

## 2. 静态 Scene

- [ ] 新建最小 `Assets/Settings/Scenes/M3.unity`，不复制 M2 YAML。
- [ ] 新增幂等 `Assets/Editor/M3Setup.cs`，菜单和 batchmode 入口固定只操作 M3。
- [ ] 设置 CanvasScaler 1920x1080 / Match 0.5，生成 SafeArea。
- [ ] 按设计生成 Header、MainScene、紧凑 B 区、C 模式控件、D 操作带、QALayer、DigitalHumanStage、ModalLayer。
- [ ] 注入普通/透明正视角钢轨、K2.5、字体和占位尺；不覆盖非空引用。
- [ ] 连续运行 Setup 两次，确认第二次无额外对象、组件或场景 diff。
- [ ] 输出 1920x1080、1280x720、2436x1125 静态截图，先过布局门槛。

## 3. 公共能力提取

- [ ] 基于 M2 已验证行为设计 `ProbeScanProfile` 配置，包含角度方向/值、扫描区间、波形窗口、目标、文案、进入策略。
- [ ] 提取探头拖拽、程序波形、IdleHelp 的模块无关部分，保持每个新增 runtime 脚本默认不超过 150 行。
- [ ] 扩展尺子为“定位尺 + 测距尺”双阶段用途，状态由 Flow 统一拥有。
- [ ] 先迁移 M2 Setup 和 M2 Scene 引用，运行完整 M2 回归；失败立即回滚公共提取。
- [ ] M3Setup 再注入同一公共组件与 M3 Profile，禁止复制一套 `M3ProbeDrag/M3Waveform` 仅改常量。

## 4. M3 核心流程

- [ ] Intro 2 秒耦合剂状态展示，使用 scaled 时间并在 QA 暂停时同步暂停。
- [ ] 定位阶段校验探头位置、定位尺位置、向下 13°三条件。
- [ ] 角度不正确时保留探头位置并禁止扫描前进。
- [ ] 扫描从 150mm 连续映射到 100mm，波形按 140/124/120/118mm 配置联动。
- [ ] 快速跨过 120mm 仍只检出和蜂鸣一次；探头可继续经过目标观察下降。
- [ ] 测距阶段尺子 0 刻度自动吸附焊缝，显示 120mm 气泡，无额外确认按钮。
- [ ] 重置统一恢复 Intro、探头、尺子、波形、检出、视图和帮助计时，不清空 QA 历史。

## 5. 透视、帮助与 QA

- [ ] 普通/透视切换不改变流程状态。
- [ ] 独立 DamageMarker 与 BeamLayer 随探头更新；命中后红转黄，不做粒子。
- [ ] 30 秒定位帮助自动完成尺子/探头/13°定位，60 秒扫描帮助滑向 120mm。
- [ ] 复用全身数字人、长按入口、QAPanel、DeepSeek 和三态视频；面板从数字人左侧展开。
- [ ] QA 打开后 `Time.timeScale=0`，M3 拖拽、Intro、帮助和动画暂停；关闭恢复打开前值。
- [ ] M4 出口为空时显示“下一模块待接入”并保持完成状态。

## 6. 验证

```bash
git diff --check
rg -n "Resources\.Load|AssetDatabase|LoadAssetAtPath" Assets/Scripts/*Probe* Assets/Scripts/*Waveform* Assets/Scripts/*Flow*
wc -l Assets/Scripts/*.cs
```

- [ ] Unity batchmode 两次运行 M3Setup 均退出码 0，第二次无额外 diff。
- [ ] 保存、关闭、重新打开 M3 后进入 Play Mode，运行时监听仍有效。
- [ ] 三视口完整检查：初始、13°定位、120mm峰值、测距、透视、QA 打开。
- [ ] M2 全流程回归：10°、110mm、尺子、波形、帮助、QA、重置均不变。
- [ ] M3 直接打开与从 M2 进入的初始状态一致。
- [ ] 正式尺未到位时验收记录明确标注“功能占位”，不报告视觉完成。

## 7. 后续串联

- [ ] M3 独立验收后，再将 M2 完成 UnityEvent 配置为加载 M3。
- [ ] M4 就绪后，再配置 M3 出口并定点修改 Build Settings。
- [ ] 未经确认不修改 M1/M2 场景、不接假 M4、不执行提交或推送。
