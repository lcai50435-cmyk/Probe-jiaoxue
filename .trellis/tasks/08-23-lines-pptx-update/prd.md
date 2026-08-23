# M1-M5 台词按 台词.pptx 更新

## 背景

客户提供 `Assets/交互动画素材/台词.pptx`，要求按其中标注更新 M1-M5 的台词与提示。M2/M3 Scene 冻结（视觉权威，程序/Agent 不得修改），故全部改动走代码默认值与运行时内存态。

## 需求

1. **步骤进度格式**（Slide 3-【2】，M2/M3/M4/M5）：删除「1/4」「/3」「/1」分母与分隔符，改为「步骤X：阶段名」；阶段名对齐 PPT（步骤1：涂抹耦合剂 / 步骤2：探头偏角 / 步骤3：移动探测 / 步骤4：测距确认；M3/M4 无步骤1 涂抹耦合剂）。冒号用**中文全角冒号**。
2. **M1 台词**（Slide 1-2）：AI回答 显示的 6 条台词按 PPT 更新（初始/选错/选对 × M1-1/M1-2），含 M1Setup 规范化与 M1ToolSelection 默认值。
3. **M2/M3/M4 底部提示文案**（DefaultHints）：按 PPT 各模块「改成」条目替换；M2 的涂抹耦合剂提示删除（改由气泡承载）。
4. **数字人台词气泡**（新增，Slide 3-6/8-11/12-15 各「增加数字人气泡」条目）：运行时创建云朵气泡（M2/M3/M4 通用组件），阶段与交互事件触发台词；长台词分段展示；M2 未涂耦合剂拖探头拦截提示。
5. **删除场景静态 Hint**（Slide 5-【4】/6-【4】，M2）：运行时隐藏。
6. **工具架槽位标注**（Slide 3-【6】）：ProbeHome 下方「K2.5探头」、RulerHome 下方「多功能尺」，M2~M5 自动装配。
7. **模块标题去编号前缀**（Slide 3-【1】）：M3/M4/M5 标题去掉「M3/M4/M5 」前缀（M2 已无前缀）。

## 约束

- 冻结 Scene（M2/M3）零修改、零新序列化字段：全部代码默认数组 / 运行时 AddComponent / DontSave 内存态。
- 低代码优先：仅因冻结 Scene 无法建节点而新增脚本（ModuleSpeechBubble / ModuleToolShelfLabel / ModuleTitleStrip），组件通用复用。
- 问答面板暂停（timeScale=0）期间气泡分段不受影响（unscaled 计时）。

## 验收标准

- [x] 步骤进度显示「步骤X：阶段名」（中文冒号），M2~M5 全部生效
- [x] M1 AI回答 台词 = PPT 原文（直接 Play 无需重跑 Setup）
- [x] M2/M3/M4 底部提示 = PPT 文案
- [x] M2/M3/M4 数字人气泡按阶段显示台词；M2 未涂耦合剂拖探头提示；测量完成长台词分段展示
- [x] M2 两个场景静态 Hint 隐藏
- [x] M2~M5 工具架槽位显示「K2.5探头 / 多功能尺」标注（含场景切换）
- [x] M3/M4/M5 模块标题无编号前缀
- [x] M2.unity / M3.unity 场景文件未被程序改动（冻结合规）
- [ ] 老板 Unity 验证气泡位置/尺寸（默认 anchorOffset (-60,220) / 320×283 / 字号 26，可调）

## 变更文件

- `Assets/Scripts/M1ToolSelection.cs`、`Assets/Editor/M1Setup.cs`、`Assets/Settings/Scenes/M1.unity`（台词字段）
- `Assets/Scripts/M2FlowController.cs`、`M3FlowController.cs`、`M4FlowController.cs`、`M5FlowController.cs`（步骤格式/提示/气泡接入）
- `Assets/Scripts/M2ProbeDrag.cs`（未涂拖探头提示）
- 新增 `Assets/Scripts/ModuleSpeechBubble.cs`、`ModuleToolShelfLabel.cs`、`ModuleTitleStrip.cs`
- 新增 `Assets/Resources/DigitalHuman/dialog.png`（云朵气泡图，手写 meta）
