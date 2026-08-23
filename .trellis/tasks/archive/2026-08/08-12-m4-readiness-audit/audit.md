# M4 模块实现就绪度审计

审计日期：2026-08-12
补充裁决：老板 2026-08-13 确认钢轨红色区域为唯一损伤目标；本审计中旧规格的“40mm 对焊缝熔合线”由该最新口径覆盖。
审计方式：只读文档/Scene/代码/素材核对，未修改任何业务代码、Scene、素材或 Build Settings。

## 总体结论

**M4 已具备实现开工条件：功能开发无产品级阻塞，不需要新增外部素材；但当前尚不具备直接完成全量验收的条件。完整运行时与最终视觉验收前，需完成 M3 runtime 验收收尾及 §十的视觉确认项。**

与 M3 开工时相比，M4 的条件更好：透明钢轨正视图现成、多功能尺子实物图现成（M2 已用）、M3 已提供同视角同伤损图的参考实现与冻结 Scene 骨架；公共链路中 QA/数字人/波形（M1/M2）已验收，尺子/帮助/流程参考实现（M3）已参数化但**尚未验收**。

**“能否开工”与“能否完整验收”需区分**：功能开工可立即进行——依赖实现任务内自给的工作（新建 M4 Setup/Scene、参数化或新建 M4 runtime 组件、验收工具）；完整验收（含最终视觉验收）的确定性依赖：① M3 runtime 验收收尾（M4 参数化复用的 M3 四件套目前是 0 项勾选、无烟测的未验证代码，M4 会继承其潜在缺陷，不只是影响“M3→M4 串联”）；② 视觉确认项（向上 10° 倾斜方向、尺子选择、轨腰摆放位/声束几何，见 §七/§十），这些改变会重跑三视口截图与测量视觉。另注意 M3 基线（Scene/钢轨素材/runtime/任务文档）当前全部为 git 未跟踪，建议提交后再开工 M4（仓库卫生，见 §六）。

## 一、产品合同（M4 权威参数）——已具备

来源：`文档/仿真交互动画技术规格书.docx` M4 专章（docx 提取文本第 324-429 行）；`文档/功能文档.docx`（第 32、46、57 行）；`文档/仿真交互动画技术规格书—（AI新增版）.docx`（第 90-104 行）。

| 参数 | 权威值 | 出处 |
|---|---|---|
| 模块定位 | 第三方位·轨腰部位探测 | 规格书 324 行 |
| 场景视角 | 正面平视（“同 M3”） | 规格书 325 行 |
| 探头 | K2.5 单探头 | 规格书 1.3（全局） |
| 偏角 | 向上 10°，D 区滑块 + 尺子卡槽 | 规格书 335-343 行 |
| 扫描区间 | 80→30mm（轨腰最上端直线前进） | 规格书 359-377 行 |
| 目标距离 | 40mm（探头入射点距钢轨红色损伤中心；老板 2026-08-13 最新口径覆盖旧规格的熔合线终点） | 老板 2026-08-13 裁决；规格书 378-379 行为旧口径 |
| 波形联动 | 80-55 基线杂波 / 55-45 生长 / 45-38 峰值（检出+蜂鸣）/ 38-30 下降消失；玩家在 40mm 检出后锁定，38→30 仅保留为参考包络 | 规格书 363-377 行；老板最新玩法口径 |
| 检出触发 | 保持向上 10°、几何距离约 40mm且绿色检测束命中红色损伤区时，蜂鸣一次并锁定 | 老板 2026-08-13 裁决 |
| 测距 | 同一多功能尺 `0mm` 对探头入射点、`40mm` 对红色损伤中心；双点同时满足才完成 | 老板 2026-08-13 裁决 |
| 完成出口 | “轨腰部位探测完成。”→ 自动切换下一功能模块（M5） | 规格书 390 行 |
| 透视 | 透明钢轨 + 红色半圆弧伤损 + 绿光束 + 红转黄 + 高亮闪烁（无粒子，功能文档降级） | 规格书 392-408 行；功能文档第 32 行降级清单 |
| 防卡死 | 角度 30 秒自动演示向上 10°；移动 60 秒滑向 40mm；检出后提示透视 | 规格书 410-417 行 |
| 数字人话术 | Step0“把探头放在轨腰最上端并向上偏转10°”；Step1“角度正确！沿着轨腰最上端向前移动探头吧！”；Step2“太棒了！三个方位都探测到了伤损！”；Step3“探测完成啦！但还有最后一步工作哦。”；首次透视“看！绿色光束就是超声波束…” | AI 新增版 90-104 行 |

