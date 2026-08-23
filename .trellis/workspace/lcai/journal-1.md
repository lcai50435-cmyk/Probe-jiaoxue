# Journal - lcai (Part 1)

> AI development session journal
> Started: 2026-08-07

---


## 2026-08-10 — trellis-implement: 数字人全身头像与问答动画联动（08-10-digital-human-avatar-qa-integration）

已实施（4 改 + 1 新增 runtime 组件 + meta）：
- `Assets/Scripts/M1PressDetector.cs`：新增 `OnShortPress`；长按触发后置 `_longTriggered` 抑制抬手短按（R4 互斥）。
- `Assets/Scripts/M1QAPanel.cs`：新增 `OnPanelVisibilityChanged(bool)`（Open 开始 true / 完全滑出隐藏 false）；新增 `bindPressTarget` 兼容开关（Setup 置 false，输入移交 Presenter）。
- `Assets/Scripts/M1DigitalHumanPresenter.cs`（新，148 行）：短按切全身/头像（面板打开忽略）；长按记形态+自动展开+开面板；AnswerState Idle/Thinking/Speaking 切三视频循环；面板关闭后恢复提问前形态（请求未结束延后到 Idle）；强制 `audioOutputMode=None`。
- `Assets/Editor/M1QASetup.cs`：Ensure ChatArea（右侧预留 344px）+ QAPanel 重挂其下（580 宽、hiddenOffsetX 960）；Ensure DigitalHumanStage（根级最后兄弟，320 宽/24 边距，盖过 Blocker）；FullBodyView（RawImage+LumaKey+AspectRatioFitter(1080/1450)+VideoPlayer+透明点击层+PressDetector）；AvatarView（A-05 头像 120px+PressDetector）；Presenter 注入；隐藏旧背景圆；仅引用三个指定 MP4，不加载 WebM；幂等自愈。
- `Assets/Editor/M1LayoutPolish.cs`：虚构 `QAPanel/Panel/...` 路径全部改为真实层级；面板宽 780→580、输入行排布同步（输入 360/语音 388/发送 472）。

验证：runtime+editor 全量 csc 编译零错误（Unity 6000.3.21f1 程序集）；`git diff --check` 干净；Presenter 148 行 ≤150；无残留 `QAPanel/Panel` 路径；状态视频 None/开场引导 Direct 保持；素材无 WebM；前置任务三个 mp4.meta（importAudio:0）未触碰；未输出 API Key。

未完成（人工验收）：项目被运行中的编辑器占用（Unity.exe PID 36436），未跑批处理 Setup。需用户在编辑器内：等脚本重编译 → 运行 Tools/M1/Setup AI 提问面板 两次（验证幂等）→ PlayMode 验证三态动画/长短按/自动展开恢复/三分辨率布局。

## 2026-08-10 — trellis-check 复核（数字人任务第二轮）

复核结论（3 项修复已确认/修正）：
1. **RT mipmap 可靠性**：原「GPU 每帧兜底」表述无依据，已修正。Unity 6000.3 反射确认 API 名为 `RenderTexture.GenerateMips()`（旧名 GenerateMipMaps）；Presenter 在 `Update` 中显式逐帧调用（仅 fullbody 可见时），配合 `useMipMap+autoGenerateMips=true` 原生分辨率 RT，确定性、平台无关；同步改写 video-intro.md 与 AGENTS.md 摘要。
2. **常驻材质**：`EnsureResidentLumaKeyMaterial` 改为从共享 intro 材质 `new Material(introMat)` 迁移创建（继承 Color/stencil 等全部属性），仅收窄 `_KeySmooth=0.006`（阈值 0.02 不变），独立保存 `UI-LumaKey-DigitalHuman.mat`；存在则保留用户调参；FullBodyView 自愈条件覆盖「仍指向 intro 材质」的旧场景。
3. **同位切换 (0,-248)**：Setup 中 FullBody/Avatar 均 anchor(0.5,0.5)+pivot(0.5,0.5)+pos(0,-248)，重跑无条件自愈；当前场景 AvatarView 仍是旧右上角布局（anchor 1,1 / pos -16,-16）、FullBodyView RawImage 仍指向 intro 材质 —— **必须重跑 Setup 自愈**（不手改 YAML）。

