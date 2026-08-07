# Fay 数字人框架接入 DeepSeek 方案

> 适用于「2D 数字人 + DeepSeek API + 语音/文字交流 + 文字回复（不要求对口型）」场景
> 整理日期：2026-08-05 ｜ 信息来源：GitHub 仓库（通过 OpenCLI 抓取）

---

## 一、项目概况

| 项 | 内容 |
|---|---|
| 项目名称 | **Fay 数字人框架**（xszyou/Fay） |
| GitHub | https://github.com/xszyou/fay |
| Star | **13.4k+**（活跃维护，长期更新） |
| 定位 | 面向终端的数字人落地应用框架：向上适配各种数字人模型技术，向下接入各式大语言模型，便于更换 TTS、ASR 模型，为单片机、app、网站提供全面的数字人应用接口 |
| 许可 | 完全开源，商用免责 |
| 核心卖点 | 数字人（2.5D/3D/网页/桌面）与 LLM（OpenAI 兼容接口，**原生支持 DeepSeek**）自由组合，全链路可插拔 |

### 与本项目需求的匹配度

| 需求 | 支持情况 |
|---|---|
| 2D / 2.5D 数字人形象 | ✅ 支持 2.5D、3D、网页、桌面、移动端数字人，模型可自由更换 |
| 接入 DeepSeek API | ✅ OpenAI 兼容接口直连，`gpt_model_engine=deepseek`、`big_model_engine=deepseek-r1` |
| 用户语音交流 | ✅ ASR 语音识别（本地 FunASR / 阿里云 NLS / SenseVoice） |
| 用户打字交流 | ✅ 文字交互接口 |
| 数字人文字回复（不要口型） | ✅ TTS 为可插拔模块，**可关闭**，回复文本走接口直接展示 |

---

## 二、这个开源项目能做什么（能力清单）

### 1. 对话与交互
- 文字交互接口、语音交互接口、数字人驱动接口、管理控制接口、自动播报接口、意图接口
- 支持唤醒词及打断对话
- 支持语音指令灵活配置执行（`qa.csv` 问答对配置）
- 支持自定义知识库、自定义问答对、自定义人设信息（角色设定）
- 支持 agent 自主决策与工具调用
- 支持 DeepSeek 等 thinking（推理）大模型
- 仿生记忆机制（长期记忆）、自我认知提升
- 基于日程式数字人主动对话（主动发起话题）
- 机器人表情输出（配合模型表现）

### 2. 数字人表现
- 2.5D / 3D / 网页 / 桌面 / 移动端模型自由切换
- 支持数字人自动播报模式（虚拟教师、虚拟主播、新闻播报）
- 支持机器人表情输出

### 3. 工程能力
- 完全开源、支持全离线使用（本地 ASR/LLM/TTS 可全离线）
- 全时流式支持（流式语音/文本）
- 支持任意终端接入：单片机、App、网站、大屏、三方业务系统
- 支持多用户多路并发
- 支持服务器模式及单机模式
- 支持后台静默启动
- 支持 MCP 工具管理（SSE、Studio）
- 提供配置管理中心（Web 管理页面）

### 4. 可插拔模块
| 模块 | 可选实现 |
|---|---|
| ASR 语音识别 | FunASR（本地推荐）、阿里云 NLS、SenseVoice |
| TTS 语音合成 | 阿里云、微软 Azure、火山引擎豆包、GPT-SoVITS、GPT-SoVITS_v3 |
| LLM 大模型 | 任意 OpenAI 兼容接口：DeepSeek、GLM、Qwen、Moonshot、MiniMax 等 |
| Embedding | text-embedding-qwen3-embedding-0.6b、BAAI/bge-large-zh-v1.5 等 |

---

## 三、相关链接

| 资源 | 地址 |
|---|---|
| GitHub 主仓库 | https://github.com/xszyou/fay |
| Fay 桌面版（fay-desk，开箱即用） | https://github.com/TheRamU/fay-desk |
| 官方文档（飞书） | https://qqk9ntwbcit.feishu.cn/wiki/JzMJw7AghiO8eHktMwlcxznenIg |
| 更新日志（飞书） | https://qqk9ntwbcit.feishu.cn/wiki/UlbZwfAXgiKSquk52AkcibhHngg |
| 数字人模型使用教程 | https://qqk9ntwbcit.feishu.cn/wiki/GHevwqxwfiX4hCk8yJCcoJ54nqg |
| 集成到自家产品教程 | https://qqk9ntwbcit.feishu.cn/wiki/Mcw3wbA3RiNZzwkexz6cnKCsnhh |
| 交流方式 | 公众号「fay数字人」（先 star 仓库） |

