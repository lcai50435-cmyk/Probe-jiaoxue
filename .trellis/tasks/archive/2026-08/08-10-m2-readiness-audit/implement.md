# M2 UGUI 与交互执行清单

> 状态：待审核。本文是审计任务交付的未来执行方案，不授权立即实现，也不得直接用当前“就绪度审计”任务进入代码阶段。用户批准后，应另建 M2 实现任务并引用本文。

## 1. 执行原则

- [ ] 低代码顺序固定为：场景/Inspector 配置 → 复用现有组件 → Editor Setup → 必要 runtime 脚本。
- [ ] 所有场景结构、锚点和尺寸由幂等 `M2Setup` 管理，不直接手改 M2 场景 YAML。
- [ ] 新增 runtime 脚本默认每个不超过 150 行；超限先检查职责和配置边界。
- [ ] 不把钢轨、探头、声束、伤损、耦合剂和尺子烘焙成单张交互图。
- [ ] 正式素材缺失不阻塞功能原型，但占位效果不作为最终视觉验收结果。
- [ ] 不修改或覆盖工作区中已有的 `Assets/Settings/Scenes/M1.unity` 用户改动。
- [ ] 不播放 M2 引导动画，不提供返回 M1，不实现假 M3。
- [ ] 不复制 M1 QA/数字人逻辑：Editor 侧优先提取参数化公共 Ensure（数字人舞台/问答面板层级），M2Setup 复用；M1 行为回归必须检查。
- [ ] 数字人复用 M1 组件与素材链路（M1DigitalHumanPresenter / M1PressDetector / VideoPlayer / RenderTexture / UI-LumaKey-DigitalHuman 与待机/思考/讲解视频），不为 M2 新增数字人 runtime 脚本。

## 2. 预检与基线

### 2.1 工作区保护

- [ ] 执行 `git status --short`，记录所有既有改动。
- [ ] 执行 `git diff -- Assets/Settings/Scenes/M1.unity`，仅用于识别用户改动，不回退、不格式化该场景。
- [ ] 核对 Unity 版本：读取 `ProjectSettings/ProjectVersion.txt`。
- [ ] 确认 Unity Editor 未在 Play 模式，批处理执行时项目未被另一个 Editor 锁定。
- [ ] 确认现有 M2 场景可打开：`Assets/Settings/Scenes/M2.unity`。

### 2.2 素材和引用预检

- [ ] 确认 K2.5 透明探头及 `.meta` 存在。
- [ ] 确认蜂鸣、拖拽、点击音效及 `.meta` 存在。
- [ ] 确认中文 TMP 字体资产存在。
- [ ] 确认 `M1QAPanel`、`M1DeepSeekClient` 和问答面板所需输入组件可复用。
- [ ] 确认 `M1DigitalHumanPresenter`、`M1PressDetector`、待机/思考/讲解 VideoClip、`UI-LumaKey-DigitalHuman.mat` 可复用（M1 常驻数字人链路）。
- [ ] 将缺失正式素材保持为已知项：干净钢轨俯视图、同构图透明钢轨、自制尺透明成品。

### 2.3 代码搜索

- [ ] 搜索现有拖拽、波形、自定义 Graphic、SafeArea、场景跳转和共享 Setup 能力，避免重复实现。
- [ ] 搜索所有 `M2` 命名和路径，防止与占位对象或后续模块约定冲突。
- [ ] 搜索 `M1QASetup` 的私有层级创建逻辑（Blocker/ChatArea/QAPanel/DigitalHumanStage/FullBodyView），确定最小参数化复用边界（根节点、面板宽度、展开方向、是否含数字人舞台）；不复制整段实现。
- [ ] 搜索 `M1DigitalHumanPresenter`/`M1PressDetector` 事件接口与 `UI-LumaKey-DigitalHuman` 材质引用，确认复用方式。

建议命令：

```bash
rg -n "IBeginDragHandler|IDragHandler|IEndDragHandler|OnPopulateMesh|SafeArea|SceneManager|LoadScene" Assets/Scripts Assets/Editor -g '*.cs'
rg -n "M2|QAPanel|M1QASetup" Assets/Scripts Assets/Editor Assets/Settings/Scenes -g '*.cs' -g '*.unity'
```

