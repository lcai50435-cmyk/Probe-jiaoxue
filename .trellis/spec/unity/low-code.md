# Unity 低代码开发规范（权威版）

> 本规范适用于本项目所有 AI 编码任务（runtime 与 Editor 均适用，另有说明除外）。
> `AGENTS.md` 总纲是本规范的摘要，两者冲突时以本文档为准；修改规范必须先改本文档，再同步总纲。

---

## 1. 低代码决策树

任何新功能按以下顺序决策，命中即停：

1. **能否纯配置实现？**（Inspector 字段、场景对象、现有组件组合）→ 写配置，不写代码。
2. **能否复用现有组件？**（`Assets/Scripts/` 现有脚本或其他模块组件）→ 复用，必要时加配置项。
3. **能否用 Editor 工具实现？**（重复性搭建/生成逻辑）→ 写或改 `Assets/Editor/` 工具脚本。
4. **以上皆否** → 新增 runtime 脚本，并在方案/说明中写明理由。

## 2. Runtime 脚本规范

- 新增 runtime 脚本默认 **≤150 行**；超限先拆分职责或改为配置驱动，仍超限须说明理由。
- 命名：`M{模块}{职责}`，如 `M1QAPanel`、`M1ToolSelection`、`M1PressDetector`。
- 只写配置表达不了的核心逻辑；UI 布局、数据、文案一律不进代码。
- 不主动重构存量代码（存量超标如 M1QAPanel.cs 384 行属于历史遗留）；仅当任务本身涉及该文件时顺手精简。

## 3. 配置化规范

- 数据/参数进 ScriptableObject、Inspector 字段或场景配置，禁止硬编码魔数、文案、尺寸。
- 配置项必须有合理默认值，保证组件拖入场景即可用。
- 批量数据（如题目、工具列表）优先 ScriptableObject 资产。

## 4. Editor 工具规范

- Editor 工具（`Assets/Editor/`）**豁免 150 行上限**——它是低代码的放大器，一次生成省掉大量手工配置；但仍须精简、复用、不硬编码。
- **幂等要求**：Setup 类工具重复执行不得产生重复对象（参考 `M1Setup.cs` / `M1QASetup.cs` 现状）。
- 生成对象的命名可预测，方便后续查找与验证。
- 修改 Setup 工具时需说明重新生成对现有场景的影响。

## 5. 场景改动规则

- 未冻结模块的**结构改动**（增删元素、布局参数、锚点/尺寸）→ 改 `Assets/Editor/*Setup.cs` 后重新生成场景。
- 未冻结模块的**纯视觉微调**（单个对象的颜色、文字、字号）→ 可直接改场景文件，不动 Setup。
- 禁止两种方式混合导致 Setup 与场景漂移；改动后建议跑一次 Setup 验证幂等。

### 5.0 M2/M3 场景冻结例外（2026-08-12 产品决定）

- `Assets/Settings/Scenes/M2.unity` 与 `Assets/Settings/Scenes/M3.unity` 的当前 Scene 文件是冻结后的视觉权威。后续程序、Setup 和 Agent 不得修改、重生成或保存覆盖；只有老板在 Unity Scene 中手工修改才允许改变视觉。
- `M2Setup` / `M3Setup` 只能打开或检测现有 Scene 并明确日志后返回；Scene 不存在时必须报错，二者均不得创建。历史生成代码只能保留为不可调用的参考实现，不得重新接回入口。
- 旧的“结构改动走 Setup”“Ensure 自愈已有节点”“双跑保存验证幂等”规则不适用于这两份冻结 Scene。调用 M2/M3 Setup 的验收标准是调用前后文件字节哈希完全一致。
- 后续 M2/M3 功能只能通过 runtime 动态绑定、复用已有节点或添加不改变视觉的组件完成。禁止 Setup 重写 `RectTransform`、`Graphic`/TMP、文案、颜色、Sprite、active 状态或 sibling 顺序，也禁止借功能修改绕过冻结。
- 若需求必须改变 M2/M3 视觉、层级或序列化视觉状态，应停止实现并交由老板手工修改 Scene；不得修改 Scene YAML 或恢复旧生成入口。

## 5.1 路径契约（防静默失效）

- Setup 写入运行时组件的路径字段必须与生成的真实层级**逐层一致**，不得包含虚构中间层；运行时组件按路径查找失败的默认行为是**报错而非静默跳过**。
- 教训：`M1QAPanel` 路径曾含虚构 "Panel" 层（`QAPanel/Panel/Header/...`），导致关闭/语音/发送按钮与输入框静默失效、发送按钮永久置灰，排查成本高（2026-08-07 归档）。
- 教训：用户改名节点（`物品 → M1物品`）后，`M1ToolSelection.toolsRootPath` 与 `M1Setup` 素材注入路径同步失效，运行时 LogError 跳过全部工具绑定（2026-08-10）。改名/移动节点必须同步 Setup 与运行时默认值；未冻结 Scene 跑一次 Setup 自愈，M2/M3 冻结 Scene 则只允许老板手工修正 Scene。

