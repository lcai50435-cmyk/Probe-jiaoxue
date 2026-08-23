# Unity 视频引导动画开发规范（视频播放 / 黑底抠像）

> 适用：开场引导动画、讲解动画、数字人视频等"视频 + UI 遮罩"场景。
> 权威坑清单与参数表见项目文档《引导动画实现与踩坑记录.md》；本文为 AI 可执行契约。

---

## 1. 架构模式（M1 引导已验证）

```
画板 (Canvas, ScreenSpaceOverlay)
└── 引导遮罩 (嵌套 Canvas, sortingOrder 100) ← 挂 M1IntroVideo
    ├── 半黑遮罩   (全屏 Image + Button 点击跳过, raycastTarget=true 挡点击)
    ├── 引导视频   (RawImage + AspectRatioFitter(HeightControlsWidth) + VideoPlayer)
    └── 跳过引导   (右上角 Button, 首次隐藏)
```

**视频链路**：`VideoPlayer → RenderTexture → RawImage(挂 LumaKey 材质)`。
用 RenderTexture 而非 CameraNearPlane：保证视频在遮罩之上、可配合 UI 层级。

**竖屏视频**：高度适配居中（anchor (0.5,0)~(0.5,1) + HeightControlsWidth），不变形不裁切；两侧留黑由遮罩覆盖。

## 2. 播放控制契约（M1IntroVideo 模式）

| 时机 | 动作 |
|---|---|
| `Awake` | 创建 RT（`(int)clip.width` 注意 uint）→ `Time.timeScale = 0` → `player.Prepare()` → 启动超时协程 |
| `Start` | 写 PlayerPrefs 首次标记 → 显示遮罩 → `TryPlay()` |
| `prepareCompleted` | `TryPlay()`（预解码完成，开头不卡） |
| 超时兜底 | `WaitForSecondsRealtime(prepareTimeout)` 后 `TryPlay()`（**禁止 `Invoke`/`WaitForSeconds`：受 timeScale=0 影响不执行**） |
| `loopPointReached` / 跳过 | `Stop()` → 隐藏遮罩 → `Time.timeScale = 1` |

`TryPlay()` 必须幂等（`_started` 标志），防 `prepareCompleted` 与超时双重触发。

**引导期间隐藏常驻数字人（2026-08-18）**：半黑遮罩 alpha=0.8 是半透明的，竖屏视频两侧留黑会透出下方常驻数字人 → `M1IntroVideo.hideWhilePlaying`（GameObject[]，Setup 注入 `DigitalHumanStage/FullBodyView`，运行时兜底路径 `hideStagePath` 自动 Find）在 `Start` 显示遮罩后隐藏、`FinishIntro`（播完/跳过）恢复。隐藏方式**优先禁用 Graphic（RawImage.enabled=false）而非 SetActive(false)**：数字人 VideoPlayer 被 `pauseWhilePlaying` 暂停但未 Stop，禁用 Graphic 保持其运行态，恢复瞬间立即显示动画无停帧；无 Graphic 的对象才 SetActive(false)。视频缺失（clip==null）时不隐藏，防止播放链路不触发导致数字人永久消失。

**引导视频静音 + 字幕（2026-08-18 老板定稿）**：视频音轨不再播放（`M1IntroVideo.Awake` 与 `M1Setup` 均强制 `VideoPlayer.audioOutputMode = None`，画面照播；防场景旧序列化 Direct 覆盖），解说词改字幕：`M1Setup.EnsureSubtitle` 在引导遮罩下创建「引导字幕」TMP（底部 1100×150、白字 34px + 黑色 Outline/Shadow 描边，**无背景条**，直接叠在画面上；2026-08-18 二轮去掉半透明黑条），台词官方文件 `Assets/DigitalHuman/A-04 引导动画/引导动画-1/引导动画-1 台词.txt`，按 `M1IntroVideo.subtitleSegments`/`subtitleTimes`（默认 0.5/4.2/10.2 秒，视频约 15.2s）在 `Update` 按 `player.time` 切换，`FinishIntro` 清空；运行时兜底 `subtitlePath` 自动发现，缺失时 `CreateRuntimeSubtitle` 动态创建（DontSave，字体从跳过按钮 TMP 复制）。**数字人缩小**：引导视频等比缩放 `IntroVideoScale=0.78`（Setup 创建/幂等 + `M1IntroVideo.introVideoScale` 运行时兜底，仅 scale≈1 时覆盖），视频视觉底部上移约 119px，字幕文字与数字人脚部分离不重叠。换新引导视频时同步更新台词分段与时间点。

## 3. 黑底抠像（LumaKey）契约

**适用前提**：视频背景纯黑（sRGB 亮度 ≤ 2）、主体亮色。本项目引导视频背景 0~2、人物暗部 8~40，阈值 0.02/羽化 0.015 分离清晰。

**shader 关键实现**（`Assets/Shaders/UI-LumaKey.shader`）：

