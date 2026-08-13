# M2 探头、字体与波形修复设计

## 设计目标

在不新增 runtime 脚本、不手改场景 YAML、不影响 M1 的前提下，让探头初始暂存、Step 2 放置与角度门控、Step 3 扫描、中文字体和实时波形形成一致的数据链路。

## 组件边界

- `M2Setup`：唯一场景结构来源；Ensure 唯一探头、`ProbeHome`/`RailViewport` 引用、中文 TMP 字体及默认参数。
- `M2ProbeDrag`：拥有探头当前显示父级/位置、放置判定、扫描轨迹约束和距离输出；不拥有流程阶段和波形 UI。
- `M2FlowController`：继续作为阶段与检出状态唯一所有者；接收放置、角度和距离事件，更新波形状态文字。
- `M2WaveformGraphic`：只消费距离并按配置区间绘制曲线，不判断检出成功。
- 不新增组件；`M2IdleHelp` 继续调用 Probe 的公开自动移动接口。

## 探头状态与坐标合同

### 初始状态

- 场景只有一个 `Probe`。
- `Probe` 初始父级为 `ProbeHome`，锚点居中，显示真实 K2.5 图片。
- Step 1 `unlocked=false`，不接受拖动。

### Step 2 放置

1. 耦合剂动画完成后 `Unlock()`。
2. 探头在未放置状态下可跟随指针移动，不受 10°门控。
3. 拖动坐标统一通过 `RectTransformUtility.ScreenPointToLocalPointInRectangle` 转换到目标容器局部空间。
4. 指针/探头进入 RailViewport 的起始放置容差区后，将探头重挂到 `RailViewport`，锚点吸附到 `scanStartLocal=(0.143,0.68)`，标记 `Placed=true`。
5. 未命中起始区释放时回到 `ProbeHome`，避免探头悬浮在任意位置。
6. `Placed && AngleCorrect` 时由 Flow 自动进入 Step 3；放置和调角先后顺序均可。

为正确处理释放判定，`M2ProbeDrag` 增加 `IEndDragHandler`，不新增脚本。

### Step 3 扫描

- 仅 `Placed && AngleCorrect` 时沿轨纵向移动。
- RailViewport 局部像素先转换为规范化锚点：`normalizedX = local.x / rect.width + pivot.x`。
- 用 `(normalizedX-scanStartLocal.x)/(scanEndLocal.x-scanStartLocal.x)` 得到扫描进度，再映射 `150→100mm`。
- `AutoMoveToMm` 与手动拖动最终都调用同一 `MoveToNormalized`/距离报告路径，避免视觉位置与读数漂移。
- Reset 将探头重挂回 `ProbeHome`，恢复中心位置和未解锁状态。

## 字体与清晰度

- `M2Setup` 使用 `GetComponentsInChildren<TextMeshProUGUI>(true)` 或等价 includeInactive 查询，覆盖初始隐藏的 Angle/Scan/Measure/Help/Completion 容器。
- 所有 M2 TMP 统一重指向 `sarasa-gothic-sc-regular_cn.asset`，不依赖对象当前 Active 状态。
- Setup 每次运行均自愈错误字体引用，且不创建新字体资产。
- 清晰度验收使用 1920x1080 Game View、1x 缩放作为基线；其他视口只检查响应式与溢出。

## 波形与状态合同

距离区间：

- `150mm ~ 125mm`：平直基线/低幅。
- `125mm ~ 112mm`：波峰平滑生长。
- `112mm ~ 108mm`：峰值区，110mm 达最大。
- `<108mm ~ 100mm`：实时波形下降。

实现原则：

- `growthStartMm`、`peakWindowMaxMm`、`peakTargetMm`、`peakWindowMinMm`、`scanEndMm` 参与幅度计算，不保留无效配置字段。
- Flow 在未检出时按当前距离更新“平直基线/波峰生长”；首次检出后标题锁定“峰值锁定”。
- 检出结果和蜂鸣保持一次性；波形仍持续消费后续距离以显示峰后下降。

## 兼容与迁移

- `M2Setup` 检测旧场景中位于 `RailViewport` 的 Probe，并迁移到 `ProbeHome`；不创建第二个 Probe。
- 序列化引用按真实对象重新注入，防迁移后引用指向旧对象。
- 运行时事件仍在 `Awake/Bind` 幂等绑定；Setup 中的普通委托不作为持久化保证。
- 不打开、不保存 M1 场景。

## 风险与回滚

- 重挂 UI 对象可能改变缩放/尺寸：迁移后显式恢复 RectTransform 尺寸、localScale、anchor 和 pivot。
- Pointer 坐标在 Overlay/Camera Canvas 下相机参数不同：沿用 `eventData.pressEventCamera`，并在三视口 Play Mode 验证。
- 若现有中文字体在 1x 下仍模糊，先保留本修复结果并单独评估材质/SDF 参数，不在本任务直接重做字体资产。
- 回滚只涉及 `M2Setup`、4 个既有 M2 runtime 文件中实际改动者和 M2 场景生成结果。
