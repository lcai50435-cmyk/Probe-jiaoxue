# PRD：M2/M3/M4 检出伤损变色绑定透视视图

## 背景

老板 2026-08-23 反馈：探测模块在探测到损伤（检出）时，**还没打开透视**就出现伤损变色（红椭圆变橙色标记）。期望行为：**打开透视才能看见伤损变色；未打开透视时什么都看不到，只能听到报警声**。

当前三个模块（M2/M3/M4）代码同构，均在 `NotifyDetected()` 中无条件调用 `ShowDamageMarker()`，且 `ApplyView()` 只在 `!Detected` 时隐藏标记，导致伤损标记显示与透视开关无关。

## 需求

1. 检出（探测到损伤）时仅蜂鸣报警，不显示橙色伤损标记。
2. 橙色伤损标记仅在透视视图下显示：`PerspectiveOn && Detected`。
3. 切换视图时按规则即时显示/隐藏：透视开且已检出 → 显示；普通视图或关闭透视 → 隐藏。
4. Reset / 未检出时保持隐藏（现有逻辑保留）。
5. 射线颜色不受影响（保持绿色）。

## 涉及文件

- `Assets/Scripts/M2FlowController.cs`（M2 标记为运行时创建，无 Scene 引用）
- `Assets/Scripts/M3FlowController.cs`（M3 标记为 Scene 节点 `damageMarker`）
- `Assets/Scripts/M4FlowController.cs`（同 M3 结构）
- `.trellis/spec/unity/module-flow-contract.md`（跨模块验收合同同步更新）

## 验收标准

- M2/M3/M4 普通视图下检出：无任何伤损变色，只有蜂鸣报警。
- 检出后切换透视视图：橙色伤损标记出现。
- 检出后切回普通视图：橙色标记隐藏；再切透视：再次显示。
- 未检出时切换视图：始终无橙色标记。
- Reset 后一切恢复。
- 场景文件（M2/M3/M4.unity）不做任何修改（纯 runtime 逻辑变更）。

## 约束

- 低代码优先：仅改既有 FlowController，不新增脚本。
- 不改冻结 Scene（M2/M3 冻结；M4 不涉及 Scene 修改）。
- 改动保持三模块同构。
