// ============================================================
// 黑底抠像 UI Shader：用于引导动画视频（纯黑背景），亮度低于阈值的像素变透明，
// 实现"人物悬空"效果——人物浮在半黑遮罩之上，周围透明露出被压暗的游戏画面。
// 基于内置 UI/Default 结构，兼容 URP Canvas 渲染（stencil / clipping 完整保留）。
//
// 【维护说明】此文件由 AI 维护，请勿手改。如需调整抠像效果，请在材质
// UI-LumaKey.mat 的 Inspector 中调 Key Threshold / Key Smooth 两个参数。
// ============================================================
Shader "UI/LumaKey"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // 黑底抠像参数：max(r,g,b)（sRGB 空间）低于 _KeyThreshold 的像素视为背景（透明）
        _KeyThreshold ("Key Threshold", Range(0, 0.5)) = 0.02
        _KeySmooth ("Key Smooth", Range(0, 0.1)) = 0.015
        _VideoInputIsSRGB ("Video Input Is SRGB", Range(0, 1)) = 0
        _VideoInputHasAlpha ("Video Input Has Alpha", Range(0, 1)) = 1
        _RemoveGreenGuide ("Remove Green Guide", Range(0, 1)) = 0
        _GreenGuideThreshold ("Green Guide Threshold", Range(0, 0.5)) = 0.02
        _GreenGuideDominance ("Green Guide Dominance", Range(0, 0.5)) = 0.02

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _KeyThreshold;
            float _KeySmooth;
            float _VideoInputIsSRGB;
            float _VideoInputHasAlpha;
            float _RemoveGreenGuide;
            float _GreenGuideThreshold;
            float _GreenGuideDominance;

            /// 统一到 sRGB 空间：Linear 项目下 RT 内为线性值，转回 sRGB 再做亮度键控，
            /// 否则人物暗部（黑裤/帽檐等）在线性空间会低于阈值被误抠成透明。
            /// Gamma 项目下原样返回（值本身就在 sRGB 空间）。
            inline half3 ToSRGB(half3 c)
            {
                #ifdef UNITY_COLORSPACE_GAMMA
                return c;
                #else
                return _VideoInputIsSRGB > 0.5 ? c : LinearToGammaSpace(c);
                #endif
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 1) 原始纹理输入（不含 UI tint）：键控与颜色空间转换都必须基于原始值，
                //    否则非白 tint 会扭曲键控阈值，且 WebGL 路径会二次乘 tint。
                half4 raw = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;

                // 2) 黑底抠像：按 max(r,g,b) 亮度键控（纯黑背景 ~0，人物暗部 >= 8/255）
                // 注意 smoothstep(edge0, edge1, x) 要求 edge0 < edge1：x<=edge0→0(透明)，x>=edge1→1(保留)。
                half3 srgb = ToSRGB(raw.rgb);
                half lum = max(srgb.r, max(srgb.g, srgb.b));
                half keyAlpha = smoothstep(_KeyThreshold, _KeyThreshold + _KeySmooth, lum);
                // 微信 Android 的 H.264 解码帧虽然按 RGBA 上传，但源视频无 Alpha，A 通道值没有跨平台保证。
                // _VideoInputHasAlpha=0 时完全由亮度键控生成 Alpha；Editor/原生 WebM 默认仍保留源 Alpha。
                half sourceAlpha = lerp(1.0h, raw.a, saturate(_VideoInputHasAlpha));
                half alpha = sourceAlpha * keyAlpha;
                half greenGuide = step(_GreenGuideThreshold, srgb.g) * step(srgb.r + _GreenGuideDominance, srgb.g) * step(srgb.b + _GreenGuideDominance, srgb.g);
                alpha *= 1 - _RemoveGreenGuide * greenGuide;

                // 3) 显示 RGB：微信 WebGL（_VideoInputIsSRGB=1）视频纹理是 sRGB 编码值、
                //    RT 未做 sRGB 采样解码，输出前必须转回线性，否则显示端二次 gamma 编码导致整体偏白。
                //    Android/Editor（=0）：RT 采样已完成 sRGB→Linear，raw.rgb 即线性值，保持现状。
                half3 display = raw.rgb;
                #if !UNITY_COLORSPACE_GAMMA
                if (_VideoInputIsSRGB > 0.5)
                    display = GammaToLinearSpace(srgb);
                #endif

                // 4) UI tint 只在此乘一次
                half4 color = half4(display * IN.color.rgb, alpha * IN.color.a);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
