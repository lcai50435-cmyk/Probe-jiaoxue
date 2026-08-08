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