验证：runtime+editor 全量 Roslyn 编译零错误（Unity 6000.3.21f1）；Presenter 148 行 ≤150；场景无重复节点、无 `QAPanel/Panel` 虚假路径、无 WebM 引用；状态 VideoPlayer `audioOutputMode=None`、开场引导保持 Direct；三个 clip guid 严格对应指定 MP4；QAPanel `_busy` 重置移除与 R7 一致（旧 Close 本不停止协程，重置是遗留保险）；`git diff --check` 仅剩 Unity YAML `m_Name: ` 尾随空格（生成格式）。

用户待办：编辑器内重编译后运行 `Tools/M1/Setup AI 提问面板` 两次（幂等验证），再人工 PlayMode 验收三态/长短按/自动展开恢复/三分辨率；首轮验收截图未在本机找到，-248 中心值按 R13 设计意图核对。

## 2026-08-11 — M2 里程碑一静态线框第二版（task 08-11-m2-implementation）

按用户审核反馈修订 `Assets/Editor/M2Setup.cs` 并重新生成 M2.unity + 三视口截图：

1. **QAEntry 裁切修复**：根因是旧锚点 pos(-24) + 宽120 使右边缘 1932 > Canvas 1920。改为 104x64、pos(-76)，右边缘 1872；按钮改用 `Assets/DigitalHuman/A-05 折叠态头像.PNG`（Multiple 子资产名 `[A-05 折叠态头像_0]` 兼容加载，M1QASetup 的裸 LoadAssetAtPath<Sprite> 对 Multiple 会返回 null 需留意），旧 "问答" 文字子节点幂等移除；点击区 104x64 ≥ 64px，不创建全身数字人。
2. **B 区 150mm 与目标线**：CurrentDistanceText pos(-24)/宽170、TargetDistanceText pos(-208)/宽160（间距 14px 不重叠）；110mm 目标竖线从占位 x=0 移到归一化 0.8（=(150-110)/(150-100)），不再遮挡 150 刻度；WaveGraphic 内新增平直基线占位 BaselinePlaceholder（绿色横线）。
3. **探头/尺子迁移**：K2.5 探头实体入 ProbeHome 暂存位（RailViewport 内旧 Probe 删除）；尺子实体入 RulerHome 常驻、置灰锁定（alpha 0.6 + 刻度文字），RailViewport 内旧 Ruler 删除。场景各仅剩 1 个 Probe/Ruler，父节点分别 = ProbeHome/RulerHome。
4. **C 区按钮**：普通/透视按钮 160x64（原 56px 不达标）；HelpControls 按钮同调 64 并改为上下布局避免与文字重叠。
5. **钢轨层次**：RailBase(深) + RailSurface(浅) + RailHighlight(顶部高光) + RailShadow(底部暗线) + WeldLine；交互坐标契约保持（钢轨 y 0.35~0.75、焊缝 x 0.62 不变）；未用用户临时正面图，纯 UGUI 色块（M1 简洁风格，无渐变发光）。
6. **幂等与隔离**：Setup 连跑两次 M2.unity 哈希一致 d3751393（TMP 重指向 1→0）；未打开/保存 M1（M1.unity diff 保持任务前 4 行不变）；M2Shot 三视口（1920x1080 / 1280x720 / 2436x1125）已更新 Logs/m2-shot_*.png。
7. **像素级验证**（PIL，非目视）：三视口 QAEntry 头像彩色像素存在、画布右边缘无内容（1280 边距 x1264..1280 干净）、基线绿线/目标黄线/轨面/高光/焊缝均在预期坐标渲染；2436 因逻辑高 974 需按 scale 重算坐标。
8. 日志注：退出 batchmode 时 TMP 包 `m_AtlasTextures` UnassignedReferenceException 为 TMP quit 回调无害告警（Setup 逻辑后发生）；`git diff --check` 仅剩 Unity YAML `m_Name: ` 尾随空格（生成格式）。

待用户审核截图 → 审核门槛 A 通过后进入里程碑二（4 个 runtime 组件骨架）。

