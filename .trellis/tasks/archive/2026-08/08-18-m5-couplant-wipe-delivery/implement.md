# Implement：M5 = 复用 M2 UGUI 骨架 + 单步擦拭交互

## 0. 基线保护（先行）

- 记录 `M2.unity` SHA-256（实施前后对比）。
- 当前分支 `feature/m5`（老板指定），工作区仅含临时目录与任务记录。

## 1. 素材并入

1. `git checkout feature/m4-rail-web -- Assets/probeFootage/rag.png Assets/probeFootage/rag.png.meta`（擦拭布 + meta，guid `1f261a28...`）。
2. 确认 rag.png meta：maxTextureSize 大图不降采样、textureCompression=0（如 m4 分支 meta 未配置则按项目约定补）。

## 2. 写 runtime 脚本（Assets/Scripts/，namespace M5）

1. `M5CouplantFx.cs`（~70 行）：切 Sprite + 铺满 + `SetWipeProgress`（fillOrigin=1, fillAmount=1-p）+ Reset。
2. `M5RagDrag.cs`（~130 行）：Home/Wiping 两态拖拽（参照 M2RulerDrag），进度 = 擦拭布 x 在钢轨顶面区间映射，`flow.NotifyWipeProgress`。
3. `M5FlowController.cs`（~120 行）：Stage { Wipe, Completed }；初始铺满；进度回调 → 耦合剂递减；100% 锁定 + 完成；Reset；普通/透视切换；完成文案"M5 擦拭耦合剂完成"。

## 3. 写 Editor 工具（Assets/Editor/）

1. `M5Setup.cs`：生成 M5.unity（参照 M1Setup 幂等模式 + ugui-module-template 视觉令牌）；层级：SafeArea/Background/HeaderBar/MainScene/RailArea/ToolShelf(+RagHome)/RailViewport(RailBackground/CouplantOverlay/CouplantMask/RailPerspective)/PerspectiveBar_C/ControlDock_D(InstructionArea+StepProgress)/QALayer/DigitalHumanStage/ModalLayer；素材注入 + 脚本挂载 + 序列化字段。
2. `M5RuntimeSmoke.cs`：Play 断言（初始铺满→拖动递减→100% 锁定+完成→Reset 恢复→视图切换）。
3. `M5Shot.cs`：三视口截图 + 像素差异断言（参照 M2Shot/M3Shot）。

## 4. 生成与验证

1. Unity batch 编译（无报错）。
2. 跑 `M5Setup` 生成 M5.unity；连跑两次验证幂等（SHA 一致）。
3. Play Mode `M5RuntimeSmoke` 全 PASS。
4. `M5Shot` 三视口 PNG 非空且非纯色。
5. `sha256sum Assets/Settings/Scenes/M2.unity` 复验 == 基线；M2 脚本/Editor git status 零改动。

## 5. 文档与收尾

1. 更新 `.trellis/spec/unity/low-code.md`（M5 合同段：擦拭耦合剂交互、fill 反转、结束模块出口）与 `ugui-module-template.md`（M5 采用 M2 骨架确认）。
2. 同步 AGENTS.md 摘要。
3. 老板目视确认后提交（Phase 3.4）并归档任务。

## 验收命令

- `sha256sum Assets/Settings/Scenes/M2.unity`（前后一致）
- `git status --short`（M2 三套文件零改动；rag.png 已并入）
- Unity batch：M5Setup 幂等 + M5RuntimeSmoke + M5Shot（项目现有 Unity 批处理方式）