## 3. 里程碑一：静态 UGUI 线框（第三版）

> 审核门槛 A：本里程碑只生成可视布局和占位层，不接完整状态机。**第三版**线框需重新生成三视口截图经用户确认后才能进入里程碑二；第二版线框不作为通过依据。

### 3.1 新增幂等 Setup 骨架

- [ ] 新增 `Assets/Editor/M2Setup.cs`。
- [ ] 提供菜单入口 `Tools/M2/Setup M2`。
- [ ] 提供批处理入口 `M2.EditorTools.M2Setup.SetupM2Batch`。
- [ ] 固定打开 `Assets/Settings/Scenes/M2.unity`，重复执行不重复创建节点或组件。
- [ ] 生成对象命名与 `design.md` 第 5 节一致。
- [ ] 仅修改 M2 场景，不打开或保存 M1 场景。

### 3.2 修正 Canvas

- [ ] 设置 `Scale With Screen Size`。
- [ ] 设置参考分辨率 `1920x1080`。
- [ ] 设置 Match `0.5`。
- [ ] Ensure `SafeArea`、`Background`、`HeaderBar`、`MainScene`、`ControlDock_D`、`DigitalHumanStage`、`QALayer`、`ModalLayer`。
- [ ] 所有业务节点位于 `SafeArea` 下，覆盖层顺序稳定。

### 3.3 生成四区布局

- [ ] Header 高 80px（只保留标题 + 重置，无 QAEntry），D 区约 176px 高（弱化卡片感），外边距 24px，区域间距 16px。
- [ ] 主教学场景全幅化/主导：工具暂存、钢轨自然融入；B 区为紧凑辅助仪器约 460x240（允许 440–480x220–250），不纵向贯穿。
- [ ] DigitalHumanStage 右侧常驻（约 300–320px 宽、无边框），预留全身舞台/构图。
- [ ] C 模式控件融入主场景左下（控件高度约 64px）。
- [ ] D 区建立左提示、中控件、右进度三块稳定布局。
- [ ] 触控控件在 1920x1080 基准下不低于 64px 高。
- [ ] 不使用嵌套装饰卡片、强发光和复杂渐变。

### 3.4 生成静态占位层

- [ ] 主场景生成工具暂存位、钢轨俯视占位、焊缝线、耦合剂层、伤损点、声束层、探头、尺子和测量气泡占位。
- [ ] 使用现有 K2.5 透明 PNG；素材字段为空时注入，非空时保留用户设置。
- [ ] 钢轨和尺子缺失时使用 UGUI 简化占位，不加载带探头/文字的合成参考图。
- [ ] B 区生成标题、状态、当前距离、目标距离、网格、单波形区域和 110mm 目标线（紧凑 460x240，低对比）。
- [ ] DigitalHumanStage 生成舞台/构图占位（右侧、无边框、全身）：静态审核使用可识别的全身预览或 Play Mode 运行截图，不只放头像；正式视频/Presenter/QA 在核心流程后接入。
- [ ] C 模式控件“普通视图 / 透视视图”固定尺寸分段控件，融入主场景左下。
- [ ] D 区生成四组步骤控件容器，但初始只显示 Step 1。
- [ ] Header 只显示标题和重置，不显示返回按钮，不保留 QAEntry 主入口。

### 3.5 静态布局验证

- [ ] 连续运行 Setup 两次，对比第二次前后 M2 场景哈希，确认幂等。
- [ ] 检查 M2 场景无 Missing Script、孤立 YAML 块和重复节点。
- [ ] 在 1920x1080、1280x720、2436x1125 横屏截图（第三版：全幅主场景 + 紧凑 B 区 + 数字人舞台）。
- [ ] 检查文字不溢出、A/B/C/D 无重叠、钢轨和波形不被裁切、D 区控件尺寸稳定、数字人全身完整可见（不被 QAPanel/Blocker 遮挡）。
- [ ] 将截图交用户审核；未通过时只调整 Setup 布局，不进入交互开发。

**审核门槛 A：用户明确批准第三版静态线框（第二版不作数）。**

## 4. 里程碑二：运行时组件骨架

### 4.1 状态所有权

