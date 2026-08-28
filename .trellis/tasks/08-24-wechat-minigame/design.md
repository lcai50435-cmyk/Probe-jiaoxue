# 微信小游戏转换 — 技术设计

## 总体架构

```
Unity 工程 (6000.3 直转优先)
 ├─ 平台隔离层：所有小游戏专属代码收进独立文件/宏分支，原生 APK 路线零感知
 ├─ Boot.unity（新增，Build index 0）：Logo + 进度条 + BootLoader
 │    └─ 拉远程资源清单 → 进度展示 → LoadScene("M1")
 ├─ 数字人：SequenceFramePlayer（UGUI Image 序列帧轮播）替代 VideoPlayer+LumaKey
 ├─ 引导视频：M1IntroVideo 内部播放后端抽象（Editor/APK=VideoPlayer；小游戏=WX.video）
 └─ AI 问答：M1DeepSeekClient 请求地址抽象 → 小游戏端走云函数，Key 在云端
微信转换 SDK (minigame-tuanjie-transform-sdk)
 └─ WebGL 构建 → 转换面板生成 /minigame（开发者工具打开）+ /webgl 资源目录（传 CloudBase 静态托管）
CloudBase 免费环境
 ├─ 静态托管：wasm/data/bundle/视频/PNG 序列帧（默认域名，免备案）
 └─ 云函数：deepseek-proxy（Node，转发 chat/completions，Key 存环境变量）
```

## 关键设计点

### 1. 引擎路线与回滚（D1）
- 冒烟顺序：装 SDK → WebGL 基线构建 → 转换 → 开发者工具运行。
- 任一步骤出现不可绕过的引擎级报错（如 wasm 生成失败、SDK API 不兼容）→ 切换预案：
  - `git checkout -b minigame-2022` 从安全点拉分支，用本机 2022.3.62f3 打开；
  - 冻结场景（M2/M3/M5 等）重序列化 diff 必须逐一审查，视觉异常即回滚该文件；
  - 2022 分支只服务小游戏出包，主线继续用 6000.3 出 APK。

### 2. 平台隔离约定（R7）
- 宏：统一使用 SDK 定义的编译宏（WEIXINMINIGAME / UNITY_WEBGL），分支逻辑只允许出现在：
  - 视频播放后端选择处（M1IntroVideo 内部或其策略类）
  - HTTP 客户端基地址注入处（M1DeepSeekClient 配置项）
  - BootLoader 本身（仅存在于 Boot 场景）
- 禁止在模块业务脚本（FlowController/Drag/Fx 等）里散落平台 if。
- 新增 runtime 脚本遵守 ≤150 行低代码约束；Boot 场景由 Editor Setup 工具幂等生成（`MinigameBootSetup.cs`，风格对齐 M1Setup）。

### 3. 序列帧数字人（D2/R3）
- 抽帧管线：**Editor 工具**（如 `DigitalHumanFrameExtractor.cs`）用 Editor 下 VideoPlayer 渲染 webm 到 RenderTexture 读回存 PNG——Unity 原生支持 WebM alpha，无需外部 ffmpeg。
  - 参数化：每态目标帧率（建议 12~15fps）、最大宽度（建议 ≤720px）、输出目录。
  - 输出 Sprite 图集或 PNG 序列 + 一个 `.asset` 清单（帧列表/fps/循环标记）。
- 运行时：`SequenceFramePlayer`（MonoBehaviour，≈100 行）：Sprite[] + fps + loop + Play/Pause/SetFrame；挂 FullBodyView/AvatarView 的 Image 上。
- 接入：M1 数字人（视频驱动）与 M3/M4/M5 Bootstrap 三态动画统一改为序列帧驱动；状态机接口保持不变，仅替换素材源。
- 体积预算：三态 × ~15fps 循环短动作压缩 PNG，单角色预计 5~15MB（远程加载，不进包）。

### 4. 引导视频适配（D3/R4）
- 抽象一个极薄播放后端接口（Play/Pause/Seek/进度事件），两实现：
  - `VideoPlayerBackend`：现有路径，Editor/APK 不变；
  - `WxVideoBackend`：调 SDK 的 WX.video 封装（全屏/浮层播放，播完回调返回 UI 流程）。
- LumaKey 悬空效果在小游戏端不可保留（原生层限制）；引导视频以"整段矩形播放"呈现，属已接受的产品妥协（PRD D3 记录）。

### 5. AI 云函数中转（D4/R5）
- 云函数 `deepseek-proxy`：POST body 透传 `{messages}` → 补 Key → 转发 `api.deepseek.com/chat/completions` → 回传 JSON；超时与错误码规范化。
- 前端：M1DeepSeekClient 增加 endpoint 注入点；小游戏构建下指向云函数，APK 构建维持现配置。
- 安全：Key 只存云函数环境变量；云函数开启频率限制防刷。

### 6. 首包与资源策略（D6/R2）
- Boot 场景资源预算：<0.5MB（一张 Logo + TMP 子集字体 + 进度条图）。
- wasm brotli 预计 2~3MB，压线风险高时启用 SDK「代码分包」把非启动逻辑拆出首包。
- 远程资源按需分组：Boot 必需（引擎+首场景数据）→ M1 素材 → M2~M5 素材（进对应模块前预取）。
- 微信本地缓存生效后二次进入不重复消耗 CDN 流量。

## 兼容性与迁移

- Input System 在 WebGL/小游戏的触摸桥接由 SDK 处理；风险点记录，真机验证拖拽交互（M2 探头/尺子、M5 擦拭布）。
- URP 2D / TMP / Shader（含 LumaKey 材质残留引用）需确认 WebGL 编译无报错；未用到的变体剔除以省体积。
- 音频量小（<1MB），随包或随首组资源即可。

## 运维与回滚

- 开工前 git 打 tag `pre-minigame`；改造全部在新分支 `minigame` 进行，合回主线前跑 APK 回归。
- CloudBase 环境、转换导出目录（默认 `Assets/WX-WASM-SDK` 与导出 `/minigame`、`/webgl`）加入 .gitignore 评审。
- 失败兜底链：6000.3 直转 → 2022.3 分支 → （最坏）暂停小游戏线，APK 路线不受任何影响。