---

## 四、环境要求

| 项 | 要求 |
|---|---|
| Python | **3.12** |
| 操作系统 | Windows / macOS / Ubuntu |
| Ubuntu 额外依赖 | `sudo apt install build-essential portaudio19-dev` |
| 网络 | 调用 DeepSeek API 需联网；若全本地化（本地 LLM）可离线 |

---

## 五、拉取与安装

### 1. 克隆仓库

```bash
git clone https://github.com/xszyou/fay.git
cd fay
```

### 2. 安装 Python 依赖

```bash
pip install -r requirements.txt
```

### 3.（可选）准备本地 ASR 服务

推荐使用 FunASR（免费、本地、无需外网），按 `asr/funasr/README.md` 说明启动，默认监听 `127.0.0.1:10197`。

---

## 六、配置（添加 DeepSeek 接口）

### 1. 生成配置文件

仓库根目录 `system.conf.bak` 重命名为 `system.conf`，用文本编辑器打开。

### 2. 填写 DeepSeek API（核心三步）

在 `[key]` 段填写：

```ini
# ===== DeepSeek 接入配置 =====
# 小模型（日常对话）：
gpt_api_key=sk-你的DeepSeek_API_KEY
gpt_base_url=https://api.deepseek.com/v1
gpt_model_engine=deepseek-chat

# 大模型（复杂推理，如伤损原理深度问答；留空则全部走小模型）：
big_model_engine=deepseek-reasoner
# big_model_base_url=   # 留空则复用 gpt_base_url
# big_model_api_key=    # 留空则复用 gpt_api_key
```

> DeepSeek 官方 OpenAI 兼容接口：
> - Base URL：`https://api.deepseek.com` 或 `https://api.deepseek.com/v1`
> - 对话模型：`deepseek-chat`（DeepSeek-V3）
> - 推理模型：`deepseek-reasoner`（DeepSeek-R1）
> - API Key 在 https://platform.deepseek.com 申请

### 3. 语音识别（ASR）配置

```ini
# funasr / ali / sensevoice（推荐 funasr，免费本地）
asr_mode = funasr
local_asr_ip = 127.0.0.1
local_asr_port = 10197
```

若用阿里云实时语音识别（免费 3 个月试用）：

```ini
asr_mode = ali
ali_nls_key_id=你的AccessKeyId
ali_nls_key_secret=你的AccessKeySecret
ali_nls_app_key=你的AppKey
```

### 4. 语音合成（TTS）配置 —— 本项目可关闭

本项目是「文字形式回复、不需要对口型」，**可以不启动 TTS**（数字人只用文字气泡展示回复）。若后续想要语音播报，可选：

```ini
tts_module = ali          # azure / ali / gptsovits / volcano / gptsovits_v3
ali_tss_key_id=...
ali_tss_key_secret=...
ali_tss_app_key=...
```

### 5. 启动模式配置

```ini
# common=本地窗口模式；web=网页模式（服务器/docker 推荐，通过 http://127.0.0.1:5000 控制）
start_mode = web
fay_url = http://127.0.0.1:5000
```

> 本项目对接微信小程序/Web 前端 → **建议 `start_mode = web`**，把 Fay 当后端服务用。

### 6. 其他常用配置项

| 配置项 | 说明 |
|---|---|
| `embedding_api_model` | 知识库向量化模型，如 `text-embedding-qwen3-embedding-0.6b`、`BAAI/bge-large-zh-v1.5` |
| `embedding_base_url` | 留空复用 gpt_base_url，key 始终复用 gpt_api_key |
| `proxy_config` | HTTP 代理，如 `127.0.0.1:7890`（网络受限时用） |
| `qa.csv` | 自定义语音指令/问答对（放仓库根目录） |

---

## 七、启动与验证

```bash
# 本地启动（web 模式）
python main.py start

# 或使用公共资源配置中心（速度慢，建议换自己的 key）
python main.py start -config_center d19f7b0a-2b8a-4503-8c0d-1a587b90eb69
```