```hlsl
// ① 颜色空间：Linear 项目 RT 内是线性值，必须转回 sRGB 再键控，否则人物暗部被误抠
inline half3 ToSRGB(half3 c)
{
    #ifdef UNITY_COLORSPACE_GAMMA
    return c;
    #else
    return LinearToGammaSpace(c);   // Unity 6 无 LinearToSRGB！
    #endif
}

// ② 键控：smoothstep 必须 edge0 < edge1（小值在前）！
half lum = max(srgb.r, max(srgb.g, srgb.b));
half keyAlpha = smoothstep(_KeyThreshold, _KeyThreshold + _KeySmooth, lum);
color.a *= keyAlpha;
```

**禁止事项**：
- ❌ `smoothstep(阈值+羽化, 阈值, x)` —— d3d11 行为完全反转（背景保留、主体抠掉）
- ❌ 线性空间直接键控 —— 人物暗部（sRGB 8~40 → 线性 0.0004~0.019）全被误抠
- ❌ `LinearToSRGB()` —— Unity 6 不存在，编译报错；用 `LinearToGammaSpace` + 宏判断

**新视频接入前必须离线验证**（numpy 模拟 sRGB→线性→转回→键控），确认背景误保留率 ≈0%、主体误抠率 <5%。

### 视频透明与 UI 点击合同

- H.264 `yuv420p` 且 VideoClip `encodeAlpha: 0` 表示视频**没有 Alpha 通道**；画面四角为纯黑只说明素材适合键控，不代表已经是透明成片。此类素材必须保留显示阶段 LumaKey，否则 RawImage 会显示黑色矩形。
- 一个 UI GameObject 只放一个 `Graphic`（`RawImage` 或 `Image`）。禁止在同一节点用 `RawImage + 透明 Image` 叠加点击层：两者共享 `CanvasRenderer` 时会互相覆盖 mesh，表现为视频消失或闪烁。
- 视频本体需要点击时，优先设 `RawImage.raycastTarget = true`；若点击区域必须独立，创建带自己 `CanvasRenderer + Image` 的子 GameObject，不与 RawImage 共用节点。

```csharp
// 错误：同一节点叠两个 Graphic
new GameObject("Video", typeof(RawImage), typeof(Image));

// 正确：RawImage 自身承接点击
var raw = videoGo.GetComponent<RawImage>();
raw.raycastTarget = true;
```

### 小尺寸常驻视频：缩小质量（2026-08-10 数字人验收）

- **常驻数字人（显著缩小显示）必须用独立材质资产**（如 `Assets/Shaders/UI-LumaKey-DigitalHuman.mat`，同 `UI/LumaKey` shader、只收窄 `_KeySmooth`），不得改开场引导 `UI-LumaKey.mat` 或全局 Shader。Setup 幂等 Ensure：不存在才创建，存在则保留用户调参。
- **RenderTexture 高质量缩小**：`useMipMap = true` + `autoGenerateMips = false`（filterMode Bilinear），RT 保持视频原生分辨率。开启 `VideoPlayer.sendFrameReadyEvents`，在 `frameReady` 回调中调用 `RenderTexture.GenerateMips()`；禁止在普通 `Update` 中首帧未写入时调用（会报 `render texture is not rendered into yet`），也禁止在 `autoGenerateMips = true` 时手动调用（会报 mip 自动生成冲突）。
- **收窄 KeySmooth 只影响边缘羽化带宽（更硬更锐利），不移动阈值**：人物暗部 sRGB ≥8/255 仍远高于“阈值+羽化上界”，不会误抠；背景 ≤2/255 仍远低于阈值。

## 4. Editor 搭建契约（幂等）

- 嵌套 Canvas 创建后**必须** `StretchFullScreen`（anchor 0~1），否则默认 100×100 内容挤在屏幕中心
- 幂等路径要**自愈**：重跑时修复历史 bug（拉伸、材质、clip 引用），不只"存在就跳过"
- 视频 clip：webm 优先、mp4 兜底（`LoadAssetAtPath ?? LoadAssetAtPath`）；webm 导入干净，mp4 有 H.264 timestamp 警告（Unity 自动修正）
- 材质资产不存在时自动创建（`AssetDatabase.CreateAsset`），存在则加载
- 批处理入口需自己 OpenScene（batchmode 不恢复上次场景）

## 5. 验证方法

- **编译**：batchmode `-quit -executeMethod`（注意项目被编辑器占用时 Library 锁，无法运行）
- **PlayMode 自动化**：本机 batchmode + EnterPlaymode 不可靠（d3d12/license），人工验证为准
- **PlayerPrefs 残留**：测试后清理注册表 `HKCU\Software\Unity\UnityEditor\<公司>\<产品>` 下 key（带 `_h` hash 后缀），否则污染"首次"状态

## 6. 复用指引

- 复制"引导遮罩"层级 + 换 VideoClip 即可复用到 M2/M3
- 无需抠像的视频：去掉 RawImage 的 LumaKey 材质
- 调参走材质 Inspector（Key Threshold / Key Smooth），不改 shader（文件头已注明勿手改）