## 5.2 场景 YAML 手改陷阱（块头丢失）

- 用正则重组 Unity YAML 块时必须**保留 `--- !u!1 &xxx` 块头行**：`re.search(r'--- !u!1 &%s\n(.*?)...')` 中块头在匹配范围内，重组 `text[:m.start()] + block + text[m.end():]` 会丢掉块头，产生孤立块体行（`GameObject:` 前没有 `--- !u!1 &xxx`），Unity 打开报 YAML 错误。
- 正确做法：重组时补回 `m.group(1)`（块头），或只替换块内字段（如 `m_IsActive`）不重组整块。
- 手改后必验：块头数与块体数配对（`grep -c '^--- !u!1 &'`）；孤立块体行（前一行不是 `--- !u!` 的 `GameObject:`/`RectTransform:` 等）为 0。
- 教训：2026-08-10 M1-2 场景手改连续丢两次块头（M1物品/M2物品/画板），均靠此校验兜住。

### 5.3 UGUI 运行时事件绑定（2026-08-11 M2）

- `Button.onClick.AddListener`、`Slider.onValueChanged.AddListener` 和普通 C# event 在 Editor Setup 中注册的委托只存在于当前编辑器进程，**不会**写入场景的 `m_PersistentCalls`；重新打开场景后会丢失。
- 未冻结 Scene 的 Setup 只负责序列化对象引用、默认参数和层级；M2/M3 冻结 Scene 不再接受 Setup 写入。每个 runtime 组件必须在 `Awake` / `OnEnable` 由事件所有者执行幂等绑定：先 `RemoveListener` / `-=`，再 `AddListener` / `+=`。例如 Flow 绑定流程按钮，Probe 绑定角度滑块与距离事件，Ruler 绑定对齐事件，IdleHelp 绑定帮助按钮。
- 验证必须包含“保存并重新打开场景后再进入 Play Mode”；`m_Calls: []` 对纯运行时绑定是预期结果，不能将其当作失效证据。

### 5.4 冻结 Scene 的运行时文案与几何合同（2026-08-13 M2 重构）

