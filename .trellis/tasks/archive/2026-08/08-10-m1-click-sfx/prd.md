# M1 点击音效接口（正确 / 错误 / 通过）

## 目标

为 M1-1 工具选择交互接入点击音效：玩家点击工具时按结果播放**正确 / 错误**提示音，点击"点击继续"时播放**通过**音效。保持低代码约定：不改架构、不加新脚本，素材由 Inspector 字段配置。

## 已确认事实（来自代码调研）

- **正确判定**：`Assets/Scripts/M1ToolSelection.cs` → `OnToolClicked()` 的 `if (toolName == correctToolName)` 分支（约 107~121 行）。
- **错误判定**：同一方法的 `else` 分支（约 122~125 行）。
- **通过判定**：`_continueButton.onClick` 注册的回调（`M1ToolSelection.cs` 第 105 行；场景"点击继续"按钮在 `Assets/Editor/M1Setup.cs` 第 146 行另有持久化监听，当前均为占位 Debug.Log）。
- 项目现有 runtime 脚本**零音效代码**：无 AudioSource / AudioClip / 播放逻辑。
- 场景已有 `AudioListener`（Main Camera 自带），**无任何 AudioSource**。
- 音效素材已导入 Unity（带 .meta）但**全项目零引用**：
  - 正确 → `Assets/Audio/E-01 正确提示音/正确音1.mp3`、`正确音2.mp3`（附 txt：从二者中选择合适音效）
  - 错误 → `Assets/Audio/E-02 错误提示音/错误提示音.mp3`
  - 通过 → `Assets/Audio/E-04 通关音效/通关音效1.mp3`、`通关音效2.mp3`（附 txt：从二者中选择合适音效）

## 需求

1. 正确音：选对工具时播放（E-01 二选一）。
2. 错误音：选错工具时播放（E-02）。
3. 通过音：点击"点击继续"时播放（E-04 二选一）。
4. 三处回调收敛在 `M1ToolSelection` 一个脚本内，无需新增脚本。

## 验收标准

- [ ] 选对工具 → 播放正确提示音（且只播一次）。
- [ ] 选错工具 → 播放错误提示音。
- [ ] 点击"点击继续" → 播放通关音效。
- [ ] 选对后锁定按钮期间，再次点击不重复播放任何音效。
- [ ] 三个 AudioClip 均为 Inspector 可配置字段（低代码：不硬编码素材路径）。
- [ ] 未配置某素材时该处静默跳过，不报错、不影响其他功能。
- [ ] 未选择素材的默认实现可开箱即用（Setup 或脚本默认值引用素材目录文件）。

## 范围外

- 拖拽音效（E-05 拖拽音效.mp3）、蜂鸣报警音（E-03）：M1 当前无对应交互，不接入。
- 引导视频音频、AI 问答面板按钮音效：不在本次范围。
- M1-2 及后续模块：不在本次范围。

## 素材选定（用户已确认）

- 正确音 → `Assets/Audio/E-01 正确提示音/正确音2.mp3`
- 错误音 → `Assets/Audio/E-02 错误提示音/错误提示音.mp3`
- 通过音 → `Assets/Audio/E-04 通关音效/通关音效1.mp3`

## 技术决策

- AudioSource 挂到画板（与 M1ToolSelection 同物体），由 `Assets/Editor/M1Setup.cs` Ensure 创建；脚本用序列化字段或 GetComponent 引用，`PlayOneShot` 播放，互不打断。
- 素材赋值走 Setup（遵循「场景结构改动走 Setup 脚本」约定）：`Assets/Editor/M1Setup.cs` 在配置 M1ToolSelection 时，若对应 AudioClip 字段为空则用 `AssetDatabase.LoadAssetAtPath` 从素材目录加载赋值（幂等 + 不覆盖用户手动替换）。
- 运行时脚本不硬编码路径、不依赖 Resources；未配置素材时播放处直接跳过。