## 2026-08-12 — trellis-implement: M2 功能总收口（08-12-m2-final-functional-closeout）

老板授权一次性定点解冻 M2 Scene（仅限正式尺子、完整 QA 子树/组件引用、Build Settings），完成后重新冻结；M1/M3 全程不碰。主 Unity Editor 占用项目，采用已验证的隔离副本模式执行。

实施（新增 1 个 Editor 工具 + 2 个场景/配置产物）：

1. `Assets/Editor/M2FinalCloseout.cs`（新增，一次性收口工具）：QALayer 下创建 ChatArea（右侧预留 336px）→ 复用冻结空根 QAPanel → 构建 Header/MessageList/InputRow 完整子树（结构同 M1，路径无虚构层）→ Blocker 本体挂透明 Image+Button（点击关闭）→ QALayer 挂 M1QAPanel+M1DeepSeekClient（apiKey 留空）→ 注入 Presenter.qaPanel → Ruler/bg 接入多功能尺子.png（Multiple 子资产加载）、Ruler rect 420x91 保比例、zeroAnchorLocal=(-210,0)、禁用占位 ScaleText → BuildScenesSetup（M1/M2 index 0/1）。幂等 + 自愈（清理旧残留双 QAPanel 空根）。
2. 副本 `E:/Project/UnityGame/Probe-jiaoxue-m2-review` 执行 batchmode：QA/尺子接入后继续吸收老板 Scene 手调，并将尺子 Scene/Game 初态统一为 `RulerHome/Ruler`；最终 M2 冻结哈希 `3ef75ced51304258b5bde9b43be8f354b247753801a708ae52b922b5829c990b`，M1 `10884e91…`、M3 `f5446de3…` 字节全程不变。
3. M2 最终自动验收：GPU batchmode 三视口非空且 Scene 哈希不变；核心 Play Mode 与 M1→M2 生产入口均 PASS。首次单帧 150→100mm 会先钳在 110mm 完成伤损/峰值/蜂鸣同步，下一次输入再到 100mm。`M2FinalCloseout` 已删除全部写 Scene 能力，仅保留 47 行只读哈希验收。
3. 回拷产物：M2.unity（重新冻结）+ EditorBuildSettings.asset（M1/M2 index 0/1，SampleScene 移除）。

验证：离线 Roslyn 编译 + 副本 batchmode 均零 `error CS`；场景 YAML 509 块头/0 孤立块体；QA 节点单实例；M1QAPanel 路径/cnFont/Presenter/DeepSeek/Blocker 引用完整；M2 runtime 5 文件均 ≤150 行；`git diff --check` 仅 LF/CRLF 警告。M2Setup/M3Setup 保持只读打开器未动，M1QASetup 未动（M1 零回归）。

人工门槛（未自动执行，已写入 implement.md §5）：主 Editor 打开 M2 后 Play Mode 跑四阶段/角度/手动+自动 110mm/QA/重置/数字人、M1 QA 回归、M1→M2 串联；三视口只读截图。

收尾补记：上述核心流程、三视口和 M1→M2 串联后续均已由隔离副本自动验收通过，任务已归档。工作提交后主 Editor 又将 M2 `FullBodyView.x` 从 `-124` 手调为 `-13`，该后续视觉改动保留在工作区，未混入 M2 收口提交，也未被 Agent 覆盖。

## 08-13 M2 轨头顶面流程重构（m2-top-surface-flow-refactor）

规划（prd/design/implement 三件套，老板批准）→ 实施 → 双烟测 PASS → 收口。冻结 Scene 全程未动（SHA 保持 `ea4268…` 基线，数字人 x=-13 保留）。

实施要点（不新增 runtime 脚本，复用 4 脚本重构 + Smoke/Shot 工具适配）：

