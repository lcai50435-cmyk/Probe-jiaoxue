# M3 UGUI 基线与 M2 迁移摘要

## 最新结论

2026-08-12 最新产品决定覆盖此前“M2 是统一视觉模板”的结论。M3 的布局和画风成为唯一权威来源：

- `Assets/Editor/M3Setup.cs`
- `Assets/Settings/Scenes/M3.unity`
- `Logs/m3-shot_1920x1080.png`
- `Logs/m3-shot_1280x720.png`
- `Logs/m3-shot_2436x1125.png`

M2 迁移到同一视觉语言，但继续保留自己的四阶段流程、俯视钢轨、10°、150→100mm、110mm、拖拽坐标、动态波形和视频数字人链路。M4、M5 默认直接采用 M3 基线。

## 稳定骨架

```text
Canvas (1920x1080 / Match 0.5)
└── SafeArea
    ├── Background
    ├── HeaderBar
    ├── MainScene
    │   ├── RailArea
    │   │   ├── ToolShelf          # 左上局部工具架
    │   │   ├── RailViewport       # 白色主教学面
    │   │   └── PerspectiveBar_C   # 左下 364x64
    │   └── SupportArea
    │       └── WaveformArea_B     # 右下 460x240
    ├── ControlDock_D              # 176px 浅色操作带
    ├── QALayer
    ├── DigitalHumanStage          # 右上全身人物
    └── ModalLayer
```

层级固定 `QALayer < DigitalHumanStage < ModalLayer`。数字人和波形在 SupportArea 内上下组合、右边缘一致。

## M3 令牌

| 令牌 | Unity Color | 用途 |
|---|---|---|
| Page | `(0.925, 0.935, 0.945)` | 页面浅灰 |
| Surface | `(0.975, 0.980, 0.985)` | Header、教学面、Dock |
| Ink | `(0.120, 0.150, 0.180)` | 主文字 |
| Muted | `(0.380, 0.420, 0.460)` | 次文字 |
| Primary | `(0.080, 0.420, 0.660)` | 标题、主操作、选中 |
| Accent | `(0.930, 0.550, 0.120)` | 角度、定位强调 |
| Screen | `(0.090, 0.110, 0.120)` | 波形底色 |
| ScreenGrid | `(0.420, 0.550, 0.530, 0.220)` | 波形网格 |
| Wave | `(0.340, 0.920, 0.620)` | 波形 |

旧 M2 的深色 Dock、全宽工具架、320x64 分段和 `#266AD1` 主色不再是统一标准。

## 复用边界

复用布局、视觉令牌、Setup 合同、数字人/QA 公共链路和验收方式。每个模块必须替换标题、步骤、角度、扫描范围、目标距离、素材、波形 profile、交互坐标和完成出口。

禁止复制其他模块的状态机、Scene YAML、坐标、阈值、DeepSeek/QAPanel/Presenter/视频逻辑。

## M2 迁移状态

代码已更新：

- `Assets/Editor/M2Setup.cs`：M3 令牌、局部工具架、364x64 分段、浅色 Dock、M3 波形外观。
- `Assets/Scripts/M2FlowController.cs`：仅同步分段选中/未选颜色。
- `.trellis/spec/unity/ugui-module-template.md`：重写为 M3 验收基线。
- `.trellis/spec/unity/index.md`、`AGENTS.md`：同步新权威结论。

Unity 6000.3.21f1 已在临时评审副本完成编译、Setup 双跑和三视口审图；最终 M2 Scene 双跑 SHA-256 均为 `e76efeddec3c8e9bec77b3fcbccd865de9a645f3798ccd6bb126268f15caf735`，并已同步回主工作区。三视口无重叠、裁切或文字溢出；局部工具架遮挡已通过 sibling 顺序自愈修复，顶层业务顺序和已批准的视频人物 `x=-75 / scale=0.78` 也已写入 Setup 权威值。

临时副本退出时仍报告既有 TMP Font Asset `m_AtlasTextures` 未赋值异常；Unity 返回码为 0，C# 编译、场景保存、截图与幂等哈希均成功。Play Mode 数字人/QA 与完整四阶段仍属于父级 M2 功能任务；质量检查已确认当前 QA 接线和重置 Modal 暂停 IdleHelp 尚未完成，本任务不将其误报为“已保留通过”。