- **旧文案数组不能写回**：冻结 Scene 序列化的 `stepHints`（含“150→100mm”“0 刻度对齐焊缝”）等旧数组只能在冻结前改；运行时组件用代码静态默认数组（`DefaultHints`）覆盖 `instructionText`，Scene 反序列化对缺失字段直接忽略，不报错。
- **唯一 mm 比例**：尺子 0→110 锚点标定跨度为唯一物理比例，`pixelsPerMm = distance(zero, ruler110) / 110`。两锚点必须位于正式尺同一条可见刻度基线上——M2 换用 `尺子正面.png`（1205×213）底边基线：0mm 左端底尖 `(0.005,0.038)`、110mm 竖刻线 `(0.73,0.038)`、10° 槽尖角 `(0.005,0.136)`，工作态 `measureSize=320×57`、`ppm≈2.109`（110mm 跨度≈232px）。再取 preserveAspect 渲染矩形中的二维欧氏距离；透明区域、字样位置或不同高度点均不是有效测量锚点。
- **110mm 目标 = 红色损伤（2026-08-14 老板定稿，取代 PPTX「焊缝熔合线」口径）**：测量/检出/检测束目标为透视图中的红色损伤——`俯视角透视.png` 红椭圆，`M2ProbeDrag.CalibrateTrack` 用 `damageUv=(0.4808,0.63)`（底左 UV，2026-08-14 老板反馈「探头初始位置太高」后由红椭圆质心 `0.711` 下移至椭圆下部/下缘，视口本地约 `(-24,+93)`，使扫描线/探头下移贴普通视图钢轨踏面；110mm 刻线视觉上仍贴着红椭圆底部）从 `RailPerspective` Rect 换算 RailViewport 本地坐标；WeldLine 节点不再作目标。
- **探头发射面锚点（2026-08-14 老板反馈「校角/测量探头与尺子遮挡」）**：`probeFootage.png`（2610×906）为「左上电缆 → 右下楔形发射面」结构，发射面底左 UV≈`(0.89,0.04)`（像素验证）；`probeEntryLocal` 必须锚定发射面——`(0.5,0.6)` 旧值使入射点在探头中部，校角时探头主体盖住尺子左端、测量时尺子 0mm 端压探头。改为 `(0.89,0.04)` 后：校角时发射面卡入尺子 10°槽、主体在尺子左侧无遮挡；测量时尺子水平 0mm 端轻触发射面、探头与尺子平行无遮挡；检测束起点（`ProbeEntryWorld`）从发射面视觉位置发出。运行时 `Bind` 覆盖 Scene 旧值不写回。
- **起始位置与射线跟随角度（2026-08-14 老板定稿）**：探头水平移动——`ScanStart = startLocal - EntryLocal`、`HitPoint = (damage.x - 110*ppm, startLocal.y) - EntryLocal`，入射点始终在钢轨中心线（y=0，`startLocal` 默认 `(-500,0)`），删旧 150mm 起点概念。**射线是直线、从探头发射面（老板图2 红色框框：连接器与主体交界处）垂直射出，跟随探头角度一起旋转**：共享滑条转角 `TiltAngle = (角度/targetAngle)*visualTiltAtTarget`；探头视觉 `probeVisual.localRotation = probeBaseAngleDeg + TiltAngle`、射线 `beamLine.localRotation = beamBaseAngleDeg + TiltAngle`，二者各自带独立基准角。`probeBaseAngleDeg`（探头图片基准角，老板称"初始图像角度偏高"）与 `beamBaseAngleDeg`（射线基准角，用来把射线转到垂直于发射面）均为 Inspector 可调；射线禁止固定朝损伤/朝右。
- **检出条件**：Scanning 阶段 + 角度 10° 容差 + `|distance(entry, damage)/ppm - 110| ≤ distanceToleranceMm`；检出后探头位置硬锁定（`MoveToScan` 对 `flow.Detected` 直接 return）。
- **检出视觉反馈（2026-08-16 老板确认）**：检出瞬间在报警蜂鸣同时，射线由绿色变为橙色（`M2ProbeDrag.beamDetectedColor` Inspector 可调；橙色使用独立渐变 Sprite，避免对绿色 Sprite 直接 tint 成暗橄榄色），Reset 后恢复绿色。
- **素材替换（运行时 + Scene 序列化双写）**：探头 `probeFootage.png`、钢轨普通 `俯视角.png`/透视 `俯视角透视.png`（railwayTracks_2 v2）、尺子 `尺子正面.png` 复制到 `Assets/Resources/`（Single/Multiple Sprite meta 手工生成）运行时 `Resources.LoadAll` 换图；同时把 M2.unity 序列化 `m_Sprite` 的 guid/fileID 同步改指向新素材，使 Scene 视图与 Game 视图一致。新素材纹理 `maxTextureSize=4096` + `textureCompression=0` 避免大图被降采样/压缩导致模糊。射线参照 `greenLight.png` 程序化生成（锥形收窄 + 端点光晕，`M2ProbeDrag.GetBeamSprite`）。
- **夹具式校角合同（2026-08-13 老板定稿，替代早期“吸附即归槽”）**：定位阶段操作链为 放探头(0°) → 拖尺子吸附成夹具（仅校验 10°槽对入射点 + 尺身平行，**不校验角度、吸附后保留现场不归槽**）→ 解锁 Slider → 玩家沿槽调角 → 角度 10° 稳定 0.5s（`M2ProbeDrag.Update` 用 `Time.deltaTime` 累积，QA 暂停不推进）→ 播放正确音效并锁定 10° → 解锁尺子 → 玩家拖回工具架（`CheckRetract` 以 `RulerHome` 位置为靶，容差内自动归槽）→ 撤尺事件触发进入 Scanning 并显示绿色检测束。角度锁与拖拽锁分离：`SetAngleLocked` 只控 Slider interactable，`SetInputLocked` 只控 `_inputLocked`（拖拽）。
- **M1→M2 链路烟测**：M2 场景在 M1 通关音效播完后才加载（`LoadSceneAfterSfx` 用 `WaitForSecondsRealtime(passClip.length)`），链路烟测必须轮询场景名并设超时，禁止固定等待。
- **2026-08-14 PPT 四要点（M2 待修改部分.pptx 定稿）**：
  - **尺子双步骤统一尺寸**：校角（`ShowAngleGuide`）与测量（`ShowMeasure`）统一使用 `measureSize=420x91`；`angleGuideSize` 字段保留但运行时不再使用；`M2RulerDrag.measureSize` 代码默认值即 `420x91`（ppm≈2.768，0→110 跨度≈304.5px）。
  - **测量尺水平放置**：测量模式 `localRotation = 0`（与探头移动方向/钢轨平行），禁止按 `zero→110` 与 `ProbeEntryPoint→DamagePoint` 向量自动斜定向；前提是**扫描轨迹线与损伤点同线**（`scanLineY = damage.y`，2026-08-14 老板确认），150mm 起点由 `damage - scanDirection*150*ppm` 反算，`startLocal` 不再作为几何距离依据（旧值 `(-500,-18)` 与 damage 欧氏距离 182mm，150mm 合同从未真正成立）。冻结 Scene 旧值 `measureAngleDeg=9.55` / `measureOffset=(19,28)` 会覆盖代码默认 0/zero 导致尺子斜置与 0mm 锚点偏移（烟测「测量尺未水平」「复跑测量失败」失败根因）：`M2RulerDrag.Awake` 运行时覆盖为 `0 / Vector2.zero`（PPT 合同，不写回）。
  - **波形简化合同**：参考「焊筋轮廓波」仪器屏——深灰底 + 浅黄绿主网格/青次网格 + 橙红波形（平直基线 + 110mm 尖峰，检出后锁峰）；运行时隐藏 `WaveStateText`/`CurrentDistanceText` 并删除 `waveStateText` 写入逻辑，界面不得出现「峰值锁定/目标 110mm/平直基线/112mm/当前距离」提示词；`M2WaveformGraphic.Awake` 强制橙红 `(0.898,0.322,0.2)`（冻结 Scene 旧绿被覆盖，不写回）；`MeasurementBubble` 序列化「110mm」字样运行时改为「测量完成」；`M2WaveformGraphic.peakTargetMm` 目标 110mm，X 轴窗口 150→100，玩法 110mm 检出即锁峰，无峰后下降段。
  - **波形契约分叉（2026-08-15 二轮定稿，Scene 直做）**：M2 迁移到真实探伤仪屏风格，**2026-08-15 老板授权直接改 `M2.unity` 波形窗口区域**（首轮运行时挂载方案因 `M2WaveformFx` 缺 `RequireComponent(CanvasRenderer)` 导致 Play 下不渲染被否决）：
    - Scene：`WaveformArea_B` 4:3（sizeDelta 460×345、anchoredPosition.y=172.5 保下缘贴底）；删 `WaveHeader`（提示词节点 WaveStateText/CurrentDistanceText/TargetDistanceText）与 `WaveGraphic`（旧 M2WaveformGraphic）；`WaveGrid` 全 stretch 并序列化挂载 `M2WaveformFx`；新增 `ScaleTexts`（横轴 0.0/40.0/80.0/120.0/160.0/200.0mm 6 个 + 纵轴 0.0/20.0/40.0/60.0/80.0/100.0 6 个 TMP；纵轴 pivot 必须 (0,0.5) 文字在窗口内，pivot (1,0.5) 会被裁）。
    - 绘制（`M2WaveformFx`，[RequireComponent(CanvasRenderer)] 必须有）：深色底 + **点状"+"网格**（5 等分交叉点画"+"，参考图风格，无连续线）+ 常驻绿色始波（发射脉冲尖峰 X 0~7.5% 宽，不画青绿竖线）+ 底部绿色锯齿噪声基线（固定正弦叠加种子，无闪烁）；伤损波与始波同形同色（共用 `DrawPulse`：陡升 20% + 指数衰减，不振荡；纹波钳制在波形区内），X 轴按 0~200mm 映射（150mm→75%、115mm→57.5%、110mm→55%），`SetDistanceMm` 三区间：>150 无波 / 150→115 短波长高（峰高 8%→78%）/ 115→110 保持最高左移 / <110 检出锁定不再变；纯状态驱动无协程，QA/Modal 暂停时无距离输入天然冻结。
    - `M2FlowController`：`waveformFx` 为 Scene 序列化引用；`NotifyDistance`/`ResetAll` 走 `waveformFx.SetDistanceMm/ResetWave`；旧 `waveform`（M2WaveformGraphic）字段与 WaveStateText/CurrentDistanceText 字段删除。M3 的 `M2WaveformGraphic` 旧样式与配置零改动。
    - `M2WaveformFx` 代码默认值已更新为 `appearMm=160 / peakMm=123 / stopMm=120`（后续新场景/新组件默认按此生成）；M2 Scene 仍序列化 150/115/110，M3/M4 在 Scene/Flow 中显式配置。
  - **烟测断言**：新增「校角/测量同尺寸」「入射点与损伤同线」「测量尺水平」「波形提示词隐藏」「波形橙红」断言；ppm 断言为 2.768/304.5px（420×91 基准）。