**文档冲突与裁决（均已有明确裁决，无未决项）**：

1. **耦合剂**：技术规格书 M4 专章 Step 0 为“耦合剂已涂抹（2s 动画展示薄膜并消失）”；功能文档第 32 行把 M2/M3/M4 统一列入“涂抹耦合剂”按钮功能。老板已拍板（prd.md）沿用 M3 口径：进入自动展示约 2 秒已涂薄膜、直接进入定位、不新增涂抹按钮。→ 已裁决。
2. **完成出口**：规格书 M4 写“自动切换下一功能模块”；功能文档第 57 行要求完整状态机串联。老板已拍板用 Inspector 可配置 `UnityEvent` 出口（prd.md）：40mm 测距完成进入完成态，M5 未配置时显示“下一模块待接入”，不报错、不创建假 M5。→ 已裁决。
3. **波形口径**：规格书要求“波形由程序绘制，形态参照参考视频”；功能文档降级为程序三态（平线/生长/峰值）平滑过渡。M2 已使用 `M2WaveformGraphic` 并通过验收，M3 已接入但尚未完成 runtime 验收；M4 可沿用同一参数化组件。→ 已裁决（项目既定降级方案）。
4. **透视降级**：取消粒子流动，用声束线段 + 红转黄变色示意（功能文档第 32 行）。M3 已按此实现。→ 已裁决。
5. **40mm 终点**：旧规格写“本侧焊缝熔合线”，老板 2026-08-13 明确钢轨红色区域为损伤处。最终以红色损伤中心作为检测束命中、40mm 距离和尺子 40mm 锚点的唯一目标；`WeldLine` 只作视觉参照。→ 已裁决。

## 二、仓库载体（Scene / Setup / runtime / 验收工具）——需新建

| 载体 | 仓库现状 | 结论 |
|---|---|---|
| M4 Scene | `Assets/Settings/Scenes/` 下只有 M1.unity / M2.unity / M3.unity，**M4.unity 不存在** | 需新建；M4 属未冻结模块，可走 Setup 生成（ugui-module-template.md §7“未冻结模块”合同） |
| M4 Setup | `Assets/Editor/` 无任何 M4 文件，**M4Setup.cs 不存在** | 需新建；参考 M3 static design（`.trellis/tasks/08-12-m3-static-scene-ugui/design.md` 层级）与 ugui-module-template.md §2 权威骨架 |
| M3 基线可用性 | M3.unity 冻结视觉权威存在；层级含 RailViewport（RailNormal/RailPerspective/WeldLine/CouplantOverlay/DamageMarker/BeamLayer/Ruler/Probe/MeasurementBubble）、ToolShelf（ProbeHome/RulerHome）、PerspectiveBar_C、WaveformArea_B、ControlDock_D（含 AngleTrack Slider、CompletionPanel、HelpPanel）、QALayer、DigitalHumanStage、ModalLayer | **可采用**。M4 默认采用 M3 基线（ugui-module-template.md §9），只替换模块专属流程/参数/素材；禁止复制 Scene YAML/fileID/整套脚本，需用自己的 Setup 重建同构骨架 |
| M4 runtime | `Assets/Scripts/` 无 M4 文件 | 需在“参数化复用 M3 组件”与“新建 M4 组件”间决策（见第四节） |
| M4 验收工具 | 无 M4Shot / M4RuntimeSmoke | 需新建；M4Shot 参考 `Assets/Editor/M2Shot.cs`（完整版：三视口 + 像素差异断言 + finally 恢复 + Scene SHA-256 前后一致，符合 ugui-module-template.md §8）；**不应照抄 M3Shot**（简版缺像素断言与哈希校验） |
| Build Settings | `ProjectSettings/EditorBuildSettings.asset` 只启用 M1、M2（enabled: 1）；**M3、M4 均不在其中** | 现状如此且本任务不修改（prd 范围外）；M3 验收后另开任务处理串联 |

## 三、素材矩阵——核心素材具备，参考素材不可直接交互