- [ ] 新增 `Assets/Scripts/M2FlowController.cs`。
- [ ] 定义单一流程状态：`Couplant → Positioning → Scanning → Measuring → Completed`。
- [ ] FlowController 是步骤、成功标志、计时、重置和 UI 显隐的唯一所有者。
- [ ] 文案、距离、角度、容差、时间、颜色、音效和出口全部暴露为 Inspector 字段。
- [ ] 使用显式状态切换方法，禁止多个组件分别猜测当前步骤。

### 4.2 探头拖拽

- [ ] 新增 `Assets/Scripts/M2ProbeDrag.cs`。
- [ ] 支持暂存位到起始放置区拖拽。
- [ ] 未涂耦合剂时拒绝拖拽并发送提示事件。
- [ ] 扫描阶段约束到预设轨迹，输出归一化位置和当前距离。
- [ ] 角度非 10°时暂停纵向移动，但保留当前位置。
- [ ] 组件不直接修改波形、步骤文字、下一步按钮或成功状态。

### 4.3 尺子拖拽

- [ ] 新增 `Assets/Scripts/M2RulerDrag.cs`。
- [ ] 前三个阶段保持可见但禁用拖拽和降低透明度。
- [ ] 测距阶段解锁，从暂存位拖向焊缝。
- [ ] 以尺子 `0` 刻度锚点与焊缝锚点计算对齐误差。
- [ ] 进入 Inspector 配置容差后自动吸附并只发送一次完成事件。
- [ ] 测量完成后保持位置，进入后续模块时再复位。

### 4.4 波形绘制

- [ ] 新增 `Assets/Scripts/M2WaveformGraphic.cs`，继承 UGUI `Graphic` 并在 `OnPopulateMesh` 中绘制单条曲线。
- [ ] 只消费归一化距离、显示尺寸和配置参数，不拥有流程状态。
- [ ] 支持平直基线、平滑生长、110mm 峰值和峰后下降。
- [ ] 不加载或生成高密度序列帧。
- [ ] 避免每帧分配临时集合；仅在距离或尺寸变化时标记顶点重建。

### 4.5 Setup 注入

- [ ] M2Setup Ensure 上述组件并注入真实场景引用。
- [ ] 禁止运行时使用 Assets 路径加载素材。
- [ ] 路径字段如存在，必须与生成层级逐层一致；引用优先直接序列化注入。
- [ ] Setup 仅在字段为空时注入素材，不覆盖用户后续替换。
- [ ] 数字人舞台 Ensure：复用 M1 组件（M1DigitalHumanPresenter / M1PressDetector）与 UI-LumaKey-DigitalHuman 材质，通过参数化公共 helper 生成，不复制 M1QASetup 整段逻辑。

### 4.6 编译门槛

- [ ] Unity batchmode 完成脚本编译，无 Error。
- [ ] 新 runtime 脚本逐个检查默认 ≤150 行。
- [ ] `git diff --check` 通过。

## 5. 里程碑三：四阶段流程

### 5.1 Step 1 耦合剂

- [ ] 初始锁定探头、锁定并置灰尺子。
- [ ] 点击按钮后从焊缝中心向左右扩散浅蓝覆盖层，持续 2 秒后淡出。
- [ ] 动画期间防重复点击。
- [ ] 动画完成后按钮显示“已涂抹”并置灰，探头解锁，进入 Step 2。
- [ ] 耦合剂层不拦截 Raycast。

### 5.2 Step 2 偏角定位

- [ ] Slider 范围 0°–20°，整度步进，初始 0°。
- [ ] 根据小于、等于、大于 10°显示对应提示和状态色。
- [ ] 探头进入起始放置区且角度为 10°时自动进入 Step 3。
- [ ] 不增加“确认角度”按钮。

### 5.3 Step 3 移动探测

- [ ] 将轨迹位置映射为 150mm→100mm，方向和显示一致。
- [ ] B 区集中更新状态、当前距离和实时波形。
- [ ] 使用跨越目标或进入目标容差判断 110mm，禁止依赖浮点精确相等。
- [ ] 首次命中 110mm 时蜂鸣一次、锁定结果、显示目标标记和“下一步”。
- [ ] 探头可继续移动并显示峰后下降，成功状态不撤销、不重复蜂鸣。
- [ ] AudioSource 强制 `spatialBlend=0`，使用 `PlayOneShot`。