- **2026-08-16 M3 轨头侧面按 PPT 对齐（老板授权 Scene/Play 同步）**：
  - 流程不再播放自动耦合剂 Intro，进入 M3 直接定位（无 2 秒耦合剂薄膜/开场延迟）；定位→扫描→测距→完成；扫描距离 `160→120mm`，到达 120mm 检出并锁定，不再走到 100mm。
  - 目标点以伤损为主；测量阶段为尺子 `0→120mm` 双点校验：0 对齐探头入射点，120 对齐伤损。
  - M3 波形复用 `M2WaveformFx`，参数 `appearMm=160`、`peakMm=123`、`stopMm=120`；初态 160mm 短波，123mm 最高，120mm 锁定。
  - 射线复用 M2 绿→橙检出反馈；`M2ProbeDrag.GetBeamSprite` 改为 public static 供 M3 复用。
  - **M3 射线长度/检出合同（2026-08-16 老板三轮定稿）**：
    - **目标线 = 红椭圆（伤损）下边缘**（正视角透明.png 2292×740 采样红椭圆 y 194/740，rail 局部 (0.5-194/740)×323≈76.8，世界 y≈52.8），不是 red 条上边缘（69=伤损中心线）——用上边缘 drop=34.4 使临界角 2.3° 起就缩、前 4° 变化剧烈。
    - **射线长度**：`长度 = min(默认, (entryY - 椭圆下边缘Y)/sin(角度))`；默认 `beamLengthZeroMm` **运行时覆盖 200mm**（Scene 旧值 300 会使临界角 2.3° 起缩）→ 临界角 ≈5.2°，**前 ~5° 长度完全不变**，之后平滑缩到 13° 末端精确落在红椭圆下边缘；min 语义天然连续无突变（仅留 sin≤0.001 / drop≤1 防除零）。
    - **检出 = 射线末端实际到达/越过伤损**（不是方向对准就算）：`BeamHitsDamage` = 伤损在射线前方且横向距离≤束宽，**且 `BeamLenPx(角度) ≥ 入射点到伤损的沿射线距离 - 束宽容差`**——射线没长到伤损就不会蜂鸣。
    - **扫描起点恢复 160→120mm**：`Bind` 运行时覆盖 `scanStartMm=160`（Scene 旧值 120.96 使探头一放下就在检出位、一拖就蜂鸣）。
  - M3 Scene 波形区按 M2 定稿同步：460×345、`WaveGrid` 挂 `M2WaveformFx`、新增 `ScaleTexts`、删除旧 WaveHeader/WaveLine/TargetMarker/Scale150/Scale100；尺子换 `尺子正面.png`。
  - M3 拖动按钮样式同步 M2：`AngleTrack` 改为深灰圆角粗条（300×48、Sprite 10905/Type=Sliced），`Fill` 与 `Handle` 使用同款圆角 Sprite；`Handle` 改为 M2 同款细长圆角条（32×48、锚点 y 0→1、初始 x=0），视觉与 M2 `AngleSlider` 一致。
