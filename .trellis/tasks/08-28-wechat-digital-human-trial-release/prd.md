# 微信小游戏数字人真机修复与体验版分包发布

## Goal

修复微信小游戏真机中 M1 引导人物颜色偏白、字幕位置错误和引导结束后常驻数字人缺失的问题；在不正式上线的前提下，形成可重复的资源分层、真机验收和体验版二维码交付流程，使后台已邀请的体验成员能够扫码完整体验 M1-M5。

## Background

- 2026-08-28 两张微信真机截图确认：M1 引导人物整体偏白；字幕位于人物左上方；引导结束后云朵台词存在，但常驻数字人画面为空。
- `M1IntroVideo.ApplyWebGlSubtitleLayout()` 当前显式使用左上锚点 `(0,1)`、位置 `(96,-180)`，与字幕错误位置完全一致，并非 `MobileCanvasAdapt` 漂移。
- `UI-LumaKey.shader` 的 `_VideoInputIsSRGB` 当前只影响键控亮度计算，未把 WebGL 视频的 sRGB 采样值转成线性输出；Linear 项目最终再次 gamma 编码，导致人物发白。
- CDN 上 `m1-intro.mp4` 与 `digital-human-idle.mp4` 均返回 HTTP 200，常驻数字人缺失不是文件未上传。当前 Presenter 在引导播放器禁用后立即创建第二个 URL `VideoPlayer`，且无 `errorReceived`、首帧超时或降级显示；这条路线与父任务已批准的“小游戏常驻数字人使用透明序列帧替代 VideoPlayer”决定冲突。
- 最新导出按 `project.config.json` 忽略规则计算：主包约 0.98MB，`wasmcode` 分包约 10.17MB；微信代码分包结构已生效，主包满足 4MB 限制。
- 最新 `game.js` 记录 `webgl.data=87,591,052 bytes`，仍超过 40MB 门槛。BuildReport 中 `sarasa-gothic-sc-regular_cn.asset` 约 48.5MB、源 TTF 约 21.5MB，且 TMP Examples & Extras Resources 仍进入构建，是首启数据的主要可压缩项。
- `Temp/wx-export.log` 证明实际 `DeepSeekConfig.asset` 进入 WebGL 构建；客户端仍读取 Key 并设置 DeepSeek Bearer Header，属于体验版上传前 P0 阻断。
- SDK `assetLoadType=0` 表示首包资源走 CDN。SDK 源码仅在 wasm 与 data 合计约 29MB 内允许 data 放小游戏分包；当前 136MB data 不应切为包内加载。

## Requirements

### R1 引导人物颜色

- WebGL/微信端按正确颜色空间显示视频人物，肤色、帽子和工装不得整体发白。
- 黑底抠像与绿色引导线移除继续有效；Android/Editor 默认材质表现不得回归。

### R2 引导布局

- 微信端字幕位于人物正下方、水平居中，不与人物脚部重叠，也不落入微信右上胶囊或底部手势区。
- 允许仅在微信端把引导人物缩小到约 0.68-0.70；具体值以 16:9 与当前真机宽屏截图验收为准。
- 三段字幕均须完整单行显示，不得换行、裁切或越界；长句允许在 24-30px 范围自动缩小字号。

### R3 常驻数字人

- Android/Editor 保留现有 VideoPlayer + RenderTexture + LumaKey 路线。
- 微信端常驻数字人不得依赖第二个 VideoPlayer；按父任务 D2 使用透明序列帧或图集播放器，保持待机、思考、讲解三态及长按问答联动。
- M1 引导结束后常驻数字人必须自动出现；M2-M5 运行时 Bootstrap 装配后也必须显示。
- 冻结的 M2/M3 Scene 不得修改或保存覆盖。

### R4 包体与启动资源

- 保持现有微信主包 + `wasmcode` 代码分包，不把当前 136MB data 强行塞入 `data-package`。
- 先做构建报告归因和低风险瘦身：小游戏专用中文字库、排除 TMP 示例 Resources、剥离视频与调试符号；不得删除 Android 需要的资源。
- 首启远程 data 目标不高于 40MB；若仅靠瘦身仍超过目标，再引入远程模块资源组。
- 模块资源按 `core/M1`、`M2`、`M3`、`M4`、`M5`、`digital-human` 分组；进入当前模块时预取下一模块，失败可重试并显示进度。

### R5 体验版交付

- 修复后完成 Android 与 iPhone 微信真机回归，至少跑通 M1-M5、引导、常驻数字人、拖拽和一轮问答。
- DeepSeek 不得由小游戏前端直连；体验版广泛分发前接入 CloudBase 云函数代理或明确暂时禁用问答入口。
- 使用当前 AppID 上传开发版本，在微信小游戏后台设为体验版；后台已邀请且启用的体验成员可扫描体验版二维码进入，无需正式提审或上线。
- 记录版本号、上传说明、CDN 资源版本、二维码生成时间和真机测试结果，便于回滚。

## Acceptance Criteria

- [ ] 同一引导关键帧与源视频/Android 对比，无可见整体泛白；黑底和绿色分隔线均不可见。
- [ ] 16:9 与当前真机宽屏下，三段字幕均在人物正下方单行居中，人物与字幕之间有可见间距且无换行/重叠/裁切。
- [ ] 引导结束后 3 秒内常驻数字人出现并循环待机；问答状态可切换思考/讲解；返回待机正常。
- [ ] M1-M5 的常驻数字人均可显示，M2/M3 场景文件字节哈希不变。
- [ ] 微信开发者工具上传统计中主包小于 4MB；现有 `wasmcode` 分包保留；首启远程 data 不高于 40MB，或有经老板确认的实测网络例外。
- [ ] Android + iPhone 各至少一台完整跑通 M1-M5，控制台无视频、纹理、内存或远程资源致命错误。
- [ ] 微信后台已设置体验版，所有已邀请且状态有效的体验成员均可通过体验版二维码进入；未执行正式提审和发布上线。

## Child Task Map

- `08-28-wechat-ai-security-boundary`：P0，移除客户端密钥与直连，建立 CloudBase 代理或安全禁用。
- `08-28-wechat-package-slimming`：P0，字体/Resources/发布配置瘦身，data 降至不高于 40MB。
- `08-28-wechat-digital-human-polish`：修复 Shader tint、字幕几何、稀疏图集页和切态内存，并回归 Android。
- `08-28-wechat-trial-device-acceptance`：依赖前三项，执行跨平台真机、CDN、体验版和二维码验收。

父任务只负责跨子任务门禁和最终集成，不再直接实现上述修复。前三项可并行，体验版验收必须最后执行。

## Constraints

- 工作区已有 25 项未提交改动，其中包含本问题的试验性视频 URL、Shader 和 StreamingAssets 迁移；实现前必须逐项审查，禁止直接覆盖或回退用户改动。
- 新增 runtime 脚本默认不超过 150 行；优先用 Editor 生成工具、构建处理器和配置资产。
- M2/M3 Scene 冻结；所有小游戏适配使用运行时绑定或构建期内存副本处理。
- 云端 Key 只能放 CloudBase 环境变量，不进入 Unity、小游戏包或公开日志。

## Out Of Scope

- 正式版提审、备案、类目资质和公开上线。
- 改变 M1-M5 教学流程、玩法或冻结 Scene 视觉。
- 为未被邀请的微信用户开放体验版访问。
