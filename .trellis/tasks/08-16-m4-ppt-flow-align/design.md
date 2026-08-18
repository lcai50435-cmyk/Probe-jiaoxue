# Design：M4 轨腰探测 = M3 复制基线 + 参数替换

## 1. 总策略

老板定稿：**M4 以 M3 为基线整体复制**（Scene、runtime、Editor 工具三套全复制改名），随后只改四类差异：波形参数、探头几何/角度、尺子几何、射线几何。这与旧"参数化复用 M2"方案不同——M3 已验证行为（伤损变橙、无耦合剂、检出即测距、防卡死、QA 暂停）全部免费继承，风险最低。

```
M3.unity ──复制──> M4.unity（改模块标题/脚本引用 guid/波形/几何参数）
M3*.cs  ──复制──> M4*.cs（namespace M3→M4，改波形/几何参数）
M3 Editor──复制──> M4 Editor（Setup/RuntimeSmoke/Shot/FinalCloseout）
```

## 2. Scene 复制（M4.unity）

1. `cp Assets/Settings/Scenes/M3.unity Assets/Settings/Scenes/M4.unity` + 生成 .meta（复制 M3.unity.meta 改名，guid 由 Unity 重新导入生成——手工改 YAML 时用新的 meta guid）。
2. YAML 内所有 `M3FlowController` 脚本引用 guid（`059f97c6...`）替换为 `M4FlowController.cs` 的 guid；`M3ProbeDrag`/`M3RulerDrag`/`M3IdleHelp`/`M3DigitalHumanVideo` 同理。
3. 模块标题文本 "M3 轨头侧面探测" → "M4 轨腰部位探测"（ModuleTitle、completionText 等）。
4. 波形区：
   - `WaveformArea_B` 4:3（460×345、anchoredPosition.y=172.5）不变（M3 已是 M2 风格）。
   - `WaveGrid` 上 `M2WaveformFx` 序列化参数改为 `appearMm=55/peakMm=45/stopMm=40`（与 M3 的 160/123/120 不同）。M2WaveformFx 脚本本身零改动（M3 已用）。
   - `ScaleTexts` 横轴 0/40/80/120/160/200mm、纵轴 0/20/40/60/80/100 不变（M4 PPT 波形图确认横轴 0~200、纵轴 0~100，与 M2/M3 一致）。
5. 钢轨/探头/尺子 Sprite 引用不动（M3 已用 railwayTracks_2 / probeFootage / 尺子正面，与 M4 要求一致）。
6. 探头/尺子/RulerHome/ProbeHome 的 Scene 初态锚点/位置按 M4 几何标定结果微调（老板 PPT：轨腰左侧接近轨腰最上端、无偏角）。

## 3. Runtime 脚本复制（M4*.cs）

复制清单（均 namespace M4、类名 M4Xxx，逻辑零改动，仅参数/文案）：

| 源 | 复制为 | 改动 |
|---|---|---|
| `M3FlowController.cs` | `M4FlowController.cs` | `targetAngle=10`、`targetDistance=40`；`waveformFx.appearMm=55/peakMm=45/stopMm=40`；`NotifyDistance` 波形映射 `Lerp(160,120)→Lerp(55,40)`（波形窗口 55→40mm）；DefaultHints/StageNames 文案改 M4（轨腰）；completionText "轨腰部位探测完成" |
| `M3ProbeDrag.cs` | `M4ProbeDrag.cs` | `scanStartMm=55`、`scanEndMm=40`；`scanStartY`=轨腰上端标定值；`visualTiltAtTarget=10`；**向上偏转**（角度视觉方向反转）；`beamLengthZeroMm` 按射线合同；damageUv 沿用（同一红椭圆伤损） |
| `M3RulerDrag.cs` | `M4RulerDrag.cs` | `ruler120Uv → ruler40Uv`（40mm 刻度标定，UV≈(0.284,0.038) 待像素验证）；`PixelsPerMm = dist(zero,ruler40)/40`；`measureAngleDeg=0`（水平）；`positioningAngle=0`（水平放置） |
| `M3IdleHelp.cs` | `M4IdleHelp.cs` | 自动演示改 55→40mm、10° |
| `M3DigitalHumanVideo.cs` | `M4DigitalHumanVideo.cs` | 仅改名 |

