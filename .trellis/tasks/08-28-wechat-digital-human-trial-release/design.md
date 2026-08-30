# Design: 微信小游戏数字人真机修复与体验版分包发布

## Scope And Boundaries

本任务分三层推进：先修复当前真机可见缺陷，再降低首启资源和设计模块资源分层，最后上传体验版二维码。Android 保留现有视频链路；微信小游戏使用平台隔离实现。M2/M3 Scene 不产生任何序列化改动。

## Root Cause And Fix Design

### 1. 引导人物颜色偏白

WebGL 视频纹理在当前真机路径中提供 sRGB 数值，而项目工作在 Linear 色彩空间。现有 `_VideoInputIsSRGB=1` 只让 LumaKey 直接用采样值计算 alpha，最终 RGB 仍作为线性值输出并被显示端再次 gamma 编码，所以整体提亮。

Shader 应分离两个空间：

- `keySrgb`：用于亮度键控和绿线判断。
- `displayLinear`：用于最终 RGB 输出；Linear 项目且输入为 sRGB 时调用 `GammaToLinearSpace`。
- `_VideoInputIsSRGB=0` 的 Editor/Android 默认路径保持现状。

测试不能只看背景透明，还要用同一关键帧采样帽子、肤色、工装三个区域，与源视频误差对比。

### 2. 字幕位于左上

`ApplyWebGlSubtitleLayout()` 当前就是左上布局，直接替换为微信专用底部居中合同：人物缩放约 0.68-0.70，字幕锚定遮罩底部中心，字幕上缘低于人物可见底边并保留间距。布局参数进入 Inspector 字段或共享配置，M1Setup 同步默认值；运行时不修改冻结 Scene。

### 3. 常驻数字人缺失

CDN 文件已可访问。当前实现把引导和常驻数字人都交给 URL `VideoPlayer`，引导结束后立即启动第二播放器，且 Presenter 没有错误/首帧超时/降级链路。即使增加延迟重试，也仍依赖微信单视频解码能力，不适合作为稳定交付方案。

采用父任务已确定的序列帧方案：

- 引导：继续使用已在真机出画面的单个视频播放器，修复颜色和布局。
- 常驻数字人：Editor 工具从带 Alpha 的源动画生成降采样透明帧图集和清单；微信端用轻量帧播放器驱动 `RawImage`/`Image`。
- 实测三个源视频均为 30fps：待机 4.17s/125 帧、思考 6.13s/184 帧、讲解 8.17s/245 帧，共 554 帧。常驻数字人实际显示宽约 223px，微信端推荐降为 10fps、240-256px 宽：待机约 42 帧、思考约 62 帧、讲解约 82 帧，共约 186 帧；12fps 高质量档为 51/74/99，共 224 帧。
- 10fps、240-256px 透明 PNG/压缩图集预计下载体积约 4-8MB，相比当前三个 MP4 合计约 20.7MB 节省约 60%-80%；但会大于现有三个 Alpha WebM 合计约 2.3MB。WebM 虽最小，却不能作为微信常驻透明视频的可靠运行时方案。
- 帧图解码后的内存高于磁盘体积：10fps、240px 宽时三态全驻留约 55-60MB，单态约 13-26MB；必须一次只保留当前状态纹理，切换后释放非必要页，控制 iPhone 内存。
- `M1DigitalHumanPresenter` 保留三态和问答状态机，只抽象“播放某状态”的后端；Android 使用 VideoPlayer 后端，微信使用帧图集后端。
- `M3DigitalHumanBootstrap` 继续运行时装配，不保存 M2/M3。

### 4. 体验版回归：重置弹窗、会话重开与 Android 引导

- `ModuleResetDialogStyle` 在 M2-M5 场景加载后运行时覆盖标题、按钮位置和背景线框；四个 `FlowController.ResetAll()` 保持独立，M2/M3 Scene 不写回。
- `ExperienceReplayOnResume` 是跨平台会话生命周期边界。M5 完成只记录当前进程内状态；完成后发生 hide→show / pause→resume 才清首次引导标记并加载 M1。进程被系统杀死时 Unity 本来就从 M1 启动，未完成流程不触发重开。
- 微信引导采用 `videos` 分包本地文件→CDN→海报字幕三级链。分包加载后 JS 把 MP4 复制到 `wx.env.USER_DATA_PATH` 并传真实路径；分包 Prepare 无回调、报错或首帧超时均切 CDN，CDN Prepare/首帧失败进入定时海报，任何失败路径最终关闭遮罩并恢复时间。
- WebGL H.264 输入不信任解码纹理 Alpha，`UI/LumaKey` 只用亮度键控生成人物 Alpha；海报自带 `AspectRatioFitter`，视频首帧前也有稳定人物占位。
- `WxVideoSubpackageInstaller` 必须同时生成 `videos/m1-intro-wx.mp4` 与分包入口 `videos/game.js`，再写入 `game.json`。

