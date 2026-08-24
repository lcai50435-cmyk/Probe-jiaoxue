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
                return LinearToGammaSpace(c);
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
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // 黑底抠像：按 max(r,g,b) 亮度键控（纯黑背景 ~0，人物暗部 >= 8/255）
                // 注意 smoothstep(edge0, edge1, x) 要求 edge0 < edge1：x<=edge0→0(透明)，x>=edge1→1(保留)。
                half3 srgb = ToSRGB(color.rgb);
                half lum = max(srgb.r, max(srgb.g, srgb.b));
                half keyAlpha = smoothstep(_KeyThreshold, _KeyThreshold + _KeySmooth, lum);
                color.a *= keyAlpha;
                half greenGuide = step(_GreenGuideThreshold, srgb.g) * step(srgb.r + _GreenGuideDominance, srgb.g) * step(srgb.b + _GreenGuideDominance, srgb.g);
                color.a *= 1 - _RemoveGreenGuide * greenGuide;

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
