# Scene 静态文本字号审计（自动生成，只读分析）

- 行高系数按 1.2em、CJK=1.0em、ASCII≈0.5~0.62em 保守估算；建议值留 ≥2px 余量。
- 文本组件总数：149


## M1

| 层级路径 | 字号 | 容器WxH | 换行 | 溢出模式 | 文本(截断) | 估算可放大至 |
|---|---|---|---|---|---|---|
| `画板/标题栏/标题` | 50 | 1002.5x93.9 | 是 | 0 | 请选择钢轨探伤工具 | 75 |
| `画板/ChatArea/QAPanel/Header/CloseButton/Text` | 36 | 64.0x64.0 | 是 | 0 | × | 46 |
| `画板/ChatArea/QAPanel/InputRow/CounterText` | 22 | 120.0x24.0 | 是 | 0 | 0/200 | 当前已溢出! |
| `画板/ChatArea/QAPanel/InputRow/VoiceButton/Text` | 30 | 76.0x78.0 | 是 | 0 | 语音 | 38 |
| `画板/ChatArea/QAPanel/InputRow/InputField/Text Area/Text` | 30 | 336.0x66.0 | 是 | 0 | ​ | 45 |
| `画板/白板背景/数字人/对话框/AI回答` | 30 | 280.0x200.0 | 是 | 0 | 那我们开始选择探测仪器吧，有问题随时长按我哦！ | 40行数3->4 |
| `画板/ChatArea/QAPanel/InputRow/SendButton/Text` | 32 | 106.0x78.0 | 是 | 0 | 发送 | 48 |
| `画板/ChatArea/QAPanel/InputRow/InputField/Text Area/Placeholder` | 30 | 336.0x66.0 | 是 | 0 | 请输入问题（最多200字） | 当前已溢出! |
| `画板/开始探测/Text` | 36 | 240.0x76.0 | 是 | 0 | 开始探测 | 54 |
| `画板/ChatArea/QAPanel/Header/Title` | 34 | 500.0x60.0 | 是 | 0 | 向铁小探提问 | 44 |
| `画板/引导遮罩/跳过引导/Text` | 32 | 140.0x60.0 | 是 | 0 | 跳过 | 48 |
| `画板/点击继续/Text` | 36 | 240.0x76.0 | 是 | 0 | 点击继续 | 54 |

## M2