### 5.4 Step 4 尺子测距

- [ ] 点击 Step 3“下一步”后解锁并高亮尺子。
- [ ] 对齐焊缝后自动吸附、显示 110mm 放大气泡并播放正确反馈。
- [ ] 不显示“完成测距”按钮。
- [ ] 显示“进入下一模块”，保持尺子和气泡供观察。
- [ ] 点击出口时复位尺子并进入 Completed。

### 5.5 流程手动验证

- [ ] 验证每个步骤只能按合同进入下一步。
- [ ] 验证提前拖探头、提前拖尺子、错误角度、快速跨过 110mm、重复经过 110mm。
- [ ] 验证拖到扫描边界、释放到区域外、分辨率切换和应用失焦恢复。

## 6. 里程碑四：透视、帮助、重置与完成出口

### 6.1 普通/透视

- [ ] C 模式控件（主场景左下）全流程可切换，默认普通视图。
- [ ] 透视状态降低钢轨透明度并显示伤损、入射线和反射线。
- [ ] 声束随探头位置和角度更新，不使用流动粒子。
- [ ] 检出后伤损红转黄并短暂高亮。
- [ ] 用户未体验透视时只高亮 C 区，不弹模态框。
- [ ] 切换视图不改变流程、位置、角度、波形和测量结果。

### 6.2 30/60 秒帮助

- [ ] Step 2 30 秒无操作时，在 D 区显示“自动演示/继续尝试”。
- [ ] 自动演示约 1 秒平滑调整到 10°。
- [ ] Step 3 60 秒无操作时提示后自动滑向 110mm，并走正常检出路径。
- [ ] 自动演示期间锁定冲突输入，结束后恢复。
- [ ] 有效操作重置当前计时；QA、确认框、应用暂停时暂停计时。

### 6.3 重置流程

- [ ] 顶部“重置流程”打开二次确认框。
- [ ] 取消时不改变任何状态。
- [ ] 确认时统一复位步骤、工具、耦合剂、偏角、波形、检出、测距、帮助计时和普通视图。
- [ ] 不返回 M1、不重播引导、不清除问答历史。
- [ ] 重置后可再次完整执行 M2，不产生重复监听或重复音效。

### 6.4 完成出口

- [ ] FlowController 暴露可配置 `UnityEvent` 下一模块出口。
- [ ] 如需场景名，字段为空时保持完成状态并显示“下一模块待接入”，不得抛异常。
- [ ] M3 未就绪时不创建 M3 假交互。
- [ ] M2 独立验收以 Completed 状态稳定显示为终点。

## 7. 里程碑五：全身数字人与问答复用

> 审核门槛 B：核心 M2 流程先独立通过，再接入数字人/问答，避免复用影响主流程定位。
> 静态里程碑（一）只预留数字人舞台与构图（可识别全身预览或 Play Mode 运行截图）；实际视频/Presenter/QA 在核心流程独立验收后接入。

### 7.1 复用设计

- [ ] 第三版静态线框阶段：M2Setup 预留 DigitalHumanStage 舞台/构图（右侧 300–320px 无边框、全身占位），静态审核使用可识别的全身预览或 Play Mode 运行截图，不只放头像。
- [ ] 核心流程独立验收后接入：复用 `M1DigitalHumanPresenter`、`M1PressDetector`、`M1QAPanel`、`M1DeepSeekClient`、VideoPlayer/RenderTexture/UI-LumaKey-DigitalHuman 及待机/思考/讲解视频。
- [ ] 与 M1 一致：默认全身待机；面板打开时全身显示；DigitalHumanStage 位于 Blocker/QAPanel 之上，不被压暗或拦截。
- [ ] QAPanel 在数字人左侧展开；不保留 Header 文字/头像 QAEntry 作为主入口。
- [ ] 评估将 `M1QASetup` 的“Blocker + QAPanel 层级 + DigitalHumanStage Ensure”提取为 Editor-only 参数化公共 helper（根节点、面板宽度、面板展开方向、是否含数字人舞台）。
- [ ] 不复制 M1 QA/数字人逻辑；公共 helper 修改前再次检查 `M1QASetup.cs` 和 M1 场景的用户改动。
- [ ] M1 保持现有行为；不得为了 M2 重命名 `M1QAPanel` / `M1DigitalHumanPresenter` 或破坏现有路径。