1. **共享几何合同**：`M2RulerDrag` 提供 `PixelsPerMm`（正式尺 0→110 锚点 preserveAspect 渲染矩形二维欧氏跨度 ÷110，实测 2.7914）；`M2ProbeDrag` 提供 RailViewport 本地像素的 `ProbeEntryPointInRail`/`DamagePointInRail`（损伤继续从 RailPerspective UV 换算）+ `ScanStart/HitPoint`（damage ∓ dir×150/110mm×ppm 反算，含入射点偏置补偿）。
2. **定位门控**：Flow 增 `AngleVerifiedByRuler`；扫描需 `Placed && AngleCorrect && AngleVerifiedByRuler`。Ruler 双模式（AngleGuide 校角三条件：Slider 10° + 10°槽对入射点 + 尺身平行钢轨 → 吸附归槽；DistanceMeasure 双点 0/110 容差 → 刚体变换吸附）。Slider 校角后锁定到 Reset。
3. **检出合同**：删除旧 `_prevMm` 跨越阈值因果；Probe 每次移动报欧氏距离并判三条件（10° + |mm-110|≤容差 + beam 端点距损伤≤容差）→ Flow 锁定探头/波形峰值/蜂鸣一次/显"下一步"。60s 帮助 AutoMoveToMm(110) 与手动同几何路径。
4. **关键教训（已写入 low-code.md 5.4）**：入射点锚点不在 Rect 中心时，ScanStart/HitPoint 必须减 `EntryOffset` 反推探头中心，否则欧氏距离合同失效（首次烟测 150mm 起点误差 0.9mm）。M1→M2 加载依赖 M1 passClip 音效时长，链路烟测改轮询+超时。冻结 Scene 旧 stepHints 数组用代码 `DefaultHints` 覆盖。
5. **验收**：编译零 error；主烟测 PASS（QA 暂停、门控单条件负例、110mm 几何、检出锁定、双点负例/正例、自动帮助、重置复跑）；M1→M2 链路 PASS；M2Shot 三视口截图无回归、Scene 哈希不变。5 个 runtime 脚本 140/142/148/119/91 行均 ≤150。

## 08-13 M2 第一轮人工反馈修复（4 项）

老板主编辑器打开验证发现 4 问题，全部处理（冻结 Scene 依旧零改动，SHA 保持 `ea4268…`）：

1. **尺子素材 + 尺寸**：换 `Assets/Ruler/尺子正面.png`（1205x213，Multiple 单子图）。冻结 Scene 无法改序列化 Sprite/measureSize → 复制素材到 `Assets/Resources/`，`M2RulerDrag.Bind` 运行时 `Resources.LoadAll<Sprite>("尺子正面")[0]` 换 sprite、强制工作态 `measureSize=320x57`（旧 420x91 太大）。新锚点（底左 UV）：0mm 左端底尖 (0.005,0.038)、110mm 竖刻线 x≈880 (0.73,0.038)、10° 斜面尖角 (0.005,0.136)；0 与 110 同底边水平线 → ppm=232/110≈2.109，150mm=316px/110mm=232px 均落视口。
2. **校角过程**：答复老板——规划（design.md §4）即"Slider 驱动 10°（探头视觉随 Slider 旋转）+ 尺子三项校验吸附"，尺子不直接驱动探头旋转；给出增强选项待确认。
3. **校角后探头拖不动（bug）**：根因 `Go(Scanning)` 用 `SetInputLocked(true)` 同时锁了角度与拖拽。新增 `M2ProbeDrag.SetAngleLocked`（只锁 Slider），`Go(Scanning)` 改用它；`SetInputLocked` 仅检出后/帮助演示用。连带修复 `M2IdleHelp` 演示结束改为按 `Detected` 锁定（原按 stage==Scanning 会锁死拖拽）。
4. **绿色射线不可见**：根因 `BeamLayer` Scene 初态 active=0 且 `ApplyView` 普通视图强制隐藏。改 `ApplyView` 不再控制 beamLayer；`ShowBeam` 激活 beamLine 父级 → 扫描阶段普通/透视视图均显示绿色检测束。

验收（主项目被老板编辑器占用，用隔离副本 `Probe-jiaoxue-smoke` 跑）：主烟测 PASS（含反向/平移双点负例替换——新素材 0/110 同底边线，水平放置单点对齐=双点对齐，旧负例失效）、M1→M2 链路 PASS、三视口截图正常、5 脚本 140/143/150/91/119 行。副本与临时裁剪图已删除。

## 08-13 M2 夹具式校角重构（老板定稿方案）