| 需求 | 本地证据 | 结论 |
|---|---|---|
| 普通钢轨正视图 | `Assets/railwayTracks/正视角.png`（2292x740 RGBA；M3.unity RailNormal 引用，guid dfa69f20…） | 可直接使用 |
| 透明钢轨正视图 | `Assets/railwayTracks/正视角透明.png`（2292x740 RGBA；M3.unity RailPerspective 引用，guid e73cfeb1…） | 可直接使用；与普通图同尺寸同构图（M3 audit 遗留的“需 PS 制作”已由仓库现成资产解决） |
| 轨腰部位参考图 | `Assets/交互动画素材/03 其他素材/三方位探测图片/轨腰部位探测.png`（1482x447） | 仅参考/构图核对；已烘焙实景、手、探头与标注，不能直接交互 |
| K2.5 探头 | `Assets/probeFootage/探头素材（无白边版）/K2.5.PNG`（2000x1410 RGBA；M2/M3 均在用） | 可直接使用 |
| 多功能测量尺 | `Assets/交互动画素材/03 其他素材/多功能尺子.png`（2102x455 RGBA；M2.unity 已用实物图，像素实测全宽有 mm 刻度）；AI 新增版第 89 行明确 M4“利用多功能测量尺将探头向上偏转 10°” | **可直接使用**（M3 用纯色矩形是 M3 内部降级，M4 规格书点名多功能测量尺，实物图优先）。注意仓库另有 `自制尺（后续有可能优化）.JPG`（4032x3024，文件名表明尺子素材存在后续替换可能），尺子选择属验收前确认项（见 §十）；40mm 读数刻度可见性列入三视口人工验收 |
| 轨腰波形参考 | `Assets/交互动画素材/03 其他素材/三方位探测波形参考/轨腰部位波形.mp4` | 参考齐全；运行时仍用程序波形，不直接播放 |
| 音效 | `Assets/Audio/E-01 正确提示音/`、`E-02 错误提示音/`、`E-03 蜂鸣报警音/`、`E-04 通关音效/`、`E-05 拖拽点击音效/`（M3.unity 已注入 E-01、E-03） | 已具备（功能文档第 61-68 行标注“需提供”，实际仓库已齐） |
| 数字人 | `Assets/DigitalHuman/A-01 待机动画/`、`A-02讲解动画/`、`A-03 思考动画/`（webm+mp4）、`A-04 引导动画/`、`A-05 折叠态头像.PNG`；`Assets/Shaders/UI-LumaKey-DigitalHuman.mat` 常驻专用材质已存在 | 已具备；M4 无需开场引导（Intro 仅 M1），常驻数字人走独立 LumaKey 材质契约（video-intro.md §3） |
| 中文字体 | `Assets/font/sarasa-gothic-sc-regular/sarasa-gothic-sc-regular_cn.asset` | 可直接复用 |
| 伤损/声束 | 仓库无独立素材；M3 用正视角透明图内红色伤损示意（像素实测伤损质心 u≈0.468、v≈0.244，与 `M3ProbeDrag` const 一致，位于轨头下颚）+ 独立 DamageMarker（红转黄）+ BeamLayer（入射/反射声束线段）实现 | **不阻塞**；规格书与 AI 新增版确认三个方位探测**同一“铝热焊缝轨头下颚伤损”**（课程主题即《铝热焊缝轨头下颚伤损探测新工艺》，M2/M3/M4 检出文案均为“铝热焊缝轨头下颚伤损已检出”），M4 与 M3 同视角同图同伤损、伤损坐标可直接复用；但 M4 探头摆放位“轨腰最上端”的 y 坐标与向上 10° 声束指向下颚伤损的几何对齐属 M4 侧确认项（见 §七） |
| 耦合剂薄膜 | 无素材；M3 用 CouplantOverlay CanvasGroup 透明度/缩放动画实现 2 秒薄膜 | 不阻塞；同机制复用 |

## 四、复用边界（M1 QA/数字人、M2 波形、M3 参数驱动组件）

### 4.1 可直接复用（零 M 专属依赖）

- **`M1QAPanel`**（`Assets/Scripts/M1QAPanel.cs`，528 行存量）：面板/挡板/输入/发送/语音/计数全部路径走 Inspector（panelPath/blockerPath/closeButtonPath…），`pauseGameOnOpen` 默认 true、关闭恢复打开前 `timeScale`、问答链路全 unscaled 计时（low-code.md §8.1）。M2.unity 已挂载并验收，M4 由 Setup 注入路径与引用即可。
- **`M1DeepSeekClient`**：全部 Inspector 配置，注释明确“M2/M3 模块可复用”；apiKey 留空不发请求。
- **`M1DigitalHumanPresenter`**：三态视频/RT/RawImage/引用全部 Inspector 注入，无模块硬编码；直接挂载即用。
- **`M1PressDetector`**：通用长按/短按组件，holdDuration 可配置。
- **`M2WaveformGraphic`**（119 行）：纯参数驱动，不拥有流程状态。M4 包络可直接 Inspector 配置：`scanStartMm=80, scanEndMm=30, growthStartMm=55, peakWindowMaxMm=45, peakTargetMm=40, peakWindowMinMm=38`（与规格书 363-377 行联动表逐项对应）。M3 已在用同一组件（命名空间 M2 不影响）。
- **`M1IntroVideo` + UI-LumaKey**：M4 不需要开场引导；常驻数字人材质资产已独立（`UI-LumaKey-DigitalHuman.mat`），不碰开场引导材质（video-intro.md §3）。

