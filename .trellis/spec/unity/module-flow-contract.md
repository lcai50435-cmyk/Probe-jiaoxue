# 探测模块流程合同（M2 定稿，M3/M4 复用）

> 本文沉淀 M2 轨头顶面流程重构的最终口径与多次返工的教训。M3（轨头侧面）、M4（轨腰）做流程重构时，先读本文，避免重走 M2 的返工。
> 相关规范：`low-code.md`（低代码/冻结 Scene）、`ugui-module-template.md`（UGUI 基线）、`video-intro.md`（引导视频）。

---

## 1. 流程骨架

四阶段（页面骨架不变，仅定位阶段内部有门控链）：

```
Couplant(涂耦合剂) → Positioning(0°放置 + 定位尺校10° + 归槽) → Scanning(前移至110mm检出) → Measuring(尺子0/110双点复测) → Completed
```

老板 PPTX 的三步口径：① 钢轨左侧中心线 0° 放置 → ② 定位尺向内偏 10° → ③ 前移至入射点距目标 110mm。

- 校角是**夹具式顺序链**（详见 `low-code.md` §5.4）：放探头 → 尺子吸附成夹具（只验槽位+平行，不验角度、吸附保留现场）→ 解锁 Slider → 调 10° 稳定 0.5s → 正确音效+锁角度 → 手动撤尺 → 进扫描。
- 角度锁与拖拽锁分离：`SetAngleLocked`（只锁 Slider）vs `SetInputLocked`（只锁拖拽）。

## 2. 目标点：红色损伤（不是焊缝）

- 目标 = 透视钢轨图里的**红色损伤椭圆**，不是 `WeldLine` 节点。M2 实测红椭圆中心 UV `damageUv = (0.4808, 0.711)`（对 `俯视角透视.png` 2469×609 做像素采样），经 `RailPerspective` Rect 换算 RailViewport 本地坐标。
- 教训：早期按 PPTX 字面「焊缝熔合线」把目标改成了 `WeldLine`(0,0)，老板截图反馈「射线要瞄准红色损伤中间」才改回。**做之前先跟老板确认目标到底是「损伤」还是「焊缝/熔合线」**。

## 3. 素材替换合同（可复用）

冻结 Scene 不能改 sprite 引用，两条路都要走：

1. **运行时换图**：素材复制到 `Assets/Resources/`，`Bind`/`Awake` 里 `Resources.LoadAll<Sprite>(名)[0]` 换 `Image.sprite`。Single 模式 fileID=21300000，Multiple 模式用 meta 里的 internalID。
2. **Scene 序列化同步**（老板要求 Scene 视图与 Game 视图一致时）：直接改 M2.unity 的 `m_Sprite: {fileID, guid, type}` 指向新素材。改完去掉 UTF8 BOM（Set-Content 会引入，破坏首行 `%YAML`）。

素材清单（M2）：探头 `probeFootage.png`、尺子 `尺子正面.png`、钢轨普通 `俯视角.png`/透视 `俯视角透视.png`（railwayTracks_2 v2）、射线参照 `greenLight.png` 程序化生成。

**模糊排查**：大图超过 `maxTextureSize 2048` 会被降采样 + DXT 压缩导致模糊 → 新素材 meta 改 `maxTextureSize 4096` + `textureCompression 0`（无压缩）。

## 4. 探头合同（位置/角度）

- **起始位置** `startLocal`（配置化，默认 `(-500,0)` = 钢轨左侧中心线）。它是**入射点**的起点；探头本体中心在 `ScanStart = startLocal - EntryLocal`（探头悬在轨面上方、底面贴轨面）。
- **入射点** `probeEntryLocal`：射线出发点，在探头贴图渲染区的归一化坐标。**必须对准探头真实发射面，不是贴图底边**（贴图底部有留白时会「偏下」）。需老板目视微调。
- **探头基准角** `probeBaseAngleDeg`：探头图片的固有偏角（老板称「初始图像角度偏高」），用来把探头图片摆正、贴合轨面。