- **2026-08-16 M2 检出即测距（老板定稿，与 M3 一致）**：
  - **检出即测距**：射线照到伤损检出瞬间探头锁定，**直接** `rulerDrag.ShowMeasure() + Go(Measuring)`，玩家可直接拖尺测量——无"下一步"按钮门控；`nextButton` 不再激活且 `NextToMeasure()` 删除，`M2FlowController` 不再绑定 nextButton，ResetAll 仍隐藏该节点。
  - **测量姿态合同补缺**：`M2RulerDrag.Awake` 运行时强制 `measureAngleDeg=0`、`measureOffset=Vector2.zero`（PPT 合同：测量尺水平放置、0mm 锚点贴入射点；冻结 Scene 旧序列化 9.55/(19,28) 会破坏"测量尺未水平"烟测断言，不写回）。
  - **警告（2026-08-16 返工教训）**：尺子工作态 `localScale` 一律保持 `Vector3.one`（`EnterWorkMode` 强制），**禁止**为适配 Scene 根缩放（如 0.8）而折算 `PixelsPerMm`——ppm 是探头扫描起点/命中点几何（`damage - mm*ppm`）的唯一依据，ppm 变化会改变探头初始放置位置（老板硬性要求保持不变）。Scene 中 Ruler 根 `localScale` 仅影响工具架（Home）显示；工作态尺寸由 `measureSize` 决定。

