Shader "UI/MaterialModifiers/BigArrow4Modifier"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Arrow Spread Settings)]
        _CopyCount ("Copy Count (1 to 20)", Range(1, 20)) = 4
        _GroupScale ("Group Scale (Fit to UI)", Range(0.1, 2.0)) = 0.8
        // --- 新增的层级遮挡控制 ---
        [Enum(Right Over Left (Default), 0, Left Over Right, 1)] _OverlapOrder ("Overlap Order", Float) = 0
        
        [Header(Animation Settings)]
        _BaseSpacing ("Base Spacing", Range(0, 0.5)) = 0.1
        _AnimAmplitude ("Animation Amplitude", Range(0, 0.5)) = 0.05
        _AnimSpeed ("Animation Speed", Float) = 2.0

        // UI 遮罩相关参数保留以兼容 UGUI 机制
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
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            
            // 用户控制参数
            float _CopyCount;
            float _GroupScale;
            float _OverlapOrder;
            float _BaseSpacing;
            float _AnimAmplitude;
            float _AnimSpeed;

            float InsideUv(float2 uv)
            {
                return step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0);
            }

            fixed4 SampleMain(float2 uv, fixed4 vertexColor)
            {
                float inside = InsideUv(uv);
                fixed4 color = (tex2D(_MainTex, saturate(uv)) + _TextureSampleAdd) * vertexColor;
                color.a *= inside;
                return color;
            }

            // 透明度混合公式 (源覆盖目标)
            fixed4 Over(fixed4 bottom, fixed4 top)
            {
                float alpha = top.a + bottom.a * (1.0 - top.a);
                float3 rgb = top.rgb * top.a + bottom.rgb * bottom.a * (1.0 - top.a);
                fixed4 result;
                result.rgb = alpha > 0.0001 ? rgb / alpha : 0;
                result.a = alpha;
                return result;
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                fixed4 finalColor = fixed4(0, 0, 0, 0);

                // 1. 计算当前的动态间距
                float animOffset = sin(_Time.y * _AnimSpeed) * _AnimAmplitude;
                float currentSpacing = _BaseSpacing + animOffset;

                // 2. 限定拷贝数量并计算整体居中偏移
                int copies = clamp((int)_CopyCount, 1, 20);
                float totalSpan = (copies - 1.0) * currentSpacing;
                float shiftToCenter = totalSpan * 0.5;

                // 3. 循环绘制箭头
                // 使用最大20次的固定循环+break，对Shader编译器更友好
                for (int i = 0; i < 20; i++)
                {
                    if (i >= copies) break;

                    // --- 新增：动态决定绘制顺序 ---
                    // 如果是 0 (Right Over Left)，则先画最左侧的 (copies - 1 - i)，最后画最右侧的 (0)
                    // 如果是 1 (Left Over Right)，则先画最右侧的 (i)，最后画最左侧的 (copies - 1)
                    int drawIndex = (_OverlapOrder == 0.0) ? (copies - 1 - i) : i;

                    // 当前箭头相对于原始位置的偏移距离
                    float displacement = drawIndex * currentSpacing;

                    float2 currentUV = uv;
                    currentUV.x = currentUV.x + displacement - shiftToCenter;

                    // 应用整体缩放
                    currentUV = (currentUV - 0.5) / _GroupScale + 0.5;

                    // 采样并叠加
                    fixed4 copyColor = SampleMain(currentUV, IN.color);
                    finalColor = Over(finalColor, copyColor);
                }

                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(finalColor.a - 0.001);
                #endif

                return saturate(finalColor);
            }
            ENDCG
        }
    }
}