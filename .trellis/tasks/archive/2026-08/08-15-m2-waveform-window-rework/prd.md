# M2 波形窗口改造（4:3 + 常驻始波 + 伤损波联动）

## Goal

将 M2 波形窗口（右下 `WaveformArea_B`）改造为参考真实探伤仪屏的最终形态：
- 窗口宽高比 4:3（当前 460×240 ≈1.9:1）；
- 波形内容 = 深色底 + **仅大刻度**主网格 + **常驻绿色始波**（发射脉冲尖峰 + 青绿竖线）+ **底部绿色锯齿噪声基线**；
- **伤损波**（与始波同形同色）随探头扫描联动：150mm 处以短波形态出现 → 逐渐左移并长高 → **115mm 长到最高** → 保持最高移动到 110mm 检出停止；
- 去掉：黄色曲线群、绿色水平线、小刻度（次网格）、橙红基线。
- **仅改 M2**，M3 波形（`M2WaveformGraphic` 旧样式）不受影响。

## Background

- M2 Scene 冻结节点（`Assets/Settings/Scenes/M2.unity`，不得修改/保存）：
  - `WaveformArea_B`（fileID 1515409903）：460×240，锚右下 (1,0)，anchoredPosition (-273,120)，pivot 0.5，包含 `bg`（深色底）、`WaveHeader`（顶栏 48px）、`WaveGrid`（杂项 UI）、`WaveGraphic`（stretch，y offset -24/-48）、`DetectionBanner`。
  - `WaveGraphic`（fileID 1455796311）：挂 `M2WaveformGraphic`（程序化绘制：深灰底 + 主网格 4 等分 + 次网格虚线 + 橙红平直基线 + 110mm 高斯尖峰；scanStartMm=150 / scanEndMm=100 / growthStartMm=125 / peakTargetMm=110 / waveColor 橙红）。
- `M2WaveformGraphic.cs`（130 行）同时被 **M2 与 M3** 复用（M3 由 `M3FinalCloseout` 在 `WaveLine` 节点挂载，配置 growthStartMm=140 / peakTargetMm=120），M3 需求未变，不得改动其行为。
- 玩法链路：`M2ProbeDrag.MoveToScan` → `currentDistanceMm = 探头到损伤欧氏距离/PixelsPerMm`（150→110 递减）→ `OnDistanceChanged` → `M2FlowController.NotifyDistance(mm)` → `waveform.SetDistanceMm(mm)`；`|mm-110|<=2` 且角度正确 → `NotifyDetected()` 检出锁定。
- 参考图（老板提供，`C:\Users\LF\AppData\Local\Temp\orca-paste-1786790465988-8ed679e2-cc93-46d2-9588-e10031a0e616.png`）：真实探伤仪波形窗口，黑底 + 点状网格 + 横轴 0~200mm（大刻度每 40mm）/ 纵轴 0~100（大刻度每 20）+ 左侧绿色发射脉冲尖峰（X≈0~15mm）+ 左侧青绿竖线（X≈0~2mm 贯穿）+ 底部绿色锯齿噪声线 + 黄色曲线群 + 绿色水平线（Y≈62.5）+ 橙色标注（老板手绘）。老板圈定：黄曲线群/绿水平线/小刻度/橙色标注不要；保留左侧尖峰、竖线、底部噪声线、深底网格、大刻度。
- 老板 2026-08-15 确认：仅 M2；窗口外形沿用、宽:高=4:3；伤损波与始波**同形同色（绿色）**；伤损波 X 轴位置按参考图刻度（0~200mm 左 0 右 200，150mm≈75% 处、110mm≈55% 处），从右向左移动 + 逐渐长高；**115mm 长到最高，保持最高移动到 110mm 停止**。
- 已确认：常驻绿色波形（始波+竖线+噪声线）作为固定内容，不随玩法变化；波形玩法联动本次一并实现（伤损波 150→115→110 动画）。

## Requirements

### R1. 窗口与对齐

- 波形窗口 `WaveformArea_B` 宽高比改为 **4:3**（宽:高 = 4:3）。
- 默认方案（老板未明确像素值，prd 审核时确认）：**宽 460、高 345**，anchoredPosition.y 由 120 调为 **172.5**（保窗口下缘贴屏幕底，避免超屏被裁）；备选方案：**宽 320、高 240**（位置不变）。
- 窗口内 `bg`/`WaveHeader`/`WaveGrid`/`WaveGraphic` 均为 stretch，尺寸变化自动适配，不写回 Scene。

### R2. 常驻内容（固定，不随玩法变化）

- **深色底**：接近黑的深色（参考图黑底观感），覆盖波形渲染区。
- **仅大刻度主网格**：横轴 0~200mm / 纵轴 0~100，大刻度 5 等分（横竖各 6 条线）；**去掉小刻度（次网格虚线）**。不加刻度文字（M2 保持无文字简洁风格；如需文字需另议，涉及运行时创建 TMP 节点）。
- **始波**：左侧**绿色发射脉冲尖峰**（陡升 + 振荡衰减，与参考图同形）+ **青绿竖线**（X≈2% 处贯穿全高）。常驻，不随玩法变化。
- **底部绿色锯齿噪声基线**：贴近底部（Y≈2~3%）的固定锯齿波动（固定种子，无闪烁）。

### R3. 伤损波联动（与始波同形同色）