老板否决"吸附即归槽"，定稿 7 步操作链：放探头(0°) → 拖尺子吸附成夹具 → 解锁 Slider → 沿槽调角 → 10° 稳定 0.5s → 正确音效+锁定 10° → 手动撤尺 → 绿色检测束+扫描。实现要点：

1. `M2RulerDrag`：`CheckAngleGuide` 移除角度条件（吸附=槽位+平行，保留现场不 ResetTool）；新增 `UnlockRetract`（撤尺解锁）、`SetPoseRetract`/`CheckRetract`（以 RulerHome 世界位置为靶拖回归槽）、`OnAngleRetracted` 事件；删 `SetPoseZeroOnEntry/SetPose110OnDamage/Pose`（旧 Smoke 负例辅助）。
2. `M2FlowController`：新状态 `RulerDocked`；`NotifyRulerAligned`→吸附解锁 Slider；`NotifyAngleConfirmed`→锁角度+正确音效+解锁撤尺；`NotifyRulerRetracted`→Go(Scanning)。删 `TryEnterScanning/NotifyAngleCorrect`（门控变为顺序链）。
3. `M2ProbeDrag`：`Update` 稳定计时（RulerDocked && AngleCorrect 累积 Time.deltaTime 0.5s）；`SetAngleLocked`（Slider）与 `SetInputLocked`（拖拽）职责分离；Bind 锁 Slider + probeVisual pivot 改 (0.5,0) 绕入射点旋转（含位置补偿）；ResetTool 恢复角度锁。
4. `M2IdleHelp`：30s 帮助按新链演示（吸附→调角→等 0.6s→撤尺）。
5. `M2RuntimeSmoke`：case1 改为夹具链断言（放置后 Slider 锁、吸附后解锁+保留现场、稳定后确认+锁定、撤尺进扫描）；case2 撤尺断言后接几何/检出。

验收（隔离副本）：主烟测 PASS、M1→M2 链路 PASS、三视口截图正常；5 脚本 147/148/145/93/119 行；M2.unity SHA 保持 `ea4268…`。规范 low-code.md 5.4 更新为夹具式合同。

## 08-13 M2 第二轮人工反馈（探头素材 + 射线美化）

老板两张截图反馈：① 探头初始位置/观感错误；② 绿色射线难看。

根因定位：
1. **探头素材用错**：M2 bg 用的是 `K2.5.PNG`（K2.5 斜探头 3D 立体侧视图，属 M3 轨头侧面场景）；M2 是轨顶面直探头偏转 10° 场景，立体斜楔块放俯视钢轨上视觉"悬浮、倾斜、不贴轨"。修复：复制 `0度.PNG`（直探头）到 `Assets/Resources/probe0.png`，运行时 `Resources.LoadAll<Sprite>("probe0")[0]` 换 bg sprite（不改冻结 Scene）；手动生成 probe0.png.meta（新 guid/内部 ID，Multiple 单子图，参照源素材导入设置）。探头放置几何本身正确（ScanStart 在 railViewport 内、入射点与损伤同水平、位于钢轨图上）。
2. **射线难看**：BeamLine 是单条实心绿 Image。美化（纯程序化，不改 Scene/素材）：`BeamGradient()` 生成 8x64 渐变 Sprite（底部亮→顶部透明 + 横向高斯），UpdateBeam 换成渐变 sprite、pivot 改 (0.5,0)（探头端为起点），Update 每帧亮度脉冲（alpha 0.3~0.8 sin 波动）形成"超声波光柱"感。BeamLine 是 RectTransform，需 `GetComponent<Image>` 缓存为 `_beamImage` 再改 sprite/color（踩坑：RectTransform 无 color/sprite）。

验证：副本编译零 error；主烟测 PASS（夹具链/110mm/双点/自动帮助/复跑全过，许可证 Handshake 错误仅警告）；M1→M2 链路 PASS。M2ProbeShot 临时截图工具不稳定（线程断言）已删除，视觉以老板主编辑器目视为准。5 脚本 147/149/145/93/119 行；M2.unity SHA 保持 `ea4268…`。

## 08-14 M2 PPTX 流程重构（焊缝目标 + 新素材，老板定稿）