### 7.2 M2 接入

- [ ] M2Setup 通过公共 helper 生成 Blocker/QAPanel/DigitalHumanStage，注入数字人视频、中文字体和 DeepSeek 客户端，保留用户 API 配置。
- [ ] 入口直接调用现有 `M1QAPanel.Open()`，关闭行为沿用现有组件。
- [ ] 面板打开时锁定底层 A/B/C/D 输入并暂停帮助计时；完全关闭后恢复。
- [ ] 面板打开不遮挡、不拦截数字人交互；抽屉在数字人左侧展开，不遮挡后仍让底层探头接收拖拽。

### 7.3 M1 回归保护

- [ ] 编辑公共 QA/数字人 helper 前再次检查 `M1QASetup.cs` 和 M1 场景的用户改动。
- [ ] 不通过 M2 Setup 打开或保存 M1 场景。
- [ ] 运行现有 M1 QA Setup 幂等检查，确认面板、数字人三态视频、长按/短按和 DeepSeek 行为不变。
- [ ] M1 回归失败则回滚公共 helper 提取，M2 保留无数字人的降级入口（如临时隐藏舞台），QA/数字人接入从本次 M2 核心交付中隔离，不影响 M2 主流程。

**审核门槛 B：M2 核心流程和 M1 QA/数字人回归均通过。**

## 8. 里程碑六：M1→M2 串联

> 本里程碑在 M2 独立验收后执行，并与 M1 场景现有用户改动定点合并。

- [ ] 将 M1“开始探测”从占位行为改为可配置加载 M2。
- [ ] 跳转配置不得硬编码到按钮层级；由 Inspector 字段或 UnityEvent 注入。
- [ ] 将 M1、M2 场景加入 Build Settings，移除或禁用错误的 SampleScene 入口前先确认启动场景要求。
- [ ] 从 M1-2 正确选择 K2.5 后进入 M2，M2 不提供返回 M1。
- [ ] 验证直接打开 M2 和从 M1 串联进入 M2 的初始状态一致。
- [ ] 不接 M3，M2 结束仍走可配置出口。

## 9. 全量质量检查

### 9.1 静态检查

```bash
git diff --check
rg -n "Resources\.Load|AssetDatabase|LoadAssetAtPath" Assets/Scripts/M2*.cs
rg -n "QAPanel/Panel|M2.*尚未实现|Constant Pixel Size" Assets/Scripts Assets/Editor Assets/Settings/Scenes/M2.unity
rg -n "DigitalHumanStage|FullBodyView|M1QASetup" Assets/Editor/M2Setup.cs   # 确认经公共 helper 复用，无整段复制
wc -l Assets/Scripts/M2*.cs
```

期望：

- runtime 不包含 Editor API 或 Assets 路径加载。
- 不产生虚构 UI 路径。
- M2 CanvasScaler 已修正。
- runtime 脚本默认各自 ≤150 行；如超限必须在审核记录中说明并拆分或配置化。

### 9.2 Unity 批处理

使用本机 Unity Editor 路径设置 `UNITY_EDITOR` 后执行：

```bash
"$UNITY_EDITOR" -batchmode -quit -projectPath "$PWD" -executeMethod M2.EditorTools.M2Setup.SetupM2Batch -logFile -
"$UNITY_EDITOR" -batchmode -quit -projectPath "$PWD" -executeMethod M2.EditorTools.M2Setup.SetupM2Batch -logFile -
```

- [ ] 两次均退出码 0。
- [ ] 日志无 compile error、Missing Script、路径解析错误和素材加载错误。
- [ ] 第二次执行不产生额外场景 diff。
- [ ] 如果项目被 Unity 占用，不强行并发运行；改为 Editor 菜单执行并保存日志。

### 9.3 Play Mode 验收矩阵

