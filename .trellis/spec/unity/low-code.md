# Unity 低代码开发规范（权威版）

> 本规范适用于本项目所有 AI 编码任务（runtime 与 Editor 均适用，另有说明除外）。
> `AGENTS.md` 总纲是本规范的摘要，两者冲突时以本文档为准；修改规范必须先改本文档，再同步总纲。

---

## 1. 低代码决策树

任何新功能按以下顺序决策，命中即停：

1. **能否纯配置实现？**（Inspector 字段、场景对象、现有组件组合）→ 写配置，不写代码。
2. **能否复用现有组件？**（`Assets/Scripts/` 现有脚本或其他模块组件）→ 复用，必要时加配置项。
3. **能否用 Editor 工具实现？**（重复性搭建/生成逻辑）→ 写或改 `Assets/Editor/` 工具脚本。
4. **以上皆否** → 新增 runtime 脚本，并在方案/说明中写明理由。

## 2. Runtime 脚本规范

- 新增 runtime 脚本默认 **≤150 行**；超限先拆分职责或改为配置驱动，仍超限须说明理由。
- 命名：`M{模块}{职责}`，如 `M1QAPanel`、`M1ToolSelection`、`M1PressDetector`。
- 只写配置表达不了的核心逻辑；UI 布局、数据、文案一律不进代码。
- 不主动重构存量代码（存量超标如 M1QAPanel.cs 384 行属于历史遗留）；仅当任务本身涉及该文件时顺手精简。

## 3. 配置化规范

- 数据/参数进 ScriptableObject、Inspector 字段或场景配置，禁止硬编码魔数、文案、尺寸。
- 配置项必须有合理默认值，保证组件拖入场景即可用。
- 批量数据（如题目、工具列表）优先 ScriptableObject 资产。

## 4. Editor 工具规范

- Editor 工具（`Assets/Editor/`）**豁免 150 行上限**——它是低代码的放大器，一次生成省掉大量手工配置；但仍须精简、复用、不硬编码。
- **幂等要求**：Setup 类工具重复执行不得产生重复对象（参考 `M1Setup.cs` / `M1QASetup.cs` 现状）。
- 生成对象的命名可预测，方便后续查找与验证。
- 修改 Setup 工具时需说明重新生成对现有场景的影响。

## 5. 场景改动规则

- 未冻结模块的**结构改动**（增删元素、布局参数、锚点/尺寸）→ 改 `Assets/Editor/*Setup.cs` 后重新生成场景。
- 未冻结模块的**纯视觉微调**（单个对象的颜色、文字、字号）→ 可直接改场景文件，不动 Setup。
- 禁止两种方式混合导致 Setup 与场景漂移；改动后建议跑一次 Setup 验证幂等。

### 5.0 M2/M3 场景冻结例外（2026-08-12 产品决定）

- `Assets/Settings/Scenes/M2.unity` 与 `Assets/Settings/Scenes/M3.unity` 的当前 Scene 文件是冻结后的视觉权威。后续程序、Setup 和 Agent 不得修改、重生成或保存覆盖；只有老板在 Unity Scene 中手工修改才允许改变视觉。
- `M2Setup` / `M3Setup` 只能打开或检测现有 Scene 并明确日志后返回；Scene 不存在时必须报错，二者均不得创建。历史生成代码只能保留为不可调用的参考实现，不得重新接回入口。
- 旧的“结构改动走 Setup”“Ensure 自愈已有节点”“双跑保存验证幂等”规则不适用于这两份冻结 Scene。调用 M2/M3 Setup 的验收标准是调用前后文件字节哈希完全一致。
- 后续 M2/M3 功能只能通过 runtime 动态绑定、复用已有节点或添加不改变视觉的组件完成。禁止 Setup 重写 `RectTransform`、`Graphic`/TMP、文案、颜色、Sprite、active 状态或 sibling 顺序，也禁止借功能修改绕过冻结。
- 若需求必须改变 M2/M3 视觉、层级或序列化视觉状态，应停止实现并交由老板手工修改 Scene；不得修改 Scene YAML 或恢复旧生成入口。

## 5.1 路径契约（防静默失效）

- Setup 写入运行时组件的路径字段必须与生成的真实层级**逐层一致**，不得包含虚构中间层；运行时组件按路径查找失败的默认行为是**报错而非静默跳过**。
- 教训：`M1QAPanel` 路径曾含虚构 "Panel" 层（`QAPanel/Panel/Header/...`），导致关闭/语音/发送按钮与输入框静默失效、发送按钮永久置灰，排查成本高（2026-08-07 归档）。
- 教训：用户改名节点（`物品 → M1物品`）后，`M1ToolSelection.toolsRootPath` 与 `M1Setup` 素材注入路径同步失效，运行时 LogError 跳过全部工具绑定（2026-08-10）。改名/移动节点必须同步 Setup 与运行时默认值；未冻结 Scene 跑一次 Setup 自愈，M2/M3 冻结 Scene 则只允许老板手工修正 Scene。

