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

- **结构改动**（增删元素、布局参数、锚点/尺寸）→ 改 `Assets/Editor/*Setup.cs` 后重新生成场景。
- **纯视觉微调**（单个对象的颜色、文字、字号）→ 可直接改场景文件，不动 Setup。
- 禁止两种方式混合导致 Setup 与场景漂移；改动后建议跑一次 Setup 验证幂等。

## 5.1 路径契约（防静默失效）

- Setup 写入运行时组件的路径字段必须与生成的真实层级**逐层一致**，不得包含虚构中间层；运行时组件按路径查找失败的默认行为是**报错而非静默跳过**。
- 教训：`M1QAPanel` 路径曾含虚构 "Panel" 层（`QAPanel/Panel/Header/...`），导致关闭/语音/发送按钮与输入框静默失效、发送按钮永久置灰，排查成本高（2026-08-07 归档）。

## 6. 目录与模块约定

- `Assets/Scripts/` — runtime 脚本（薄、通用、配置驱动）。
- `Assets/Editor/` — 搭建/生成工具（幂等）。
- `Assets/交互动画素材/` — 美术素材；`Assets/Settings/Scenes/` — 场景；`文档/` — 需求文档（技术规格书、功能文档、DeepSeek 接入方案）。
- M1 模块结构：QAPanel（问答抽屉）、ToolSelection（工具卡片）、PressDetector（按压检测）；后续模块复用其通用部分。

## 7. 禁止事项

- 引入 Bolt / Visual Scripting 等可视化脚本包。
- 复制粘贴式扩展（同一逻辑复制到多模块）。
- 无理由新增专用脚本、硬编码数据、主动重构存量代码（除非任务涉及）。
- 场景结构与 Setup 脚本逻辑漂移。

## 7.1 Unity 6 已知坑（2026-08-07 M1 面板实战）

- **运行时 `Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd")` 会报错**：该内置 UI 切片图在 Unity 6 运行时加载不到（Editor 的 `AssetDatabase.GetBuiltinExtraResource` 正常）。运行时动态创建气泡等 UI 时改用程序化生成（`Sprite.Create` 自绘圆角 + border 九宫格）或项目内 Sprite 资产。
- **动态尺寸 UI 不要依赖嵌套布局组 preferred 缓存**：逐字/动态生长场景（气泡随文本增长）中，`HorizontalLayoutGroup` 的行高按子物体 preferred 推算且带缓存，尺寸变化时行高跟不上会重叠/错位。改用显式控制：行挂 `LayoutElement`（minHeight/preferredHeight 同值手动同步），气泡手动锚定定位，逐字更新后立即 `LayoutRebuilder.ForceRebuildLayoutImmediate`。
- **`TextAnchor` 枚举无 `Top/Bottom`**：Unity 命名体系为 `Upper/Middle/Lower`（如 `UpperLeft`、`UpperRight`）。
- **UI 场景音效必须强制 2D（`spatialBlend = 0`）**：AudioSource 挂在 UI 画板/普通场景物体上时，默认 3D 音效会随与 Main Camera 的距离衰减，画板远离相机则完全听不见。接入点播音效时在运行时获取 AudioSource 后立即设 `spatialBlend = 0f`（运行时兜底优于 Setup 创建时设置——Setup 只在新建时生效，用户手动挂的 AudioSource 覆盖不到）。

## 8. 音效接入约定（M1 起）

- 素材放 `Assets/Audio/`（按 E-xx 用途分目录，附选择说明 txt）；素材由 **Editor Setup 注入**，运行时脚本只暴露 `AudioClip` 字段，禁止硬编码路径（运行时无法按 Assets 路径加载，除非走 Resources/Addressables）。
- Setup 注入采用「仅当字段为空时赋值」（`if (comp.clip == null) comp.clip = LoadClip(...)`），幂等且不覆盖用户手动替换的素材；`LoadClip` 失败打 `Debug.LogWarning` 返回 null 不中断 Setup。
- 播放统一用 `AudioSource.PlayOneShot(clip)`：互不打断、适合短音效；未配置素材或 AudioSource 缺失时**静默跳过不报错**（`if (clip == null || src == null) return;`）。
- 场景音频出口：AudioSource 由 Setup Ensure 到交互物体上（`GetComponent ?? AddComponent`），`playOnAwake` 默认 false，不产生开机噪音。

## 9. 与 AGENTS.md 的同步契约

- 本文档为权威来源；`AGENTS.md` 总纲只存放摘要（项目速览、五条规则、约定速查）。
- 修改本文档后必须同步更新总纲对应条目；总纲不新增本文档没有的规则。