## 5. 射线合同（位置/角度/长度）—— 返工最密集处

共享滑条转角 `TiltAngle = (角度/targetAngle) × visualTiltAtTarget`，探头和射线各自带独立基准角：

```
probeVisual.localRotation = probeBaseAngleDeg + TiltAngle
beamLine.localRotation   = beamBaseAngleDeg  + TiltAngle
```

- **射线起点 = 入射点**（`ProbeEntryWorld`），pivot 在底面、向上延伸。
- **射线与探头相对夹角恒定**：两者共享 `TiltAngle`，相对角 = `beamBaseAngleDeg - probeBaseAngleDeg` 不变，射线随探头同步旋转。
- **「90°垂直」的基准是发射面**（老板图2 红色框框 = 连接器与主体交界处），不是图片的侧面/底部。`beamBaseAngleDeg` 就是把射线转到垂直于发射面的那个偏角。
- **射线从放置那一刻就出现**：`PlaceAtStart()` 里调 `ShowBeam()`，不是等到扫描阶段。
- **射线长度随角度插值**：`长度 = Lerp(beamLengthZeroMm, hitMm, 角度/10°) × ppm`。0° 时最长（够到目标/右缘），转到 10° 平滑缩短到 110mm。

### 返工根因（务必先对齐）

1. **「固定方向」 vs 「跟随角度」**：早期把射线做成「瞄准损伤」的固定方向，老板两次纠正「射线要跟着探头一起转、和探头底面保持 90°、不能固定方向」。
2. **「垂直」的基准面**：一开始以为垂直图片底面/侧面，老板纠正「是垂直图2 红色框框那个发射面」。**做射线前先让老板圈出发射面位置**。
3. **探头和射线的角度要能分开调**：探头 `probeBaseAngleDeg`、射线 `beamBaseAngleDeg` 各自独立，只共享滑条转角。

## 6. 探头视觉区分（阴影+描边）

探头（白）和钢轨（浅灰）颜色接近，用内置组件做视觉分离（运行时挂在探头 `bg` Image 上，不改 Scene）：

```csharp
var sh = img.GetComponent<Shadow>() ?? img.gameObject.AddComponent<Shadow>();
sh.effectColor = new Color(0,0,0,.48f); sh.effectDistance = new Vector2(7,-7);   // 阴影
var ol = img.GetComponent<Outline>() ?? img.gameObject.AddComponent<Outline>();
ol.effectColor = new Color(.1f,.12f,.15f,.6f); ol.effectDistance = new Vector2(2,-2); // 描边
```

- 返工过程：只加阴影「不明显」→ 阴影 55%+描边 85%「太过了」→ 降到阴影 48%+描边 60%「合适」。**阴影/描边的 alpha 和偏移做成可调，先给中等值再让老板微调**。

## 7. 最关键教训：场景序列化字段覆盖代码默认值

**改了代码里的默认值，对「已经被场景序列化过的字段」不生效**——Unity 反序列化时用场景里存的值覆盖代码默认值。

- M2 实锤：`beamLengthZeroMm` 代码默认从 237 改到 500/550，但场景里已序列化 `beamLengthZeroMm: 237`，老板看到的一直是 237mm，我改代码白改。
- 正确做法：**新增字段先让老板在 Inspector 里改（会写进场景），或改完代码默认值后同步改场景 YAML**；不要只改代码默认值就以为生效了。

## 8. 可复用实现清单（M3/M4 直接用）