老板按 `文档/M2轨头顶面探测.pptx` 重新定稿 M2 流程：3 步 = 钢轨左侧中心线 0° 放置 → 定位尺向内偏 10° → 前移至入射点距本侧焊缝熔合线 110mm。核心变化：**110mm 目标从「红色损伤」改为「焊缝熔合线（WeldLine）」，红色损伤仅作透视可见缺陷；起始从「150mm 起点」改为「钢轨左侧中心线（startLocal）」。** 素材全部换新。

实施（不新增脚本、不写冻结 Scene，全部运行时替换）：

1. **素材 → Resources**：复制 `probeFootage.png`（直探头）、`railwayTracks_2/俯视角.png`（普通）、`俯视角透视.png`（透视）到 `Assets/Resources/`，手工生成 Single-mode meta（新 guid + spriteID）；尺子沿用已有 `尺子正面.png`。射线参照 `greenLight.png` 程序化生成锥形收窄 + 端点光晕。
2. **M2ProbeDrag**：删 `damageUv`/`scanStartMm`，新增 `startLocal(-500,0)`；`WeldPointInRail` 取代 `DamagePointInRail`（经 `flow.rulerDrag.weldLineRt` 反推，兜底 `railViewport.Find("WeldLine")`）；`ScanStart=startLocal`、`HitPoint=weld-dir*110*ppm-EntryLocal`；`BeamGradient` 重做（32×128 锥形+端点光晕）；Bind 换 probeFootage sprite。
3. **M2RulerDrag**：尺子换 `尺子正面`（0/110 底边 `(0.005,0.038)/(0.73,0.038)`、10°槽 `(0.005,0.136)`、measureSize 320×57、ppm≈2.109）；`CheckMeasure`/`OrientMeasure` 110 目标改 `WeldPointInRail`。
4. **M2FlowController**：`SwapRailSprites` 运行时换 `俯视角`/`俯视角透视`；步骤文案改「入射点距焊缝 110mm」「110mm 对焊缝熔合线」。保留耦合剂 + 独立复测（②A③A）。
5. **M2RuntimeSmoke**：素材/ppm 断言改为引用比较（尺子正面/probeFootage/俯视角 v2，ppm 2.109/232px）；150mm 起点断言改「起始在焊缝左侧」；110mm 断言改「距焊缝」。5 脚本 150/150/149/93/119 行。

验收：代码静态一致（无残留 DamagePoint 引用）；**未跑 Unity 编译/烟测**（老板主编辑器占用中）。待老板编辑器内：重编译 → 跑 `M2.EditorTools.M2RuntimeSmoke.RunBatch` + `RunM1ToM2Batch` → 人工目视校准 `startLocal`/`probeEntryLocal`/尺子锚点/射线观感；确认后 M2.unity SHA 仍须等于基线 `ea4268…`。

## 08-14 M2 第二轮反馈（射线瞄准损伤 + Scene 素材同步 + 模糊排查）

老板截图反馈三点，全部处理：

1. **射线目标回退「红色损伤」**（老板明确「瞄准红色损伤中间」，取代 PPTX「焊缝熔合线」口径）：`M2ProbeDrag` 删 `WeldPointInRail`，恢复 `DamagePointInRail`；`CalibrateTrack` 用实测 `damageUv=(0.4808,0.711)`（对 `俯视角透视.png` 2469×609 红椭圆做像素采样得到，替代旧 RailPerspective 的 `(0.4798,0.6875)`）。
2. **射线水平发射 + 出发点抬高**：根因是 `ScanStart=startLocal` 未补偿 `EntryLocal`，导致入射点 y 偏离损伤中心线、射线斜向下。改 `ScanStart=(startLocal.x, damage.y)-EntryLocal`、`StartMm=distance((startLocal.x,damage.y),damage)/ppm`，使入射点全程保持在损伤水平线；`probeEntryLocal` 从 `(0.5,0)` 抬到 `(0.5,0.25)`（probeFootage 图里设备不在图底部，底边是空白）。
3. **Scene 素材同步（老板授权写 Scene）**：把 M2.unity 序列化 `m_Sprite` 的 4 处 guid/fileID 改指向新素材（尺子→尺子正面 fileID 5987772278907439635、钢轨普通/透视→俯视角 v2/俯视角透视 v2 fileID 21300000、探头→probeFootage fileID 21300000），Scene 视图与 Game 视图一致。注意去掉 Set-Content 引入的 UTF8 BOM（否则首行 `%YAML` 带 BOM）。
4. **模糊排查**：新素材 `probeFootage`(2610px)/`俯视角`(2469px) 超 `maxTextureSize 2048` 被降采样 + `textureCompression 1`（DXT 压缩）导致模糊。已把 3 个 Resources meta 改为 `maxTextureSize 4096` + `textureCompression 0`（无压缩）。