| 层级路径 | 字号 | 容器WxH | 换行 | 溢出模式 | 文本(截断) | 估算可放大至 |
|---|---|---|---|---|---|---|
| `Canvas/SafeArea/ControlDock_D/StepControlArea/ScanControls/Hint` | 26 | 360.0x60.0 | 是 | 0 | 向前移动探头（150→100mm） | 不建议(无余量) |
| `Canvas/SafeArea/ControlDock_D/StepProgress/Text` | 26 | 360.0x48.0 | 是 | 0 | 步骤1：涂抹耦合剂 | 36 |
| `Canvas/SafeArea/ControlDock_D/StepControlArea/HelpControls/TryAgainButton/Text` | 26 | 160.0x64.0 | 是 | 0 | 继续尝试 | 39 |
| `Canvas/SafeArea/ControlDock_D/StepControlArea/HelpControls/AutoDemoButton/Text` | 26 | 160.0x64.0 | 是 | 0 | 自动演示 | 39 |
| `Canvas/SafeArea/ControlDock_D/InstructionArea/Text` | 26 | 648.0x60.0 | 是 | 0 | {} | 39 |
| `Canvas/SafeArea/ControlDock_D/StepControlArea/HelpControls/Text` | 26 | 420.0x48.0 | 是 | 0 | 需要帮助调整到 10° 吗？ | 36 |
| `Canvas/SafeArea/ModalLayer/ResetConfirmDialog/ConfirmButton/Text` | 30 | 160.0x64.0 | 是 | 0 | 确认重置 | 40 |
| `Canvas/SafeArea/MainScene/RailArea/PerspectiveBar_C/ViewModeSegment/PerspectiveButton/Text` | 24 | 182.0x64.0 | 是 | 0 | 透视视图 | 36 |
| `Canvas/SafeArea/DigitalHumanStage/dialog/text` | 36 | 331.1x50.0 | 是 | 0 | {} | 40 |
| `Canvas/SafeArea/QALayer/ChatArea/QAPanel/Header/CloseButton/Text` | 36 | 64.0x64.0 | 是 | 0 | × | 46 |
| `Canvas/SafeArea/ControlDock_D/StepControlArea/CompletionControls/Text` | 30 | 320.0x56.0 | 是 | 0 | 轨头顶面探测完成 | 40 |
| `Canvas/SafeArea/ModalLayer/ResetConfirmDialog/Title` | 34 | 600.0x60.0 | 是 | 0 | 重置 M2 流程？ | 44 |
| `Canvas/SafeArea/MainScene/RailArea/RailViewport/MeasurementBubble/Text` | 28 | 140.0x0.0 | 是 | 0 | 110mm | 容器为0 |
| `Canvas/SafeArea/ControlDock_D/StepControlArea/AngleControls/SliderLabel` | 24 | 110.0x48.0 | 是 | 0 | 探头偏角 | 26 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/WaveGrid/Scale100` | 14 | 44.0x20.0 | 是 | 0 | 100 | 不建议(无余量) |
| `Canvas/SafeArea/HeaderBar/ModuleTitle` | 36 | 600.0x64.0 | 是 | 0 | 轨头顶面探测 | 46 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_20.0` | 12 | 80.0x20.0 | 是 | 0 | 20.0 | 14 |
| `Canvas/SafeArea/ControlDock_D/StepControlArea/ScanControls/NextButton/Text` | 28 | 160.0x64.0 | 是 | 0 | 下一步 | 42 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_120.0mm` | 12 | 80.0x20.0 | 是 | 0 | 120.0mm | 14 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_40.0mm` | 12 | 80.0x20.0 | 是 | 0 | 40.0mm | 14 |
| `Canvas/SafeArea/QALayer/ChatArea/QAPanel/InputRow/VoiceButton/Text` | 30 | 76.0x78.0 | 是 | 0 | 语音 | 38 |
| `Canvas/SafeArea/QALayer/ChatArea/QAPanel/Header/Title` | 34 | 400.0x60.0 | 是 | 0 | 向铁小探提问 | 44 |
| `Canvas/SafeArea/QALayer/ChatArea/QAPanel/InputRow/InputField/Text Area/Text` | 30 | 336.0x66.0 | 是 | 0 | ​ | 45 |
| `Canvas/SafeArea/ControlDock_D/StepControlArea/AngleControls/AngleValue` | 26 | 80.0x48.0 | 是 | 0 | 0° | 36 |
| `Canvas/SafeArea/ControlDock_D/StepControlArea/AngleControls/AngleStatus` | 24 | 200.0x48.0 | 是 | 0 | 请增大偏角 | 36 |
| `Canvas/SafeArea/QALayer/ChatArea/QAPanel/InputRow/CounterText` | 22 | 120.0x24.0 | 是 | 0 | 0/200 | 当前已溢出! |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/DetectionBanner/Text` | 24 | 240.0x0.0 | 是 | 0 | 检出伤损！ | 容器为0 |
| `Canvas/SafeArea/ModalLayer/ResetConfirmDialog/CancelButton/Text` | 30 | 160.0x64.0 | 是 | 0 | 取消 | 45 |
| `Canvas/SafeArea/ControlDock_D/StepControlArea/CouplantControls/ApplyButton/Text` | 28 | 260.0x72.0 | 是 | 0 | 涂抹耦合剂 | 42 |
| `Canvas/SafeArea/MainScene/RailArea/ToolShelf/RulerHome/Chip/Text (TMP)` | 22.4 | 200.0x50.0 | 是 | 0 | 多功能尺子 | 33.6 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_200.0mm` | 12 | 80.0x20.0 | 是 | 0 | 200.0mm | 14 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_80.0` | 12 | 80.0x20.0 | 是 | 0 | 80.0 | 14 |
| `Canvas/SafeArea/ControlDock_D/StepControlArea/CompletionControls/EnterNextButton/Text` | 28 | 242.2x64.0 | 是 | 0 | 进入轨头侧面探测 | 30 |
| `Canvas/SafeArea/MainScene/RailArea/PerspectiveBar_C/ViewModeSegment/NormalButton/Text` | 24 | 182.0x64.0 | 是 | 0 | 普通视图 | 36 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_0.0` | 12 | 80.0x20.0 | 是 | 0 | 0.0 | 14 |
| `Canvas/SafeArea/QALayer/ChatArea/QAPanel/InputRow/SendButton/Text` | 32 | 106.0x78.0 | 是 | 0 | 发送 | 48 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_100.0` | 12 | 80.0x20.0 | 是 | 0 | 100.0 | 14 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/WaveGrid/Scale150` | 14 | 44.0x20.0 | 是 | 0 | 150 | 不建议(无余量) |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_160.0mm` | 12 | 80.0x20.0 | 是 | 0 | 160.0mm | 14 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_60.0` | 12 | 80.0x20.0 | 是 | 0 | 60.0 | 14 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_80.0mm` | 12 | 80.0x20.0 | 是 | 0 | 80.0mm | 14 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_40.0` | 12 | 80.0x20.0 | 是 | 0 | 40.0 | 14 |
| `Canvas/SafeArea/MainScene/RailArea/ToolShelf/ProbeHome/Chip/Text (TMP)` | 22.4 | 200.0x50.0 | 是 | 0 | K2.5 探头 | 33.6 |
| `Canvas/SafeArea/ControlDock_D/StepControlArea/MeasureControls/Hint` | 26 | 420.0x60.0 | 是 | 0 | 拖动尺子：0 刻度对齐焊缝熔合线 | 28 |
| `Canvas/SafeArea/HeaderBar/ResetButton/Text` | 28 | 168.0x64.0 | 是 | 0 | 重置流程 | 42 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_0.0mm` | 12 | 80.0x20.0 | 是 | 0 | 0.0mm | 14 |
| `Canvas/SafeArea/QALayer/ChatArea/QAPanel/InputRow/InputField/Text Area/Placeholder` | 30 | 336.0x66.0 | 是 | 0 | 请输入问题（最多200字） | 当前已溢出! |

## M3

| 层级路径 | 字号 | 容器WxH | 换行 | 溢出模式 | 文本(截断) | 估算可放大至 |
|---|---|---|---|---|---|---|
| `Canvas/SafeArea/ControlDock_D/HelpPanel/HelpText` | 30 | 1288.0x0.0 | 是 | 0 | 需要帮助吗？ | 容器为0 |
| `Canvas/SafeArea/HeaderBar/ResetButton/Text` | 28 | 168.0x64.0 | 是 | 0 | 重置流程 | 42 |
| `Canvas/SafeArea/ModalLayer/ResetConfirmDialog/ConfirmButton/Text` | 28 | 176.0x64.0 | 是 | 0 | 确认重置 | 42 |
| `Canvas/SafeArea/ControlDock_D/CompletionPanel/EnterNextButton/Text` | 30 | 297.9x76.0 | 是 | 0 | 进入轨腰部位探测 | 36 |
| `Canvas/SafeArea/ControlDock_D/CompletionPanel/CompletionText` | 32 | 1488.0x0.0 | 是 | 0 | 下一模块待接入 | 容器为0 |
| `Canvas/SafeArea/ControlDock_D/HelpPanel/TryAgainButton/Text` | 26 | 120.0x60.0 | 是 | 0 | 再试试 | 39 |
| `Canvas/SafeArea/ModalLayer/ResetConfirmDialog/Title` | 34 | 600.0x56.0 | 是 | 0 | 重置 M3 流程？ | 44 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/H0` | 16 | 80.0x24.0 | 是 | 0 | 0.0mm | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/H1` | 16 | 80.0x24.0 | 是 | 0 | 40.0mm | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/H2` | 16 | 80.0x24.0 | 是 | 0 | 80.0mm | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/H3` | 16 | 80.0x24.0 | 是 | 0 | 120.0mm | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/H4` | 16 | 80.0x24.0 | 是 | 0 | 160.0mm | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/H5` | 16 | 80.0x24.0 | 是 | 0 | 200.0mm | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/V0` | 16 | 60.0x24.0 | 是 | 0 | 0.0 | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/V1` | 16 | 60.0x24.0 | 是 | 0 | 20.0 | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/V2` | 16 | 60.0x24.0 | 是 | 0 | 40.0 | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/V3` | 16 | 60.0x24.0 | 是 | 0 | 60.0 | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/V4` | 16 | 60.0x24.0 | 是 | 0 | 80.0 | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/V5` | 16 | 60.0x24.0 | 是 | 0 | 100.0 | 18 |
| `Canvas/SafeArea/MainScene/RailArea/PerspectiveBar_C/PerspectiveButton/Text` | 24 | 182.0x64.0 | 是 | 0 | 透视视图 | 36 |
| `Canvas/SafeArea/DigitalHumanStage/dialog/text` | 36 | 331.1x50.0 | 是 | 0 | {} | 40 |
| `Canvas/SafeArea/ControlDock_D/HelpPanel/AutoDemoButton/Text` | 26 | 160.0x60.0 | 是 | 0 | 自动演示 | 39 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/DetectionBanner/Text` | 21 | 232.0x36.0 | 是 | 0 | 120mm 伤损检出 | 27 |
| `Canvas/SafeArea/QALayer/QAPanel/Placeholder` | 30 | 560.0x600.0 | 是 | 0 | 问答面板 | 60 |
| `Canvas/SafeArea/ControlDock_D/PositioningControls/AngleValue` | 30 | 96.0x64.0 | 是 | 0 | 13° | 45 |
| `Canvas/SafeArea/ControlDock_D/PositioningControls/AngleLabel` | 22 | 130.0x48.0 | 是 | 0 | 向下偏转 | 32 |
| `Canvas/SafeArea/ControlDock_D/InstructionArea/Title` | 27 | 684.9x46.0 | 是 | 0 | 将探头放在轨头侧面，利用多功能尺将探头向下偏转13° | 不建议(无余量) |
| `Canvas/SafeArea/MainScene/RailArea/PerspectiveBar_C/NormalButton/Text` | 24 | 182.0x64.0 | 是 | 0 | 普通视图 | 36 |
| `Canvas/SafeArea/ModalLayer/ResetConfirmDialog/CancelButton/Text` | 28 | 176.0x64.0 | 是 | 0 | 取消 | 42 |
| `Canvas/SafeArea/HeaderBar/ModuleTitle` | 36 | 640.0x64.0 | 是 | 0 | 轨头侧面探测 | 46 |
| `Canvas/SafeArea/ControlDock_D/StepProgress/Text` | 27 | 413.3x176.0 | 是 | 0 | 步骤1：探头定位 | 54 |