| 能力 | 位置 | 复用点 |
|---|---|---|
| 运行时素材换图 | `Resources.LoadAll<Sprite>` + `Bind` | 探头/钢轨/尺子换素材 |
| 程序化射线/阴影贴图 | `GetBeamSprite()`（绿色/检出橙色） / 柔边椭圆 | 射线、阴影、光效 |
| 角度模型 | `probeBaseAngleDeg`/`beamBaseAngleDeg` + `TiltAngle` | 探头+射线同步旋转 |
| 射线长度插值 | `Lerp(beamLengthZeroMm, hitMm, 角度/10°)` | 随角度变长的射线 |
| 阴影+描边 | `Shadow`+`Outline` 组件 | 设备与背景区分 |
| 几何合同 | `startLocal`/`probeEntryLocal`/`damageUv`/`PixelsPerMm` | 起点/入射点/目标点 |
| 像素级标定 | 对素材 PNG 采样红像素中心 | 标定目标 UV |
| 冻结 Scene 双写 | runtime LoadAll + 序列化 guid 改 | Scene/Game 视图一致 |

---

## 9. M3 轨头侧面流程合同（2026-08-16）

- 流程：Positioning(13°定位，直接进入，无 Intro/耦合剂) → Scanning(160→120mm 检出锁定) → Measuring(尺子 0/120 双点) → Completed。
- 目标点：**伤损**（不是焊缝线）。
- 扫描：`scanStartMm=160`、`scanEndMm=120`；到达 120mm 检出后探头锁定，不再继续向 100mm。
- 波形：复用 `M2WaveformFx`，`appearMm=160`、`peakMm=123`、`stopMm=120`；初态 160mm 短波，123mm 最高，120mm 锁定。
- 测量：0 刻度对齐探头入射点，120mm 刻度对齐伤损，完成测量。
- 检出即测距（2026-08-16 老板追加）：射线照到伤损检出瞬间探头锁定，**直接** `rulerDrag.Show() + Go(Measuring)`，玩家可直接拖尺测量——**无"下一步"按钮门控**（M3 曾用运行时创建的 NextButton 门控，已删除）。
- 检出无视觉标记（2026-08-16 老板追加）：**不显示**橙色损伤方块（DamageMarker 永久 `SetActive(false)`）与"伤损检出"横幅（DetectionBanner 不激活）；检出反馈仅剩报警蜂鸣 + 射线绿→橙。
- 完成出口（2026-08-18 老板追加）：M3 完成后点击"下一模块"按钮 → `M3FlowController.nextSceneName`（代码默认 `"M4"`，M3 冻结 Scene 未序列化该字段）`SceneManager.LoadScene` 进入 M4；完成文案条件同步 M2（nextSceneName 非空即显示"轨头侧面探测完成"）。**2026-08-18 二轮：`EnterNextModule` 删除 `rulerDrag?.ResetTool()`，点击直接切场景，尺子不再先归位（M2/M4 同款）**。M3 脚本本次变更属老板明确授权。
- 射线：正常绿色，检出后橙色（复用 `M2ProbeDrag.GetBeamSprite`）。
- Scene：波形窗口按 M2 风格同步；尺子使用 `尺子正面.png`；探头起始按 PPT 左侧轨头侧面。

---

## 10. M2 检出即测距合同（2026-08-16）

老板在 M3 验收后要求 M2 同步：**检出即测距，无"下一步"门控**。`NotifyDetected()` 检出瞬间探头锁定 + 报警蜂鸣后**直接** `rulerDrag.ShowMeasure() + Go(Measuring)`；`nextButton` 不再激活，`NextToMeasure()` 已删除（`M2FlowController` 不绑定该按钮）。烟测断言改为：检出后 nextButton 未激活、阶段直接为 Measuring 且尺子解锁。

**返工教训（2026-08-16）**：尺子工作态 `localScale` 必须保持 `Vector3.one`，禁止为适配 Scene 根缩放（0.8）而折算 `PixelsPerMm`——`PixelsPerMm` 是 M2ProbeDrag/M3ProbeDrag 扫描起点/命中点几何（`damage - mm*ppm`）的唯一依据，ppm 变化会改变探头初始放置位置（老板硬性要求不变）。Scene Ruler 根 `localScale` 只影响工具架显示。另：`M2RulerDrag.Awake` 强制 `measureAngleDeg=0`、`measureOffset=zero`（PPT 水平放置合同，Scene 旧值 9.55/(19,28) 不写回）。