## 5.2 场景 YAML 手改陷阱（块头丢失）

- 用正则重组 Unity YAML 块时必须**保留 `--- !u!1 &xxx` 块头行**：`re.search(r'--- !u!1 &%s\n(.*?)...')` 中块头在匹配范围内，重组 `text[:m.start()] + block + text[m.end():]` 会丢掉块头，产生孤立块体行（`GameObject:` 前没有 `--- !u!1 &xxx`），Unity 打开报 YAML 错误。
- 正确做法：重组时补回 `m.group(1)`（块头），或只替换块内字段（如 `m_IsActive`）不重组整块。
- 手改后必验：块头数与块体数配对（`grep -c '^--- !u!1 &'`）；孤立块体行（前一行不是 `--- !u!` 的 `GameObject:`/`RectTransform:` 等）为 0。
- 教训：2026-08-10 M1-2 场景手改连续丢两次块头（M1物品/M2物品/画板），均靠此校验兜住。

### 5.3 UGUI 运行时事件绑定（2026-08-11 M2）

- `Button.onClick.AddListener`、`Slider.onValueChanged.AddListener` 和普通 C# event 在 Editor Setup 中注册的委托只存在于当前编辑器进程，**不会**写入场景的 `m_PersistentCalls`；重新打开场景后会丢失。
- 未冻结 Scene 的 Setup 只负责序列化对象引用、默认参数和层级；M2/M3 冻结 Scene 不再接受 Setup 写入。每个 runtime 组件必须在 `Awake` / `OnEnable` 由事件所有者执行幂等绑定：先 `RemoveListener` / `-=`，再 `AddListener` / `+=`。例如 Flow 绑定流程按钮，Probe 绑定角度滑块与距离事件，Ruler 绑定对齐事件，IdleHelp 绑定帮助按钮。
- 验证必须包含“保存并重新打开场景后再进入 Play Mode”；`m_Calls: []` 对纯运行时绑定是预期结果，不能将其当作失效证据。

## 6. 目录与模块约定

- `Assets/Scripts/` — runtime 脚本（薄、通用、配置驱动）。
- `Assets/Editor/` — 搭建/生成工具（幂等）。
- `Assets/交互动画素材/` — 美术素材；`Assets/Settings/Scenes/` — 场景；`文档/` — 需求文档（技术规格书、功能文档、DeepSeek 接入方案）。
- M1 模块结构：QAPanel（问答抽屉）、ToolSelection（工具卡片）、PressDetector（按压检测）；后续模块复用其通用部分。
- 素材组织（2026-08-10 整理后）：探伤工具图 `Assets/InspectionToolMaterials/`（PNG）；探头图 `Assets/probeFootage/探头素材（有白边版）/` 与 `（无白边版）/`（M1-2 探头用**有白边版**）；音频 `Assets/Audio/E-xx 用途/`；数字人 `Assets/DigitalHuman/A-xx/`。旧目录 `01 探伤工具素材`、`02 探头素材` 已删除。素材迁移后必须同步 Setup 注入路径，否则 `LoadAssetAtPath` 返回 null 只打 Warning、场景空白但流程继续。

## 6.1 M1-2 阶段切换与防卡死模式（2026-08-10）

- M1-1/M1-2 由单一组件 `M1ToolSelection` 承载（同一"探测仪器选择模块"，不拆脚本）：
  - M1-1：6 个工具按 `ToolNames` 绑定，`correctToolName` 判定；选对显示"点击继续"。
  - `OnContinueClicked`：隐藏 `m1ItemsPath` 容器、显示 `m2ItemsPath` 容器、AI 回答切 `textM2Initial`、启动 M1-2 防卡死；`_phase2` 标志防重复进入。
  - M1-2：`probeNames` 列表按物体名绑定到 `m2ItemsPath` 容器，`correctProbeName` 判定；选对抖动+音效+锁定+显示"开始探测"。
- 防卡死（规格书硬性要求）：`toolIdleTimeout` / `probeIdleTimeout` 秒无操作 → `PulseHighlight` 金色脉动高亮正确项 → 自动选对并推进（M1-1 还自动进入 M1-2）；0=关闭。高亮与手动判定共用 `correctToolName/correctProbeName` 字段，保证行为一致。
- 阶段容器激活由运行时兜底（Awake 强制 M1-1 可见、M1-2 隐藏）+ Setup 幂等设置，两端一致不漂移。

## 7. 禁止事项