启动后：
1. 浏览器访问管理页面：**http://127.0.0.1:5000**
2. 在管理页面绑定/选择数字人模型（2.5D 模型资源见上方飞书文档）
3. 验证对话：管理页面直接打字提问，确认 DeepSeek 正常回复
4. 验证语音：对着麦克风说话 → FunASR 转文字 → DeepSeek 回复 → 页面展示文字

---

## 八、仓库目录结构（关键部分）

```
fay/
├── main.py               # 入口
├── fay_booter.py         # 启动器
├── system.conf.bak       # 主配置（重命名为 system.conf 使用）
├── config.json           # 附加配置
├── qa.csv                # 语音指令/问答对
├── ai_module/            # AI 模块（人设、对话编排）
├── llm/                  # 大语言模型接入层（OpenAI 兼容）
├── asr/                  # 语音识别（funasr / ali / sensevoice）
├── tts/                  # 语音合成（ali / azure / volcano / gptsovits）
├── core/                 # 核心逻辑
├── gui/                  # 界面
├── skills/               # agent 技能
├── mcp_servers/          # MCP 工具服务
├── memory/               # 记忆机制
├── scheduler/            # 日程式主动对话
├── simulation_engine/    # 模拟引擎
├── samples/              # 示例
├── docs/                 # 文档
├── scripts/              # 脚本
└── requirements.txt      # Python 依赖
```

---

## 九、对接本项目（微信小程序 2D 数字人）的落地建议

### 推荐架构

```
微信小程序（前端）
 ├─ 2D 贴图数字人（序列帧动画，已有素材）
 ├─ 输入：打字 或 微信录音转文字（小程序自带）
 └─ 展示：文字气泡回复
        │
        ▼  HTTP / WebSocket（Fay 文字/语音交互接口）
Fay 后端（Python 服务）
 ├─ DeepSeek API（gpt_model_engine=deepseek-chat）
 ├─ ASR：FunASR 本地 或 小程序端转文字后直传文本
 └─ TTS：不启用（文字回复）
```

### 落地步骤

1. **后端**：按第五节、第六节部署 Fay，配置 DeepSeek，`start_mode=web`
2. **前端数字人**：保留现有 2D 贴图序列帧动画（待机/讲解/思考三态），**不需要 Fay 自带的 3D/2.5D 模型**——Fay 只当「对话大脑」
3. **交互**：用户打字/语音 → 调 Fay 文字交互接口 → 返回文本 → 前端按现有「思考动画 → 讲解动画 + 气泡逐字显示」播放
4. **语音输入**：微信小程序 `wx.getRecorderManager()` 录音 + `wx.serviceMarket` 或腾讯云 ASR 转文字；或后端接 FunASR
5. **人设/知识库**：在 Fay 配置自定义人设 + 探伤知识问答对（qa.csv / 知识库），让"铁小探"回答探伤专业问题

---

## 十、备选开源项目（如 Fay 不合适）

| 项目 | Star | 说明 |
|---|---|---|
| TheRamU/fay-desk | 185 | Fay 桌面版框架，开箱即用，数字人 + AI 对话 + 动态壁纸 |
| znn1980/web-digital-human | 1 | 最简 Web 数字人：语音识别 + DeepSeek 对话 + 语音合成 |
| clouds443/ai-digital-guide | 0 | Live2D + DeepSeek + 语音问答 + RAG 知识库 |
| l11223/digital-human-livestream | 96 | LiveTalking 二次开发，DeepSeek LLM 对话 + B 站弹幕互动 |

---

## 十一、注意事项

1. **数字人模型资源**：Fay 本身不带模型资产，2.5D/3D 模型需按官方飞书文档获取（关注公众号「fay数字人」）
2. **ASR 首次运行**：FunASR 首次会下载模型，需联网且较慢；也可直接用阿里云 NLS（免费 3 个月）
3. **DeepSeek 费用**：API 按 token 计费，建议管理页设置用量/限流，避免教学演示时超额
4. **端口**：默认管理页 5000、本地 ASR 10197，部署到服务器需开放端口并改 `fay_url`
5. **商用**：完全开源、商用免责，放心用于本项目（微信小程序教学工具）
6. **文档更新**：飞书文档与 GitHub 持续更新，以仓库最新版为准

---

*本方案基于 2026-08-05 抓取的 Fay 仓库（v1.8.6 时代）README 与 system.conf.bak 整理。*
