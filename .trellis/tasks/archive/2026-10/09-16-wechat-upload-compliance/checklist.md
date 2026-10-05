# 体验版/提审版上传前核对单

> 每次上传微信版本前逐项勾选，勾完归档到本任务 logs/（或任务会话记录）。任一项不过不传。

## A. 代码与场景基线

- [ ] Unity 编译无 error CS（Console 干净）
- [ ] M2/M3 冻结 Scene SHA256 与基线一致：M2 `896370bd…` / M3 `2565232c…`（若有变化，确认是老板手工改动并记录）
- [x] 问答口径（2026-09-21 老板定稿）：体验版与提审版**统一 PresetLibrary(1)，不再切换**；AiProxyConfig.asset 应为 qaMode: 1。如未来要恢复实时 AI，改回 0 并重新导出+传 CDN。
- [ ] 预设话术/文案有改动时：新字符已核对 `Assets/font/WeChatSubset/charset.txt`，缺失则重跑 Tools/WeChat/生成微信字体子集

## B. 导出与 CDN

- [ ] 微信导出成功（日志 `SUCCEED` / `Build Finished, Result: Success`）
- [ ] 主包 ≤ 4M、主包+分包 ≤ 30M（开发者工具体积统计）
- [ ] game.json 声明的每个分包根下存在 game.js（videos/wasmcode/data-package）
- [ ] 新 `webgl/<哈希>.webgl.data.unityweb.bin.txt` 已上传 CloudBase `rail-inspection/` 根，`curl -I` 返回 200 且 Content-Length 与本地一致
- [ ] 旧版本 data 文件保留未删（回滚用）
- [ ] 导出目录扫描：无 `DeepSeekConfig.asset`、无 `api.deepseek.com`、无 Key 明文

## C. 上传与记录

- [ ] 开发者工具上传成功，**记录**：版本号、版本备注、上传时间、data 哈希
- [ ] 后台设为体验版（提审则提交审核并记录提审单号）
- [ ] 真机扫码冒烟：M1 进入 → 引导/数字人 → 问答一轮 → M1→M2 切换
- [ ] 记录本次回滚目标版本（上一个体验版号 + 对应 CDN data 哈希）