- [ ] 1920x1080：完整流程、QA、重置、透视、帮助。
- [ ] 1280x720：触控尺寸、文字换行、D 区控件和尺子读数。
- [ ] 2436x1125：宽屏边距、A/B 比例、钢轨和波形不拉伸错位。
- [ ] 快速拖拽跨越 110mm仍能检测一次。
- [ ] 重复跨越目标不重复蜂鸣。
- [ ] QA 打开时底层不可操作、计时暂停；数字人仍全身可见、不被压暗或拦截。
- [ ] 长按数字人打开问答、短按切换形态；三态视频随回答状态切换。
- [ ] 自动帮助期间输入锁定正确，结束后恢复。
- [ ] 重置确认和取消路径均正确。
- [ ] 未配置 M3 时完成状态稳定，无异常。

### 9.4 截图证据

- [ ] 保存三种视口的 Step 1、Step 3 峰值、Step 4 测距、透视状态和数字人/QA 打开状态截图。
- [ ] 检查无重叠、裁切、空白波形、不可读刻度和异常拉伸。
- [ ] 正式素材未到位时在验收记录中明确标注“功能占位”，不误报视觉完成。

## 10. 回滚点

### 回滚点 A：静态布局未批准

- 只回滚 M2Setup 生成的命名节点和 M2 场景变更。
- 不保留未获批准的 runtime 组件。
- 不触碰 M1、Build Settings 和 QA 公共代码。

### 回滚点 B：核心交互失败

- 保留已批准的静态布局。
- 回滚对应 M2 runtime 脚本和 Setup 注入，不回退其他用户改动。
- 根据 FlowController、ProbeDrag、RulerDrag、WaveformGraphic 边界定位问题，不整体重写。

### 回滚点 C：QA/数字人复用导致 M1 回归

- 回滚公共 QA/数字人 helper 和 M2 的 QA/数字人接入。
- 保留 M2 无数字人的降级入口（临时隐藏 DigitalHumanStage），不重建 Header QAEntry。
- M2 核心四阶段流程继续独立验收。

### 回滚点 D：M1→M2 串联失败

- 回滚 M1ToolSelection 跳转和 Build Settings 定点改动。
- 保留可直接打开的 M2 独立场景。
- 不回滚已经通过验收的 M2 内容。

## 11. 文件边界

### 预计新增

- `Assets/Editor/M2Setup.cs`
- `Assets/Scripts/M2FlowController.cs`
- `Assets/Scripts/M2ProbeDrag.cs`
- `Assets/Scripts/M2RulerDrag.cs`
- `Assets/Scripts/M2WaveformGraphic.cs`
- 必要时新增一个 Editor-only QA/数字人公共 helper（参数化 Ensure，名称在复用评审后确定）；M2 不新增数字人 runtime 脚本

### 预计修改

- `Assets/Settings/Scenes/M2.unity`：仅由 M2Setup 生成/维护
- `Assets/Editor/M1QASetup.cs`：仅在提取公共 QA/数字人 helper 时定点修改
- `Assets/Scripts/M1ToolSelection.cs`：仅在独立 M2 验收后接 M1→M2 出口
- `ProjectSettings/EditorBuildSettings.asset`：仅在串联阶段定点修改

### 明确不修改

- M2 引导视频及 `M1IntroVideo`
- 用户提供的参考截图和侧面钢轨图
- M3/M4/M5/M6 场景与交互
- 正式钢轨、自制尺素材原文件
- M1 数字人视频原文件、`UI-LumaKey-DigitalHuman.mat` 调参与与本任务无关的 M1 视觉和数字人配置

## 12. 最终审核与提交门槛

- [ ] 用户批准 `design.md` 与本执行清单（含第三版静态线框三视口审核）。
- [ ] 里程碑一第三版静态线框经用户审核（第二版不作数）后进入里程碑二。
- [ ] 用户明确同意创建独立的“M2 实现”Trellis 任务；当前审计任务不承载代码实施。
- [ ] 新实现任务的 PRD 引用本任务的 `audit.md`、`design.md` 和 `implement.md`，并按复杂任务要求准备自身上下文清单。
- [ ] 仅在新实现任务通过规划审核并执行 `task.py start <m2-implementation-task>` 后进入实现。
- [ ] 每个里程碑完成时先运行最小相关检查，再进入下一阶段。
- [ ] 最后运行全量质量检查并更新审计/规范中的新增事实。
- [ ] 未经用户明确授权，不执行 `git commit` 或 `git push`。