## Package Architecture

### Current WeChat Packages

保持 SDK 已生成结构：

- 主包：约 1.21MB，负责 JS 胶水、加载页和最小启动逻辑。
- `wasmcode` 分包：约 10.14MB，已并行预载。
- `data-package`：保持 CDN 模式。SDK 源码会在 wasm + data 超过约 29MB 时拒绝包内加载，当前 data 约 136MB，不能改成小游戏包内。

### Phase A: Base Data Slimming

先做最小风险瘦身，再决定是否引入模块远程场景：

1. 生成小游戏专用中文 TMP 字体资产，覆盖项目静态台词和常用汉字；构建期在场景内存副本替换，不写回 M2/M3。
2. 排除 `TextMesh Pro/Examples & Extras/Resources` 等示例资源。
3. WebGL 构建剥离所有 VideoClip、本地视频和调试 symbols；保留 Android 资源。
4. 对仅小尺寸显示的头像、工具图设置 WebGL 平台覆盖并核对清晰度。
5. 生成 BuildReport 资产归因表，以压缩后 data <=40MB 为第一阶段门槛。

SDK 自带 Loading 页发生在 Unity `callmain` 前，应先承担首启进度显示。仅新增 Unity Boot Scene 并不能减少单体 data 下载；Boot 只在后续远程模块资源启用后承担模块预取。

### Phase B: Remote Module Groups

若 Phase A 仍超过 40MB，或首启真机时间不可接受，再启用远程模块资源：

- `core-m1`：M1 和共享 UI/字体/音频，首启必需。
- `module-m2`、`module-m3`、`module-m4`、`module-m5`：每个模块 Scene 与专属纹理/音频。
- `digital-human`：待机常驻优先；思考/讲解按首次问答预取。

优先采用 Addressables 远程组或经 SDK 验证的 AssetBundle/StreamingAssets 缓存，不把大资源放微信代码分包。增加统一 `ModuleSceneLoader`，Android 仍走 `SceneManager.LoadScene`，微信端走远程场景；M1-M4 只改统一出口调用。进入 M1 时后台预取 M2，进入每个模块后预取下一模块，失败时保留当前页面并允许重试。

## Experience Release Flow

1. 修复与包体门槛通过后导出 `minigame` 与 `webgl`。
2. 先上传带版本目录/哈希的 CloudBase 静态资源，再更新并上传小游戏代码，避免新代码引用旧 CDN。
3. 微信开发者工具用当前 AppID 上传开发版本，版本号和说明记录到任务结果。
4. 微信小游戏后台在“版本管理”把该开发版本设为体验版。
5. 在成员管理确认体验成员状态有效，生成体验版二维码；只有受邀体验成员可扫码进入。
6. Android/iPhone 各做一次冷启动、二次缓存启动和 M1-M5 全流程测试。

正式提审和上线不属于本任务。体验版同样应配置合法域名、CloudBase 权限和云函数环境变量。

## AI Boundary

小游戏前端不可直连 DeepSeek。广泛发放二维码前，优先完成 CloudBase `deepseek-proxy`；如果代理未就绪，则隐藏/禁用问答入口并明确标注该体验构建范围，不能保留一个必然失败的入口。

## Execution Ownership

本任务作为集成父任务保留源需求和最终 Acceptance Criteria；直接修复分别由四个子任务拥有：AI 安全、包体瘦身、数字人视觉/内存、体验版跨平台验收。前三个子任务可并行规划和实现；体验版任务必须在两个 P0 与数字人任务检查通过后启动。任何 P0 未通过时禁止上传二维码。

## Rollback

- Shader 修复由 `_VideoInputIsSRGB` 开关隔离，关闭即可恢复原生路径。
- 帧播放器仅微信端启用，Android 视频素材和引用不删除。
- 字体替换在构建期内存副本执行，场景资产不写回。
- CDN 资源按版本目录发布，后台可重新指定上一开发版本为体验版。