### 3.1 探头向上 10° 的视觉方向

M3 是向下 13°（`probeVisual.localRotation = probeBaseAngleDeg - tilt`、`beamLine = -degrees`）。M4 向上 10°，符号取反：

```
tilt = degrees / targetAngle * visualTiltAtTarget        // visualTiltAtTarget=10
probeVisual.localRotation = probeBaseAngleDeg + tilt      // 向上
beamLine.localRotation   = degrees                        // 向上（相对探头 90°，方向反转）
```

`probeBaseAngleDeg`（探头图片基准角）与 `beamBaseAngleDeg` 独立可调，与 M3 合同一致。

### 3.2 射线几何（不能穿透到轨头）

M4 PPT：光线只能打到轨腰最顶部，不能穿透到轨头。目标线 = 红椭圆（伤损）**下边缘**（伤损在轨腰最上端，y 约在正视角透明.png 的 142~183px，质心 (0.4711, 0.2194)）。射线长度 `min(默认, (entryY - 椭圆下边缘Y)/sin(角度))`，与 M3 合同同构（M3 目标线=红椭圆下边缘 194/740），仅方向相反（向上），`drop = entryY - redBottomY` 取负/反号。检出 = 射线末端实际到达/越过伤损（复用 M3 的 `BeamHitsDamage` 逻辑，方向适配向上）。

### 3.3 尺子 40mm 标定

- `尺子正面.png`（1205×213）底边基线：0mm 左端底尖 UV≈(0.005,0.038)（沿用 M3/M2）；40mm 刻度线像素采样确认（初步识别 x≈342px → UV≈0.284，实现时像素验证）。
- `PixelsPerMm = distance(zero, ruler40) / 40`（M3 是 /120；M4 的 40mm 跨度更短，ppm 会更大——几何按此标定）。
- 测量 0/40 双点：0 对齐探头入射点、40 对齐伤损。

## 4. Editor 工具复制

| 源 | 复制为 | 说明 |
|---|---|---|
| `M3Setup.cs` | `M4Setup.cs` | M4 未冻结：保留只读打开器形态（M3 已冻结，M4 基线复制后无需 Setup 生成）；或最小化 Ensure。以 M3Setup 只读形态为准，避免漂移。 |
| `M3RuntimeSmoke.cs` | `M4RuntimeSmoke.cs` | 断言改 55→40、10° 向上、波形 55/45/40、尺子 40mm、射线橙色、伤损橙色 |
| `M3Shot.cs` | `M4Shot.cs` | 三视口截图（1920x1080 / 1280x720 / 2436x1125），哈希记录 |
| `M3FinalCloseout.cs` | `M4FinalCloseout.cs` | 若 M3 有 closeout 验收则复制改名 |

## 5. 复用与边界

- `M2WaveformFx` 脚本零改动（M3/M4 共用）；M2ProbeDrag 的 `GetBeamSprite`/`GetEllipseSprite` 公共静态方法复用。
- M4 不新增独立波形/射线/尺子脚本——全部继承 M3 复制件。
- M3.unity / M3*.cs / M3 Editor 三套文件**只读不写**；实施前后校验 M3.unity SHA-256。
- M4 Scene 未冻结，允许运行时 Bind 覆盖 Scene 旧值（沿用 M3 模式）。

## 6. 风险

- 向上偏转是方向反转（M3 向下），视觉验证需老板目视确认探头贴轨腰。
- 尺子 40mm 刻度 UV 标定若不准，ppm 错误会导致扫描起点/命中点偏差——像素验证 + 烟测断言。
- M4 扫描起点"轨腰左侧不超出轨道"需几何标定 scanStartY / scanStartLocal，可能与 M3 的 damageUv 换算冲突，以老板目视为准微调。
- 波形 55→40mm 在 0~200mm 窗口内位于左端 20%~27.5% 区间（始波附近），视觉上与 M3（160~120 在右侧 60%~80%）不同——符合 PPT（伤损波靠近始波）。