- 引入 Bolt / Visual Scripting 等可视化脚本包。
- 复制粘贴式扩展（同一逻辑复制到多模块）。
- 无理由新增专用脚本、硬编码数据、主动重构存量代码（除非任务涉及）。
- 未冻结场景的结构与 Setup 脚本逻辑漂移；或用 Setup/Agent 覆盖已冻结的 M2/M3 Scene。

## 7.1 Unity 6 已知坑（2026-08-07 M1 面板实战）

- **运行时 `Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd")` 会报错**：该内置 UI 切片图在 Unity 6 运行时加载不到（Editor 的 `AssetDatabase.GetBuiltinExtraResource` 正常）。运行时动态创建气泡等 UI 时改用程序化生成（`Sprite.Create` 自绘圆角 + border 九宫格）或项目内 Sprite 资产。
- **动态尺寸 UI 不要依赖嵌套布局组 preferred 缓存**：逐字/动态生长场景（气泡随文本增长）中，`HorizontalLayoutGroup` 的行高按子物体 preferred 推算且带缓存，尺寸变化时行高跟不上会重叠/错位。改用显式控制：行挂 `LayoutElement`（minHeight/preferredHeight 同值手动同步），气泡手动锚定定位，逐字更新后立即 `LayoutRebuilder.ForceRebuildLayoutImmediate`。
- **`TextAnchor` 枚举无 `Top/Bottom`**：Unity 命名体系为 `Upper/Middle/Lower`（如 `UpperLeft`、`UpperRight`）。
- **UI 场景音效必须强制 2D（`spatialBlend = 0`）**：AudioSource 挂在 UI 画板/普通场景物体上时，默认 3D 音效会随与 Main Camera 的距离衰减，画板远离相机则完全听不见。接入点播音效时在运行时获取 AudioSource 后立即设 `spatialBlend = 0f`（运行时兜底优于 Setup 创建时设置——Setup 只在新建时生效，用户手动挂的 AudioSource 覆盖不到）。
- **Vector2/Vector3 混合运算符重载歧义（Unity 6000）**：`(Vector2)vector3 - vector2` 在 Unity 实际编译下报 CS0034（Vector3/Vector2 混合运算符使重载解析歧义）；改用逐分量运算 `new Vector2(a.x - b.x, a.y - b.y)` 规避（2026-08-11 M2RulerDrag）。
- **离线编译 ≠ Unity 编译**：用外部 csc + `Managed/UnityEngine` impl dll 做离线编译可能漏报 Unity 实际编译错误（重载解析/引用集差异，实测 Vector2/Vector3 混合运算漏报）；编译验证一律以 Unity 编辑器/批处理为准（2026-08-11）。

## 8. 音效接入约定（M1 起）

- 素材放 `Assets/Audio/`（按 E-xx 用途分目录，附选择说明 txt）；素材由 **Editor Setup 注入**，运行时脚本只暴露 `AudioClip` 字段，禁止硬编码路径（运行时无法按 Assets 路径加载，除非走 Resources/Addressables）。
- Setup 注入采用「仅当字段为空时赋值」（`if (comp.clip == null) comp.clip = LoadClip(...)`），幂等且不覆盖用户手动替换的素材；`LoadClip` 失败打 `Debug.LogWarning` 返回 null 不中断 Setup。
- 播放统一用 `AudioSource.PlayOneShot(clip)`：互不打断、适合短音效；未配置素材或 AudioSource 缺失时**静默跳过不报错**（`if (clip == null || src == null) return;`）。
- 场景音频出口：AudioSource 由 Setup Ensure 到交互物体上（`GetComponent ?? AddComponent`），`playOnAwake` 默认 false，不产生开机噪音。

## 8.1 问答面板暂停契约（M1 起，M2/M3 复用）

- `M1QAPanel.pauseGameOnOpen`（默认 true）：面板 Open 时 `Time.timeScale = 0` 全局暂停（含模块计时/拖拽/动画），Close 时恢复**打开前**的值（`_timeScaleBefore` 记录，不硬编码 1，避免覆盖引导等场景的 timeScale 设置）。
- 暂停期间不受影响是设计前提：长按检测用 `Time.unscaledTime`、面板滑入/逐字用 `unscaledDeltaTime` / `WaitForSecondsRealtime`、DeepSeek 请求用 `UnityWebRequest`、数字人视频走 VideoPlayer（不受 timeScale 影响）。**新增问答链路组件必须遵循 unscaled 计时**，否则暂停时功能卡死。
- 引导期间（M1IntroVideo 全屏遮罩挡点击）QA 入口不可达，与引导的 timeScale 管理无并发冲突；若未来出现并发场景需先协调。

## 9. 与 AGENTS.md 的同步契约

- 本文档为权威来源；`AGENTS.md` 总纲只存放摘要（项目速览、五条规则、约定速查）。
- 修改本文档后必须同步更新总纲对应条目；总纲不新增本文档没有的规则。