M2.unity 新 SHA-256 = `4fd7a85ae8dcb8b448504aa82ae23d84eccdb07e4e9482faf8368793f769a074`（含老板数字人 x=-13 调整 + 本次 4 处 sprite 替换）。5 脚本 150/150/149/93/119 行。待老板编辑器内：Reload Scene（因我改了 Scene YAML）→ 重编译 → 目视校准 `probeEntryLocal`(约 0.25)/`damageUv`/`startLocal` 三个值。


## Session 1: M5 擦拭耦合剂模块交付（M2 基线 + 擦拭交互 + m4 分支合并）

**Date**: 2026-08-23
**Task**: M5 擦拭耦合剂模块交付（M2 基线 + 擦拭交互 + m4 分支合并）
**Branch**: `feature/m5`

### Summary

M5 擦拭耦合剂模块完整交付：M2 轨顶基线 + 耦合剂薄膜（初始铺满/擦拭递减）+ 擦拭布拖拽（拖出吸附钢轨最左/相对偏移跟随）+ MainScene/Tool 工具架（Probe/Ruler 静态展示、Rag 可拖、清晰不置灰）+ 保留 M2 波形窗口（M2WaveformFx 150/115/110）+ 数字人 Bootstrap 壳模式（FullBodyView Scene 壳 + ??AddComponent 伪 null 修复 + try-catch 日志）+ 完成面板不显示 + 多轮 Scene 权威约定（钢轨/透视/耦合剂/数字人舞台/工具架布局 Setup 不覆盖）；trellis-check 清理死代码常量并同步 prd 验收；最后合并 feature/m4-rail-web（M1/M2/M3/M4 以 m4 为准、M5 以当前分支为准，Bootstrap/rag meta 用当前分支版，AGENTS.md 手动合并）。

### Git Commits

| Hash | Message |
|------|---------|
| `8868893` | (see git log) |
| `fc63513` | (see git log) |
| `2ad1f44` | (see git log) |
| `a299270` | (see git log) |
| `784758e` | (see git log) |
| `f0e34ea` | (see git log) |
| `36197e3` | (see git log) |
| `7f4dace` | (see git log) |
| `9c51a50` | (see git log) |
| `fecdfce` | (see git log) |
| `7f3473e` | (see git log) |
| `7645685` | (see git log) |
| `80d7ae5` | (see git log) |
| `b3c4084` | (see git log) |
| `26d266e` | (see git log) |
| `9aede40` | (see git log) |
| `ed218d6` | (see git log) |
| `6cc6c9b` | (see git log) |
| `b00727c` | (see git log) |
| `3384d66` | (see git log) |
| `54acb42` | (see git log) |
| `c9ea4dd` | (see git log) |
| `2d75339` | (see git log) |
| `7f2a745` | (see git log) |
| `53d53a0` | (see git log) |
| `d6ea725` | (see git log) |
| `9367be9` | (see git log) |
| `ec63ff2` | (see git log) |
| `0cb0e30` | (see git log) |
| `4aa9fdd` | (see git log) |
| `d4cf09c` | (see git log) |
| `7b49749` | (see git log) |
| `e364ad4` | (see git log) |
| `bd9830d` | (see git log) |
| `17a2fbc` | (see git log) |
| `0140d85` | (see git log) |
| `2bd853b` | (see git log) |
| `ac78955` | (see git log) |
| `964f0ad` | (see git log) |
| `26b9ce2` | (see git log) |
| `dc2a50d` | (see git log) |
| `2e8d6f8` | (see git log) |

### Status

[OK] **Completed**