### 4.2 有条件复用（M3 组件，含 M3 专属默认值/常量残留，需参数化后使用）

> M3 四件套（Flow 147 行 / ProbeDrag 147 行 / RulerDrag 114 行 / IdleHelp 60 行）均在 ≤150 行合同内，状态机结构（Intro→Positioning→Scanning→Measuring→Completed，无涂抹按钮）与 M4 产品决定同构，是 M4 的正确基线。M2FlowController 的 Couplant 涂抹按钮阶段与 M4 无关，**禁止复制**。

| 组件 | 可复用部分 | 残留的 M3 专属硬编码 | M4 处理 |
|---|---|---|---|
| `M3FlowController` | 三阶段状态机、Intro 2s 动画、检出/测距/完成、透视切换、ResetAll、UnityEvent 出口（未配置时显示“下一模块待接入”，M3.unity 序列化 onCompleted `m_Calls: []` 验证） | `_prevMm = 150f`（两处）、`waveform?.SetDistanceMm(150f)`（两处，M4 需 80）；`StageNames` static readonly（“探头定位与偏角/移动探测/尺子测距/完成”）；完成文案“轨头侧面探测完成”（M4 需“轨腰部位探测完成”）；`stepHints` 默认值（M3 文案，Inspector 可覆盖）；“步骤 X/3” 硬编码 3（M4 同为 3 步，不冲突） | 参数化 `scanStartMm`、完成文案、StageNames 后复用；或新建 M4 组件（见下） |
| `M3ProbeDrag` | 放置/角度门控/距离报告、CalibrateTrack 伤损坐标校准、AutoMoveToMm 钳制 | `DamageU=1073.5f/2292f, DamageV=179.6f/740f` const（正视角透明图伤损坐标，像素实测质心 u≈0.468、v≈0.244 与 const 一致——**M4 同视角同图同伤损，坐标相同**，但仍是常量非配置）；`CalibrateTrack` 中 `.6f` 魔法数字（120mm→0.6 进度，M4 需 0.8=(80-40)/(80-30)）；`visualTiltAtTarget=13f` 默认（Inspector 可覆盖）；**`ApplyAngleVisual` 固定 `-tilt` 旋转，无方向字段——M4 “向上 10°”与 M3 “向下 13°”视觉方向相反** | 新增 `tiltDirection`/`targetProgress` 配置字段（或按方向复用）；伤损坐标建议参数化 |
| `M3RulerDrag` | 拖拽/零刻度吸附/Scene 初态缓存与 Reset 恢复，全 Inspector 配置 | 日志前缀 `[M3RulerDrag]`（无关紧要）；`measureStartLocal` 默认 (0.5,0.78) 为 M3 摆放位 | 配置 M4 尺子摆放位（轨腰部位）与 measureSize 即可复用 |
| `M3IdleHelp` | 30/60 秒防卡死编排、SetPaused/ResetAll、自动演示协程 | 帮助文案硬编码：“需要帮助调整到向下 13° 吗？”、“即将演示目标点探测”（M4 需“向上 10°”/40mm） | 文案改配置或按 M4 替换 |

**决策建议（低代码规范 §1 决策树）**：优先“参数化复用 M3 组件”（新增配置字段 ≤ 几个），不新建整套 M4 状态机；若 M3 硬编码点超过可接受范围，可新建 M4 专属薄组件（每脚本 ≤150 行）复用 M3 的机制而非复制代码。**禁止**复制 M2/M3 状态机为 M4 版本。

### 4.3 M3 当前实现与验收状态（对 M4 的影响）