- **2026-08-18 M5 擦拭耦合剂（复用 M2 UGUI 骨架，单步交互结束模块）**：
  - 流程：起始（M2 轨头顶面视角 + 钢轨顶面涂蓝色耦合剂）→ 玩家拖擦拭布（rag.png）至钢轨顶面 → 左右拖动控制擦拭范围（进度跟手）→ 100% 通过。**无探测流程、无下一模块**（完成面板显示"M5 擦拭耦合剂完成"，enterNextButton 不显示；onCompleted UnityEvent 保留可配置）。
  - 耦合剂视觉 = 从 `俯视角.png` 切 `coverRect=(.005,.222,.993,.553)` 子矩形（铁轨主体，覆盖轨顶中央大部分，老板 2026-08-18 确认）的蓝色半透明薄膜（`M5CouplantFx`，filmColor=(.55,.8,.96,.45) 同 M2）。**状态与 M2CouplantFx 相反**：初始 `fillAmount=1` 铺满（M2 是 0→1 动画后淡出）；擦拭进度 p → `fillOrigin=1`（右对齐剩余）+ `fillAmount=1-p`（已擦左侧消失、剩余在右侧，与拖动方向一致）。
  - 擦拭布拖拽（`M5RagDrag`）：Home（RagHome 槽位置灰锁定 color=(.55,.57,.6,.62)）→ 拖出进工作态（挂 RailViewport、锚定 pivot、跟手）；x 限制在钢轨顶面擦拭区间（wipeRect 同 coverRect 的 x 范围换算 railViewport 局部像素），y 贴 railBg 中心线；`progress = clamp((x-left)/(right-left))`。
  - `M5FlowController`：Stage { Wipe, Completed }；`NotifyWipeProgress(p)` → `couplantFx.SetWipeProgress(p)`，p≥1-0.001 时锁定 + 正确音效 + Completed。Reset 恢复（铺满/归槽/回 Wipe）。普通/透视切换复用 M2 行为，**透视视图隐藏耦合剂层**（擦拭发生在普通视图）。
  - Scene 由 `M5Setup.cs` 生成（未冻结模块）：复用 M2 骨架（Canvas 1920x1080/Match0.5、SafeArea/HeaderBar/MainScene(RailArea)/ControlDock_D/QALayer/DigitalHumanStage/ModalLayer），无波形窗口/探头/尺子流程节点；**ToolShelf 三槽位（570×88，2026-08-18 老板二轮）**：ProbeHome（176×88 @88，探头 probeFootage.png 静态展示不可交互）、RulerHome（176×88 @282，尺子 尺子正面.png 静态展示不可交互）、RagHome（176×88 @476，紧邻 RulerHome 右侧，rag 可拖）；探头/尺子用 M2 同款置灰 (0.55,0.57,0.6,0.62)（有深色细节可见）；ModalLayer 必须放 SafeArea 下且运行时用 `FindDeep` 查找（`transform.Find("ModalLayer")` 只查直接子节点会失效）。
  - **浅色工具置灰坑（2026-08-18 老板反馈 rag 透明）**：rag 是浅灰白布，置灰 (0.55,0.57,0.6,0.62) 后几乎融入浅色背景（像透明）。浅色工具锁定色必须加深+高不透明 `(0.45,0.47,0.5,0.9)` 并加 Outline 深色描边（effectColor=(.2,.22,.25,.6) effectDistance=(2,-2)）与背景分离；深色工具（探头/尺子）用 M2 同款置灰即可。
  - 数字人/QA 复用 `M3DigitalHumanBootstrap`（已支持 M3/M4/M5 场景名）：M5Setup 建 QAPanel 壳（含 Placeholder）+ Blocker + DigitalHumanStage（含 FullBodyPreview）即可，Bootstrap 运行时装配全套。
  - 素材：rag.png（`Assets/probeFootage/rag.png`，Multiple sprite rag_0 internalID `1024226415114158248`，meta 已修 4096+Uncompressed）从 m4 分支并入；钢轨复用 `俯视角.png`/`俯视角透视.png`。
  - **2026-08-23 波形窗口保留 + 钢轨 Scene 权威（老板定稿）**：M5 保留 M2 波形窗口（SupportArea/WaveformArea_B 静态视觉）——`M2WaveformFx` 程序化绘制与 M2 同款（深底/蓝条红条/网格/绿色始波/噪声线，参数 150/115/110；早期 Setup 曾删该组件且不会自愈，Adapt 现检测 WaveGrid 缺组件自动补回）。老板手工调整钢轨位置（RailBackground anchoredPosition x=-236，左移避开波形窗口），此后 **M5Setup 对钢轨布局 Scene 权威**：railBg sizeDelta 非 0 时 Setup 不覆盖（仅新建/空布局设默认），视觉微调不写回 Setup 默认。
  - **2026-08-23 工具架用 MainScene/Tool（老板定稿，方案 B）**：老板手工添加 MainScene/Tool（M2 样式三槽位 ProbeHome/RulerHome/RagHome，含 bg/Chip/Outline）。M5Setup：`Tool` 存在时跳过 ToolShelf 创建；`EnsureToolCompat` 适配 Tool 树——Probe/Ruler 仅静态展示（M2ProbeDrag/M2RulerDrag 由 Adapt 移除，M5 不参与交互），RagHome 槽位工具节点（复制残留名为 Ruler）改名为 Rag + rag.png/置灰/Outline/M5RagDrag（擦拭功能）；EnsureAll 的 rag/ragHome 查找优先 Tool 树，无 Tool 回退标准 ToolShelf。Tool 树布局 Scene 权威不覆盖。
  - 验收：M5RuntimeSmoke 5 组断言（初态铺满/进度跟手+视图切换/拖出工作态/100% 完成+结束模块/Reset+QA 暂停）；M5Shot 三视口；M5Setup 幂等（连跑两次 SHA 一致）。