- 伤损波形状与始波**完全相同**（复用同一脉冲绘制函数），颜色同为绿色。
- 位置：X 轴按 0~200mm 刻度映射（150mm→75% 宽、115mm→57.5%、110mm→55%），随探头距离**从右向左连续移动**。
- 高度曲线（由 `SetDistanceMm(mm)` 驱动）：
  - mm > 150：无伤损波（0）；
  - 150 ≥ mm ≥ 115：短波起步 → 线性/平滑长到最高（150mm 时短波可见，约 8% 峰高起步，可配置）；
  - 115 > mm ≥ 110：**保持最高**，继续左移；
  - mm ≤ 110：最高波形停在 110mm 刻度（检出锁定后不再变化）。
- 峰值高度参数化（Inspector 可调），默认最高约 78% 渲染区高（与现 M2 尖峰观感一致）。

### R4. Reset 与暂停

- `ResetAll()`：波形复位到初始态（150mm 短波），可重新扫描；检出锁定解除。
- 暂停合同：波形联动为纯状态驱动（无协程/动画），QA/Modal 暂停时探头拖动冻结、距离不变，波形天然冻结；恢复后继续。

### R5. 实现边界

- 不修改/保存 `M2.unity`、不新增素材文件。
- **仅改 M2**：`M3` 的 `M2WaveformGraphic` 旧样式与配置零改动（Scene、脚本行为均不变）。
- 低代码优先：新增 runtime 组件 `M2WaveformFx.cs`（程序化 UGUI Graphic，无素材），由 `M2FlowController.Awake` 运行时 `AddComponent` 挂到 `WaveGraphic` 节点并**禁用旧 `M2WaveformGraphic`**（不写 Scene；M3 的组件不受影响）。
- `M2WaveformFx.cs` ≤150 行；`M2FlowController.cs` ≤150 行（当前 145，净增 ≤5）。
- `M2RuntimeSmoke.cs`、`M2Shot.cs`（Editor 截图工具，需在无 Play 时触发新组件绘制）同步适配。

## Acceptance Criteria

- [ ] AC1：M2 波形窗口宽高比 4:3（运行时覆盖生效），窗口内布局（顶栏/波形区）正常无裁切，下缘贴屏幕底。
- [ ] AC2：波形渲染区 = 深色底 + 仅大刻度主网格（5 等分，无次网格小刻度），无刻度文字。
- [ ] AC3：常驻始波（绿色脉冲尖峰 + 青绿竖线）+ 底部绿色锯齿噪声基线正确显示，不随玩法变化。
- [ ] AC4：伤损波与始波同形同色；150mm 时短波出现（X≈75% 处）→ 随扫描逐渐左移长高 → **115mm 最高（X≈57.5%）** → 保持最高移动到 **110mm（X≈55%）检出停止**；检出后波形锁定不再变化。
- [ ] AC5：无黄色曲线群、无绿色水平线、无橙红基线。
- [ ] AC6：Reset 后波形复位（150mm 短波），可重新扫描；检出锁定解除。
- [ ] AC7：QA/Modal 暂停期间波形不推进（探头冻结），恢复后继续。
- [ ] AC8：Unity 编译无 Error；`M2WaveformFx.cs` ≤150 行、`M2FlowController.cs` ≤150 行。
- [ ] AC9：`M2.unity` SHA-256 实施前后不变；无新增素材文件；M2 RuntimeSmoke 通过；M3 波形（`M2WaveformGraphic` 旧样式）与 M3 RuntimeSmoke 不受影响。

## Out Of Scope

- M3 波形样式改造（M3 保持旧样式；如需统一另立任务）。
- 刻度文字（横轴 0~200mm / 纵轴 0~100 的数字标签），如需另议。
- 黄色曲线群/绿色水平线等参考图元素的新增（本次是去掉）。
- 其他 M2 流程（耦合剂/定位/测量/完成）与素材零改动。

## 2026-08-15 二轮修正（老板反馈 + 授权）

老板反馈：Play 下波形窗空白（绿色始波/网格/刻度全无）；要求「原来的波形图不要了」「Scene 界面也要同步更新」「用参考图的（点状+网格）」；授权直接改 `M2.unity`（波形窗口区域解冻）。

### 根因
1. `M2WaveformFx` 缺 `[RequireComponent(typeof(CanvasRenderer))]` → Play 下运行时 AddComponent 不自动加渲染器 → 不渲染。
2. 刻度文字未做（此前默认「不加文字」理解错，老板要刻度）。
3. 波形为运行时挂载，非 Play 的 Scene/Game 视图显示 Scene 序列化旧内容（旧组件 + WaveHeader 提示词）。

### 新方案（Scene 直做，替代原运行时挂载）
- `M2.unity` 直接改造（老板授权）：`WaveformArea_B` 4:3（460×345、y=172.5）；删 `WaveHeader`（bg/平直基线/目标110mm/150mm 提示词）与 `WaveGraphic`（旧 M2WaveformGraphic）整节点；`WaveGrid` 全 stretch 并挂 `M2WaveformFx`（序列化）；新增刻度文字（横轴 0/40/80/120/160/200mm + 纵轴 0/20/40/60/80/100，12 个 TMP）。
- `M2WaveformFx.cs`：加 `[RequireComponent(typeof(CanvasRenderer))]`；网格改**点状"+"**（参考图风格，5 等分交叉点画"+"，无连续线）。
- `M2FlowController.cs`：`waveformFx` 改 Scene 序列化引用；删运行时挂载/禁用旧/窗口覆盖/LogError；`waveform`（M2WaveformGraphic）字段删除。
- `M2Shot.cs`/`M2RuntimeSmoke.cs`：Scene 直做后简化（无需 AddComponent/补渲染器/禁用旧组件）。
- 改完 M2 波形窗口区域即重新冻结；spec/AGENTS 更新冻结约定。
