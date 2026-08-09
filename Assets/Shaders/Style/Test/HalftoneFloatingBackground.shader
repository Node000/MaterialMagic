Shader "Style/Test/HalftoneFloatingBackground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BackgroundColorA ("Background Color A", Color) = (0.055,0.035,0.09,1)
        _BackgroundColorB ("Background Color B", Color) = (0.025,0.09,0.14,1)
        _DotColor ("Dot Color", Color) = (0.85,0.48,0.76,1)
        _DotDensity ("Dot Density", Range(30,220)) = 110
        _DotScale ("Dot Scale", Range(0.05,0.65)) = 0.32
        _FloatSpeed ("Float Speed", Range(0,1)) = 0.08
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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; float4 worldPosition : TEXCOORD1; };
            fixed4 _Color, _BackgroundColorA, _BackgroundColorB, _DotColor;
            float4 _ClipRect;
            float _DotDensity, _DotScale, _FloatSpeed;
            v2f vert(appdata_t v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.color = v.color * _Color; o.texcoord = v.texcoord; o.worldPosition = v.vertex; return o; }
            float panelMask(float2 p, float2 center, float2 size)
            {
                float2 q = abs(p - center) - size;
                return 1.0 - smoothstep(0.0, 0.006, max(q.x, q.y));
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float time = _Time.y * _FloatSpeed;
                float2 uv = i.texcoord;
                float diagonal = saturate(uv.x * 0.6 + (1.0 - uv.y) * 0.4);
                float3 rgb = lerp(_BackgroundColorA.rgb, _BackgroundColorB.rgb, diagonal);
                float2 wave = float2(sin(time + uv.y * 5.0), cos(time * 0.8 + uv.x * 4.0)) * 0.018;
                float panelA = panelMask(uv, float2(0.30, 0.56) + wave, float2(0.20, 0.13));
                float panelB = panelMask(uv, float2(0.38, 0.49) + wave * 1.45, float2(0.20, 0.13));
                float panelC = panelMask(uv, float2(0.46, 0.42) + wave * 1.9, float2(0.20, 0.13));
                float floatingPanels = max(panelA * 0.45, max(panelB * 0.28, panelC * 0.16));
                float2 dotUv = uv * _DotDensity;
                float2 cell = frac(dotUv) - 0.5;
                float pulse = sin(uv.x * 5.0 + time * 4.0) * 0.12 + 0.32;
                float dot = 1.0 - smoothstep(_DotScale * 0.5, _DotScale * 0.5 + 0.045, length(cell));
                float halfTone = dot * (pulse + floatingPanels * 0.75);
                float scan = sin(uv.y * 540.0 + time * 6.0) * 0.5 + 0.5;
                rgb += _DotColor.rgb * halfTone * 0.42;
                rgb *= 1.0 - scan * 0.025;
                fixed4 color = fixed4(saturate(rgb) * i.color.rgb, i.color.a);
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
