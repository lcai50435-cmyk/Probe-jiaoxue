# M2 迁移至 M3 UGUI 基线技术设计

## 修改边界

本任务修改：

- `Assets/Editor/M2Setup.cs`：M2 的 M3 同构布局、视觉令牌和幂等自愈。
- `Assets/Scripts/M2FlowController.cs`：仅同步普通/透视分段控件的 M3 颜色。
- `Assets/Settings/Scenes/M2.unity`：由 Setup 重新生成。
- `.trellis/spec/unity/ugui-module-template.md`：M3 权威复用合同。
- 任务、索引和 AGENTS 文档。

不修改 M2 状态机、拖拽、尺子、IdleHelp、波形数据算法、数字人/QA 逻辑，也不修改 M3Setup 或 M3 Scene。

## 权威来源

视觉值直接来自 `Assets/Editor/M3Setup.cs`：

| 令牌 | Unity Color | 用途 |
|---|---|---|
| Page | `(0.925, 0.935, 0.945)` | 页面浅灰 |
| Surface | `(0.975, 0.980, 0.985)` | Header、教学面、Dock |
| Ink | `(0.120, 0.150, 0.180)` | 主文字 |
| Muted | `(0.380, 0.420, 0.460)` | 次文字 |
| Primary | `(0.080, 0.420, 0.660)` | 标题、主操作、选中 |
| Accent | `(0.930, 0.550, 0.120)` | 角度与教学强调 |
| Screen | `(0.090, 0.110, 0.120)` | 波形底色 |
| ScreenGrid | `(0.420, 0.550, 0.530, 0.220)` | 波形网格 |
| Wave | `(0.340, 0.920, 0.620)` | 动态波形 |

## 布局迁移

1920 基准保持 24px 页面边距、16px 间距、80px Header、176px Dock、576px SupportArea。主要变化：

- ToolShelf：从全宽 96px 条带改为 RailArea 左上约 372x88 局部暂存区。
- RailViewport：扩展为完整 RailArea 白色教学面，M2 俯视钢轨仍为 891x220，运行时坐标不变。
- PerspectiveBar_C：由 320x64 改为 364x64，两个 182x64 分段。
- DigitalHumanStage / WaveformArea_B：继续使用 M3 的右侧上下组合，右边缘一致。
- ControlDock_D：由深石墨改为浅色 Surface，维持左 30%提示、中 48%操作、右 22%步骤。

## 运行时兼容

`M2FlowController.ApplyView` 原本硬编码旧蓝/灰并同步文字色。仅把这两个颜色替换为 M3 Primary/Neutral，保持方法、事件和状态不变。

`M2WaveformGraphic` 不修改算法。Setup 每次将实例 `waveColor` 自愈为 M3 Wave，确保已有 Scene 不继续使用旧默认绿色。

数字人保持 RawImage + VideoPlayer + Presenter + RenderTexture + LumaKey。静态截图可临时隐藏未播放 RawImage，但必须恢复且不保存。

## 幂等与验证

1. 记录 M1/M2/M3/Build Settings 哈希。
2. 用 Unity 6000.3.21f1 编译并执行 `M2Setup.SetupM2Batch`。
3. 连续执行两次，比较 M2 Scene SHA-256。
4. 执行 `M2Shot.CaptureAll`，审查三视口。
5. 运行静态路径/组件检查和 `git diff --check`。
6. Play Mode 数字人、QA、四阶段流程保留为父级 M2 功能门槛。

## 回滚

视觉问题只修正 M2Setup 并重新生成 M2，不手改 Scene YAML，不反向修改 M3 基线。功能回归则回滚本任务在 M2FlowController 的颜色行并检查 Setup 引用，不触碰工作区其他改动。