## M4

| 层级路径 | 字号 | 容器WxH | 换行 | 溢出模式 | 文本(截断) | 估算可放大至 |
|---|---|---|---|---|---|---|
| `Canvas/SafeArea/ControlDock_D/HelpPanel/HelpText` | 30 | 1288.0x0.0 | 是 | 0 | 需要帮助吗？ | 容器为0 |
| `Canvas/SafeArea/HeaderBar/ResetButton/Text` | 28 | 168.0x64.0 | 是 | 0 | 重置流程 | 42 |
| `Canvas/SafeArea/ModalLayer/ResetConfirmDialog/ConfirmButton/Text` | 28 | 176.0x64.0 | 是 | 0 | 确认重置 | 42 |
| `Canvas/SafeArea/ControlDock_D/CompletionPanel/EnterNextButton/Text` | 30 | 240.0x76.0 | 是 | 0 | 下一步 | 60 |
| `Canvas/SafeArea/ControlDock_D/CompletionPanel/CompletionText` | 32 | 1488.0x0.0 | 是 | 0 | 下一模块待接入 | 容器为0 |
| `Canvas/SafeArea/ControlDock_D/HelpPanel/TryAgainButton/Text` | 26 | 120.0x60.0 | 是 | 0 | 再试试 | 39 |
| `Canvas/SafeArea/DigitalHumanStage/dialog/text` | 36 | 331.1x50.0 | 是 | 0 | {} | 40 |
| `Canvas/SafeArea/ModalLayer/ResetConfirmDialog/Title` | 34 | 600.0x56.0 | 是 | 0 | 重置 M4 流程？ | 44 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/H0` | 16 | 80.0x24.0 | 是 | 0 | 0.0mm | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/H1` | 16 | 80.0x24.0 | 是 | 0 | 40.0mm | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/H2` | 16 | 80.0x24.0 | 是 | 0 | 80.0mm | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/H3` | 16 | 80.0x24.0 | 是 | 0 | 120.0mm | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/H4` | 16 | 80.0x24.0 | 是 | 0 | 160.0mm | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/H5` | 16 | 80.0x24.0 | 是 | 0 | 200.0mm | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/V0` | 16 | 60.0x24.0 | 是 | 0 | 0.0 | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/V1` | 16 | 60.0x24.0 | 是 | 0 | 20.0 | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/V2` | 16 | 60.0x24.0 | 是 | 0 | 40.0 | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/V3` | 16 | 60.0x24.0 | 是 | 0 | 60.0 | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/V4` | 16 | 60.0x24.0 | 是 | 0 | 80.0 | 18 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/V5` | 16 | 60.0x24.0 | 是 | 0 | 100.0 | 18 |
| `Canvas/SafeArea/MainScene/RailArea/PerspectiveBar_C/PerspectiveButton/Text` | 24 | 182.0x64.0 | 是 | 0 | 透视视图 | 36 |
| `Canvas/SafeArea/ControlDock_D/HelpPanel/AutoDemoButton/Text` | 26 | 160.0x60.0 | 是 | 0 | 自动演示 | 39 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/DetectionBanner/Text` | 21 | 232.0x36.0 | 是 | 0 | 40mm 伤损检出 | 27 |
| `Canvas/SafeArea/QALayer/QAPanel/Placeholder` | 30 | 560.0x600.0 | 是 | 0 | 问答面板 | 60 |
| `Canvas/SafeArea/ControlDock_D/PositioningControls/AngleValue` | 30 | 96.0x64.0 | 是 | 0 | 10° | 45 |
| `Canvas/SafeArea/ControlDock_D/PositioningControls/AngleLabel` | 22 | 130.0x48.0 | 是 | 0 | 向上偏转 | 32 |
| `Canvas/SafeArea/ControlDock_D/InstructionArea/Title` | 27 | 747.0x46.0 | 是 | 0 | 将探头放在轨腰最上端，利用多功能尺将探头向上偏转10° | 29 |
| `Canvas/SafeArea/MainScene/RailArea/PerspectiveBar_C/NormalButton/Text` | 24 | 182.0x64.0 | 是 | 0 | 普通视图 | 36 |
| `Canvas/SafeArea/ModalLayer/ResetConfirmDialog/CancelButton/Text` | 28 | 176.0x64.0 | 是 | 0 | 取消 | 42 |
| `Canvas/SafeArea/HeaderBar/ModuleTitle` | 36 | 640.0x64.0 | 是 | 0 | 轨腰部位探测 | 46 |
| `Canvas/SafeArea/ControlDock_D/StepProgress/Text` | 27 | 413.3x176.0 | 是 | 0 | 步骤1：探头偏角 | 54 |

## M5

| 层级路径 | 字号 | 容器WxH | 换行 | 溢出模式 | 文本(截断) | 估算可放大至 |
|---|---|---|---|---|---|---|
| `Canvas/SafeArea/ControlDock_D/StepProgress/StepProgressText` | 26 | 379.8x176.0 | 是 | 0 | 步骤 1/1 · 擦拭耦合剂 | 52行数1->2 |
| `Canvas/SafeArea/ControlDock_D/InstructionArea/InstructionText` | 26 | 529.6x176.0 | 是 | 0 | 请将擦拭布拖至钢轨顶面，由左至右擦拭 | 52行数1->2 |
| `Canvas/SafeArea/ModalLayer/ResetConfirmDialog/ConfirmButton/Text` | 30 | 160.0x64.0 | 是 | 0 | 确认重置 | 40 |
| `Canvas/SafeArea/MainScene/RailArea/RailViewport/PerspectiveBar_C/ViewModeSegment/PerspectiveButton/Text` | 24 | 182.0x64.0 | 是 | 0 | 透视视图 | 36 |
| `Canvas/SafeArea/DigitalHumanStage/dialog/text` | 36 | 331.1x50.0 | 是 | 0 | {} | 40 |
| `Canvas/SafeArea/ModalLayer/ResetConfirmDialog/Title` | 34 | 600.0x60.0 | 是 | 0 | 重置 M2 流程？ | 44 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/WaveGrid/Scale100` | 14 | 44.0x20.0 | 是 | 0 | 100 | 不建议(无余量) |
| `Canvas/SafeArea/HeaderBar/ModuleTitle` | 36 | 600.0x64.0 | 是 | 0 | 擦拭耦合剂 | 46 |
| `Canvas/SafeArea/MainScene/Tool/RagHome/Rag/ScaleText` | 18 | 80.0x24.0 | 是 | 0 | 0        50        100        150 | 当前已溢出! |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_20.0` | 12 | 80.0x20.0 | 是 | 0 | 20.0 | 14 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_120.0mm` | 12 | 80.0x20.0 | 是 | 0 | 120.0mm | 14 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_40.0mm` | 12 | 80.0x20.0 | 是 | 0 | 40.0mm | 14 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/DetectionBanner/Text` | 24 | 240.0x0.0 | 是 | 0 | 检出伤损！ | 容器为0 |
| `Canvas/SafeArea/ModalLayer/ResetConfirmDialog/CancelButton/Text` | 30 | 160.0x64.0 | 是 | 0 | 取消 | 45 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_200.0mm` | 12 | 80.0x20.0 | 是 | 0 | 200.0mm | 14 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_80.0` | 12 | 80.0x20.0 | 是 | 0 | 80.0 | 14 |
| `Canvas/SafeArea/CompletionPanel/CompletionText` | 42 | 1920.0x1080.0 | 是 | 0 | M5 擦拭耦合剂完成 | 84 |
| `Canvas/SafeArea/MainScene/RailArea/RailViewport/PerspectiveBar_C/ViewModeSegment/NormalButton/Text` | 24 | 182.0x64.0 | 是 | 0 | 普通视图 | 36 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_0.0` | 12 | 80.0x20.0 | 是 | 0 | 0.0 | 14 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_100.0` | 12 | 80.0x20.0 | 是 | 0 | 100.0 | 14 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/WaveGrid/Scale150` | 14 | 44.0x20.0 | 是 | 0 | 150 | 不建议(无余量) |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_160.0mm` | 12 | 80.0x20.0 | 是 | 0 | 160.0mm | 14 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_60.0` | 12 | 80.0x20.0 | 是 | 0 | 60.0 | 14 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_80.0mm` | 12 | 80.0x20.0 | 是 | 0 | 80.0mm | 14 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_40.0` | 12 | 80.0x20.0 | 是 | 0 | 40.0 | 14 |
| `Canvas/SafeArea/HeaderBar/ResetButton/Text` | 28 | 168.0x64.0 | 是 | 0 | 重置流程 | 42 |
| `Canvas/SafeArea/MainScene/SupportArea/WaveformArea_B/ScaleTexts/ScaleText_0.0mm` | 12 | 80.0x20.0 | 是 | 0 | 0.0mm | 14 |
| `Canvas/SafeArea/MainScene/Tool/RulerHome/Ruler/ScaleText` | 18 | 150.0x24.0 | 是 | 0 | 0        50        100        150 | 当前已溢出! |