## 6. 目录与模块约定

- `Assets/Scripts/` — runtime 脚本（薄、通用、配置驱动）。
- `Assets/Editor/` — 搭建/生成工具（幂等）。
- `Assets/交互动画素材/` — 美术素材；`Assets/Settings/Scenes/` — 场景；`文档/` — 需求文档（技术规格书、功能文档、DeepSeek 接入方案）。
- M1 模块结构：QAPanel（问答抽屉）、ToolSelection（工具卡片）、PressDetector（按压检测）；后续模块复用其通用部分。
- 素材组织（2026-08-10 整理后）：探伤工具图 `Assets/InspectionToolMaterials/`（PNG）；探头图 `Assets/probeFootage/探头素材（有白边版）/` 与 `（无白边版）/`（M1-2 探头用**有白边版**）；音频 `Assets/Audio/E-xx 用途/`；数字人 `Assets/DigitalHuman/A-xx/`。旧目录 `01 探伤工具素材`、`02 探头素材` 已删除。素材迁移后必须同步 Setup 注入路径，否则 `LoadAssetAtPath` 返回 null 只打 Warning、场景空白但流程继续。

## 6.1 M1-2 阶段切换与防卡死模式（2026-08-10）

- M1-1/M1-2 由单一组件 `M1ToolSelection` 承载（同一"探测仪器选择模块"，不拆脚本）：
  - M1-1：6 个工具按 `ToolNames` 绑定，`correctToolName` 判定；选对显示"点击继续"。
  - `OnContinueClicked`：隐藏 `m1ItemsPath` 容器、显示 `m2ItemsPath` 容器、AI 回答切 `textM2Initial`、启动 M1-2 防卡死；`_phase2` 标志防重复进入。
  - M1-2：`probeNames` 列表按物体名绑定到 `m2ItemsPath` 容器，`correctProbeName` 判定；选对抖动+音效+锁定+显示"开始探测"。
- 防卡死（规格书硬性要求）：`toolIdleTimeout` / `probeIdleTimeout` 秒无操作 → `PulseHighlight` 金色脉动高亮正确项 → 自动选对并推进（M1-1 还自动进入 M1-2）；0=关闭。高亮与手动判定共用 `correctToolName/correctProbeName` 字段，保证行为一致。
- 阶段容器激活由运行时兜底（Awake 强制 M1-1 可见、M1-2 隐藏）+ Setup 幂等设置，两端一致不漂移。

## 7. 禁止事项

- 引入 Bolt / Visual Scripting 等可视化脚本包。
- 复制粘贴式扩展（同一逻辑复制到多模块）。
- 无理由新增专用脚本、硬编码数据、主动重构存量代码（除非任务涉及）。
- 未冻结场景的结构与 Setup 脚本逻辑漂移；或用 Setup/Agent 覆盖已冻结的 M2/M3 Scene。

## 7.1 Unity 6 已知坑（2026-08-07 M1 面板实战）

