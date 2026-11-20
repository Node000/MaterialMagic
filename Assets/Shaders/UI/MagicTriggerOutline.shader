Shader "MaterialMagic/UI/Magic Trigger Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 bounds : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 bounds : TEXCOORD1;
                float2 localPosition : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex, _AlphaTex;
            float4 _MainTex_TexelSize, _ClipRect;

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = UnityObjectToClipPos(v.vertex);
                o.localPosition = v.vertex.xy;
                o.color = v.color;
                o.uv = v.uv;
                o.bounds = v.bounds;
                return o;
            }

            fixed sampleAlpha(float2 uv, float4 bounds)
            {
                float2 inside = step(bounds.xy, uv) * step(uv, bounds.zw);
                float2 inset = 0.5 * _MainTex_TexelSize.xy;
                float2 safeUV = clamp(uv, bounds.xy + inset, bounds.zw - inset);
                return tex2D(_MainTex, safeUV).a * tex2D(_AlphaTex, safeUV).r * inside.x * inside.y;
            }

            fixed4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv.xy;
                float2 d = i.uv.zw;
                float2 diag = d * 0.70710678;
                fixed center = sampleAlpha(uv, i.bounds);
                fixed outer = max(sampleAlpha(uv + float2(d.x, 0), i.bounds), sampleAlpha(uv - float2(d.x, 0), i.bounds));
                outer = max(outer, max(sampleAlpha(uv + float2(0, d.y), i.bounds), sampleAlpha(uv - float2(0, d.y), i.bounds)));
                outer = max(outer, max(sampleAlpha(uv + diag, i.bounds), sampleAlpha(uv - diag, i.bounds)));
                diag.y = -diag.y;
                outer = max(outer, max(sampleAlpha(uv + diag, i.bounds), sampleAlpha(uv - diag, i.bounds)));
                fixed4 result = fixed4(i.color.rgb, saturate(outer - center) * i.color.a);
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(i.localPosition, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