- M3 冻结 Scene（`Assets/Settings/Scenes/M3.unity`）已完成收口：`M3FlowController` 等 4 组件 + `M2WaveformGraphic` 已挂载，参数完整序列化（introDuration=2、targetAngle=13、targetDistance=120、stepHints、beepClip/correctClip 注入、onCompleted 空）。
- **M3 的 QA/数字人链路未接入**：M3.unity 中无任何 M1 组件、无 VideoPlayer/RenderTexture/RawImage/LumaKey（脚本引用核查：仅 4 个 M3 组件 + 1 个 M2WaveformGraphic）。
- **M3 runtime 任务未验收**：`.trellis/tasks/08-12-m3-runtime-implementation/implement.md` 当前 0 项勾选；`Assets/Editor/` 无 `M3RuntimeSmoke.cs`；`M3Shot.cs` 是简化版（无像素断言、无 SHA-256 校验）。
- **对 M4 的判定**：M4 的核心机制（状态机、波形、尺子、防卡死、QA/数字人）依赖**已验收的 M1/M2 公共能力**（QA/数字人/波形），以及**未经验证的 M3 参考实现**（M3 runtime 任务 0 项勾选、无 M3RuntimeSmoke）。M4 功能开工不依赖 M3 验收完成，但参数化复用 M3 四件套意味着 M4 会继承 M3 的潜在缺陷——M3 runtime 验收收尾同时是 M4 参考实现质量的验证，建议在 M4 完整验收前完成（非阻塞开工，影响验收确定性）；M3→M4 串联（范围外）则明确依赖 M3 验收。
- **版本控制状态**：M3 Scene（`Assets/Settings/Scenes/M3.unity`）、正视角/正视角透明 PNG、M3 runtime 四件套与 Editor 工具（M3Setup/M3Shot/M3FinalCloseout）、M3 任务文档当前全部为 git 未跟踪（`??`），M2.unity/M1.unity 亦处于未提交修改状态。M4 的参考基线与冻结视觉权威不在版本控制内，建议开工 M4 前先提交基线（仓库卫生，非功能阻塞）。

## 五、串联与验收工具链

| 项 | 现状 | 结论 |
|---|---|---|
| M3→M4 | M3 `onCompleted` UnityEvent 已存在且为空（`m_Calls: []`）；M4 不存在 | 本任务不配置（范围外）；M4 实现后需老板/串联任务在 Inspector 配置 |
| M4→M5 | M5 不存在 | M4 完成态显示“下一模块待接入”（M3FlowController.UpdateUi 已验证此逻辑）；不创建假 M5 |
| Build Settings | 仅 M1、M2 enabled | 本任务不修改；串联任务处理 |
| 编译验证 | 项目 batchmode 编译链路已有先例（M2Shot/M2RuntimeSmoke）；离线 csc 不可信（low-code.md §7.1） | 可复用流程 |
| Play Mode 烟测 | `M2RuntimeSmoke.cs` 是完整模板（QA 暂停、阶段推进、角度/距离钳制、尺子吸附、重置断言） | M4 需新建 M4RuntimeSmoke |
| 三视口截图 | `M2Shot.cs` 完整版（1920x1080/1280x720/2436x1125、像素差异断言、finally 恢复、冻结 Scene SHA-256 前后一致） | M4 需新建 M4Shot，按 M2Shot 实现；M3Shot 简版不可作模板 |
| Scene 幂等 | 未冻结模块合同：Setup 连续两次执行 Scene SHA-256 不变 | M4 Setup 需满足 |

## 六、阻塞项

**无产品级阻塞（功能开工）。** 所有产品决定已拍板（耦合剂口径、UnityEvent 出口），素材齐备，M1/M2 公共能力已验收。

顺序性前置（非阻塞功能开工，但影响完整验收）：M3 runtime 任务验收收尾（QA/数字人接入 + M3RuntimeSmoke + 完整截图）——M4 参数化复用的 M3 四件套是未验证代码，其验收同时验证 M4 参考实现质量，也是 M3→M4 串联（范围外）的前提。

仓库卫生项（非阻塞）：M3 Scene/钢轨素材/M3 runtime/任务文档均未纳入 git，建议提交基线后再开工 M4。

## 七、非阻塞缺口（实现任务内补齐）

