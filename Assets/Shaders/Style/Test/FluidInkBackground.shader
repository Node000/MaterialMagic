Shader "Style/Test/FluidInkBackground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ColorCount ("Color Count", Range(1,5)) = 4
        _FlowSpeed ("Flow Speed", Range(0,2)) = 0.28
        _ColorA ("Color A", Color) = (0.07,0.04,0.14,1)
        _ColorB ("Color B", Color) = (0.45,0.08,0.38,1)
        _ColorC ("Color C", Color) = (0.08,0.28,0.48,1)
        _ColorD ("Color D", Color) = (0.65,0.28,0.55,1)
        _ColorE ("Color E", Color) = (0.18,0.7,0.7,1)
        _Contrast ("Contrast", Range(0.2,2)) = 0.85
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
            fixed4 _Color, _ColorA, _ColorB, _ColorC, _ColorD, _ColorE;
            float4 _ClipRect;
            float _ColorCount, _FlowSpeed, _Contrast;
            v2f vert(appdata_t v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.color = v.color * _Color; o.texcoord = v.texcoord; o.worldPosition = v.vertex; return o; }
            float noise(float2 p) { return sin(p.x) * sin(p.y); }
            float field(float2 p, float time)
            {
                float v = 0.0;
                v += sin(p.x * 2.1 + time * 0.9);
                v += sin(p.y * 2.6 - time * 0.72);
                v += sin((p.x + p.y) * 1.75 + time * 0.48);
                v += sin(length(p) * 4.4 - time * 1.1);
                return v * 0.25;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float time = _Time.y * _FlowSpeed;
                float2 p = (i.texcoord - 0.5) * float2(1.7, 1.0);
                float flowA = field(p, time);
                float2 offset = float2(field(p + float2(2.3, 0.0), time), field(p + float2(0.0, 2.3), time)) * 0.16;
                float flowB = field(p + offset, time * 1.3);
                float value = saturate((flowA * 0.45 + flowB * 0.75) * _Contrast + 0.5);
                float count = round(_ColorCount);
                float3 rgb = _ColorA.rgb;
                rgb = lerp(rgb, _ColorB.rgb, smoothstep(0.05, 0.42, value));
                if (count > 2.0) rgb = lerp(rgb, _ColorC.rgb, smoothstep(0.32, 0.66, value));
                if (count > 3.0) rgb = lerp(rgb, _ColorD.rgb, smoothstep(0.56, 0.82, value));
                if (count > 4.0) rgb = lerp(rgb, _ColorE.rgb, smoothstep(0.75, 0.98, value));
                float vein = smoothstep(0.78, 0.95, abs(sin((flowA + flowB) * 9.0)));
                rgb += vein * 0.06;
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
