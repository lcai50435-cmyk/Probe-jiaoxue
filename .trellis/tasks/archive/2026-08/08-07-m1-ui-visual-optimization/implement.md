# M1 界面横屏适配与视觉优化 — 实施计划

## 实施清单（按序）

1. **更新 `Assets/Editor/M1QASetup.cs`**（常量 + CreatePanel 布局，与 design.md 参数表一致）：
   - `PanelWidth` 780 → 700；`HeaderHeight` 110 → 100；`InputRowHeight` 130 → 120。
   - Header `Title` 字号 34 → 40。
   - InputRow：输入框 470 → 420（pos 不变）；语音 76 → 110、字号 30；发送 130 → 110、字号 32 → 30；三个 pos 改为 (18,4)/(450,4)/(572,4)。
   - CounterText：改为挂到 InputField/Text Area 下，anchor(1,0)、pos(-8,6)、字号 22 → 18；Text Area 右内边距 -12 → -80。
   - `comp.counterTextPath` 默认值同步为 `QAPanel/Panel/InputRow/InputField/Text Area/CounterText`。

2. **新建 `Assets/Editor/M1LayoutPolish.cs`**（幂等工具，菜单 `Tools/M1/优化 M1 界面布局`）：
   - 打开 `Assets/Settings/Scenes/M1.unity`，按名称查找（复用 FindIncludingInactive 模式），全部改动走工具：
   - CanvasScaler `m_MatchWidthOrHeight` → 0。
   - 标题栏高度 160 → 120。
   - 标题：anchor(0,0.5)、pos(30,0)、字号 50 → 40。
   - 物品容器：anchor(0,0.5)、pos(612,60)、size(1164,768)。
   - 6 张卡片：size 372×372；pos 按各自锚点重设 ±186（超声波焊缝探伤仪(0,1)→(186,-186)、手推式(1,1)→(-186,-186)、双轨式(0.5,1)→(0,-186)、轨距尺(0.5,0)→(0,186)、钢轨打磨机(1,0)→(-186,186)、内燃威客镐(0,0)→(186,186)）；Image `PreserveAspect` → 1。
   - QAPanel sizeDelta.x → 700；Header 高 → 100；Header Title 字号 → 40。
   - InputRow 高 → 120；输入框宽 → 420；语音/发送 110×110 字号 30、pos(450,4)/(572,4)。
   - 计数器迁移到 InputField/Text Area 下（anchor(1,0)、pos(-8,6)、字号 18），Text Area offsetMax.x → -80。
   - `EditorUtility.SetDirty` + `MarkSceneDirty` + `SaveScene`；重复执行校验（幂等）。

3. **人工验证（用户 Unity 编辑器执行）**：
   - 运行 `Tools/M1/优化 M1 界面布局` 后打开场景 `Assets/Settings/Scenes/M1.unity`。
   - Game 视图依次切 16:9、18:9、19.5:9、20:9：顶部标题栏、底部输入行完整可见；卡片 2×3 无裁剪无遮挡。
   - 6 张卡片图片等比居中、无拉伸；间隙横纵均匀。
   - 打开 QAPanel：语音/发送/输入框单行等高对齐；计数器在输入框内右下角；左右标题字号一致；关闭按钮居中。
   - 面板打开时卡片与面板间隙 ≥ 20px 观感。
   - 幂等：菜单重复执行一次，布局不变、无重复对象。
   - 重跑 `Tools/M1/Setup M1-1` 与 `Setup AI 提问面板` 确认不报错、不重复创建。

## 验证命令

- 无 CLI 验证（Unity 场景改动）；以编辑器内人工检查为准，对照 prd.md 验收标准逐条勾选。
- 代码静态检查：`Assets/Editor/*.cs` 编译错误在 Unity 控制台确认无报错。

## 风险文件与回滚点

- `Assets/Editor/M1QASetup.cs`、`Assets/Editor/M1LayoutPolish.cs`（新）、`Assets/Settings/Scenes/M1.unity`。
- 回滚：`git checkout --` 上述文件；工具幂等可重跑。

## 复查项（验收阶段）

- 字体模糊：编辑器 1x 缩放 + 真机预览后若仍模糊，再议 SDF 采样点 60 → 90 重生成（用户已否，暂缓）。
- 发送按钮 110 宽观感（原 130），用户不满意可单独回调。