1. M4 Scene 缺失 → 新建 `Assets/Editor/M4Setup.cs` 生成（未冻结模块，参考 M3 static design 层级 + ugui-module-template §2 骨架）。
2. M4 runtime 组件缺失 → 参数化复用 M3 四件套或新建 M4 薄组件（见 4.2 决策建议）。
3. 角度方向符号：M4“向上 10°”与 M3“向下 13°”视觉反向，`M3ProbeDrag.ApplyAngleVisual` 无方向配置 → 需加配置字段。
4. 波形包络与距离合同：M4 波形参考域为 80→30mm、40mm 峰值；玩家从80mm移动到40mm即锁定。距离必须由多功能尺 `0→40mm` 二维锚点跨度标定，禁止把 M3 的 `.6` 改成 `.8` 后继续用归一化进度伪造毫米。
5. 文案：M4 步骤提示、完成文案（“轨腰部位探测完成”）、IdleHelp 帮助文案、数字人话术（AI 新增版 90-104 行）。
6. 轨腰摆放位与声束几何：M4 探头摆放在轨腰最上端；IncidentBeam 从探头入射点指向老板确认的钢轨红色损伤中心。具体 y 坐标和命中容差由实现与 Play Mode 标定验证，不再作为产品待确认项。
7. （验收前确认）尺子选择：多功能尺子.png（规格书点名）vs `自制尺（后续有可能优化）.JPG` vs 与 M3 一致的纯色占位；选择影响 Setup 注入与最终视觉验收。
8. 验收工具：M4Shot（按 M2Shot 完整版）、M4RuntimeSmoke。
9. Build Settings 串联与 M3→M4 / M4→M5 UnityEvent 配置：另开任务（本任务范围外）。
10. 红色损伤中心坐标必须配置化，作为 IncidentBeam 命中、40mm 几何距离和尺子 40mm 锚点的唯一目标；不得沿用 M3 const 后再建立另一套 WeldLine 判定。

## 八、可复用资产清单

- 公共 runtime：`M1QAPanel`、`M1DeepSeekClient`、`M1DigitalHumanPresenter`、`M1PressDetector`、`M2WaveformGraphic`（全 Inspector 配置，M1/M2 验收通过）。
- 参考实现：`M3FlowController` / `M3ProbeDrag` / `M3RulerDrag` / `M3IdleHelp`（同视角同伤损图，参数化后可用；**未验收**，复用前建议先补 M3RuntimeSmoke 验证，见 §4.3/§六）。
- 验收模板：`M2Shot`（完整截图）、`M2RuntimeSmoke`（Play Mode 烟测）、`M2FinalCloseout`（冻结哈希只读验收思路）。
- 素材：正视角/正视角透明钢轨、K2.5（无白边）、多功能尺子实物图、轨腰波形参考 mp4、E-01~E-05 音效、A-01~A-05 数字人、UI-LumaKey-DigitalHuman 材质、中文字体。
- 视觉骨架：冻结的 M3.unity（只作视觉权威参照，禁止复制 YAML）。

## 九、建议实施顺序

1. （前置）M3 runtime 验收收尾：QA/数字人接入 M3、M3RuntimeSmoke、M3Shot 升级或三视口人工验收——为 M4 提供经过完整验收的参考基线与串联上游。
2. M4 参数提取完成 → 决策“参数化复用 M3 组件 vs 新建 M4 组件”（低代码决策树）。
3. 新建 `M4Setup.cs` 生成 M4 Scene（M3 基线骨架 + M4 流程/参数/素材注入；连续执行幂等）。
4. M4 runtime 组件参数化/新建与绑定（波形 80/55/45/40/38、向上 10°、尺子 0→40mm 几何比例、红色损伤命中、M4 文案与话术）。
5. `M4Shot`（按 M2Shot 完整版）+ `M4RuntimeSmoke` 验收；三视口截图人工确认。
6. 另开任务：Build Settings 纳入 M3/M4、M2→M3→M4→M5 UnityEvent 串联与全链路验收。

## 十、老板需要补充的事项

- **产品决定（已拍板，无未决项）**：耦合剂口径、UnityEvent 出口、钢轨红色区域作为唯一损伤目标均已拍板。
- **视觉节点**：无（M4 未冻结，Scene 由 M4Setup 生成，不需要老板手工补节点）。
- **外部素材**：无需搜索下载（多功能尺子实物图已有）；但 `自制尺（后续有可能优化）.JPG` 表明尺子素材存在后续替换可能，尺子最终形态需老板确认。
- **验收前确认（不影响功能开工，但影响完整视觉验收）**：① M4 探头“向上 10°”的视觉倾斜方向约定（顺时针/逆时针，决定 `tiltDirection` 字段取值）；② M4 尺子选择——多功能尺子.png（规格书点名“多功能测量尺”）/ 自制尺 JPG / 纯色占位（决定 Setup 注入内容与最终截图）。红色损伤目标和声束终点不再属于待确认项。