- **运行时 `Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd")` 会报错**：该内置 UI 切片图在 Unity 6 运行时加载不到（Editor 的 `AssetDatabase.GetBuiltinExtraResource` 正常）。运行时动态创建气泡等 UI 时改用程序化生成（`Sprite.Create` 自绘圆角 + border 九宫格）或项目内 Sprite 资产。
- **动态尺寸 UI 不要依赖嵌套布局组 preferred 缓存**：逐字/动态生长场景（气泡随文本增长）中，`HorizontalLayoutGroup` 的行高按子物体 preferred 推算且带缓存，尺寸变化时行高跟不上会重叠/错位。改用显式控制：行挂 `LayoutElement`（minHeight/preferredHeight 同值手动同步），气泡手动锚定定位，逐字更新后立即 `LayoutRebuilder.ForceRebuildLayoutImmediate`。
- **`TextAnchor` 枚举无 `Top/Bottom`**：Unity 命名体系为 `Upper/Middle/Lower`（如 `UpperLeft`、`UpperRight`）。
- **UI 场景音效必须强制 2D（`spatialBlend = 0`）**：AudioSource 挂在 UI 画板/普通场景物体上时，默认 3D 音效会随与 Main Camera 的距离衰减，画板远离相机则完全听不见。接入点播音效时在运行时获取 AudioSource 后立即设 `spatialBlend = 0f`（运行时兜底优于 Setup 创建时设置——Setup 只在新建时生效，用户手动挂的 AudioSource 覆盖不到）。
- **Vector2/Vector3 混合运算符重载歧义（Unity 6000）**：`(Vector2)vector3 - vector2` 在 Unity 实际编译下报 CS0034（Vector3/Vector2 混合运算符使重载解析歧义）；改用逐分量运算 `new Vector2(a.x - b.x, a.y - b.y)` 规避（2026-08-11 M2RulerDrag）。
- **Unity 6 伪 null 对象与 `??` 不兼容（2026-08-18 M5Setup 实战）**：`GetComponent<T>()`/`Find` 对缺失对象返回 **Unity 伪 null**（非 C# null，`== null` 为 true 但引用非空），`??` 运算符检查 C# 引用不触发，导致 `var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();` 得到伪 null 组件，访问属性抛 `MissingComponentException`。**必须用 `if (x == null)` 分步**：`var cg = go.GetComponent<CanvasGroup>(); if (cg == null) cg = go.AddComponent<CanvasGroup>();`。同理 `GetComponent<T>() ?? AddComponent<T>()` 全部禁止。
- **`TextAlignmentOptions` 无 `MiddleCenter`**：TMP 枚举为 `Center`/`MidlineLeft`/`MidlineRight` 等；`MiddleCenter` 编译报 CS0117（2026-08-18 M5Setup）。
- **EventSystem 必须用 `InputSystemUIInputModule`（2026-08-18 M5 实战）**：项目 `activeInputHandler: 1`（Input System Package 模式），旧版 `StandaloneInputModule` 每帧调 `Input.GetButtonDown` 抛 `InvalidOperationException: You are trying to read Input using the UnityEngine.Input class...`（Console 疯狂刷错 999+）。**新建 EventSystem 必须 `typeof(EventSystem) + typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule)`**（M2/M3/M4 同款，guid `01614664b831546d2ae94a42149d80ac`）；此坑不影响拖拽模拟（PointerEventData 直接构造 + OnBeginDrag/OnDrag 调用绕过 InputModule）。
- **离线编译 ≠ Unity 编译**：用外部 csc + `Managed/UnityEngine` impl dll 做离线编译可能漏报 Unity 实际编译错误（重载解析/引用集差异，实测 Vector2/Vector3 混合运算漏报）；编译验证一律以 Unity 编辑器/批处理为准（2026-08-11）。

## 8. 音效接入约定（M1 起）

- 素材放 `Assets/Audio/`（按 E-xx 用途分目录，附选择说明 txt）；素材由 **Editor Setup 注入**，运行时脚本只暴露 `AudioClip` 字段，禁止硬编码路径（运行时无法按 Assets 路径加载，除非走 Resources/Addressables）。
- Setup 注入采用「仅当字段为空时赋值」（`if (comp.clip == null) comp.clip = LoadClip(...)`），幂等且不覆盖用户手动替换的素材；`LoadClip` 失败打 `Debug.LogWarning` 返回 null 不中断 Setup。
- 播放统一用 `AudioSource.PlayOneShot(clip)`：互不打断、适合短音效；未配置素材或 AudioSource 缺失时**静默跳过不报错**（`if (clip == null || src == null) return;`）。
- 场景音频出口：AudioSource 由 Setup Ensure 到交互物体上（`GetComponent ?? AddComponent`），`playOnAwake` 默认 false，不产生开机噪音。

## 8.1 问答面板暂停契约（M1 起，M2/M3 复用）

- `M1QAPanel.pauseGameOnOpen`（默认 true）：面板 Open 时 `Time.timeScale = 0` 全局暂停（含模块计时/拖拽/动画），Close 时恢复**打开前**的值（`_timeScaleBefore` 记录，不硬编码 1，避免覆盖引导等场景的 timeScale 设置）。
- 暂停期间不受影响是设计前提：长按检测用 `Time.unscaledTime`、面板滑入/逐字用 `unscaledDeltaTime` / `WaitForSecondsRealtime`、DeepSeek 请求用 `UnityWebRequest`、数字人视频走 VideoPlayer（不受 timeScale 影响）。**新增问答链路组件必须遵循 unscaled 计时**，否则暂停时功能卡死。
- 引导期间（M1IntroVideo 全屏遮罩挡点击）QA 入口不可达，与引导的 timeScale 管理无并发冲突；若未来出现并发场景需先协调。

## 9. 与 AGENTS.md 的同步契约

- 本文档为权威来源；`AGENTS.md` 总纲只存放摘要（项目速览、五条规则、约定速查）。
- 修改本文档后必须同步更新总纲对应条目；总纲不新增本文档没有的规则。
