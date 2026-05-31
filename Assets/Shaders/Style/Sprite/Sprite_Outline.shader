Shader "Style/Sprite/Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0.55,0.86,1,0.75)
        _OutlineWidth ("Outline Width Pixels", Range(0,8)) = 2
        _PulseSpeed ("Pulse Speed", Float) = 1.2
        _PulseAmount ("Pulse Amount", Range(0,1)) = 0.22
        [Enum(Off,0,On,1)] _UseOuterGlow ("Use Outer Glow", Float) = 0
        _OuterGlowColor ("Outer Glow Color", Color) = (0.94,0.35,0.65,0.3)
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
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            fixed4 _OutlineColor;
            fixed4 _OuterGlowColor;
            float4 _ClipRect;
            float _OutlineWidth;
            float _PulseSpeed;
            float _PulseAmount;
            float _UseOuterGlow;

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

            float sampleAlpha(float2 uv)
            {
                return (tex2D(_MainTex, uv) + _TextureSampleAdd).a;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 source = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                float2 px = _MainTex_TexelSize.xy * _OutlineWidth;
                float outlineAlpha = source.a;
                outlineAlpha = max(outlineAlpha, sampleAlpha(IN.texcoord + float2(px.x, 0)));
                outlineAlpha = max(outlineAlpha, sampleAlpha(IN.texcoord - float2(px.x, 0)));
                outlineAlpha = max(outlineAlpha, sampleAlpha(IN.texcoord + float2(0, px.y)));
                outlineAlpha = max(outlineAlpha, sampleAlpha(IN.texcoord - float2(0, px.y)));
                outlineAlpha = max(outlineAlpha, sampleAlpha(IN.texcoord + px));
                outlineAlpha = max(outlineAlpha, sampleAlpha(IN.texcoord - px));
                outlineAlpha = max(outlineAlpha, sampleAlpha(IN.texcoord + float2(px.x, -px.y)));
                outlineAlpha = max(outlineAlpha, sampleAlpha(IN.texcoord + float2(-px.x, px.y)));

                float outlineOnly = saturate(outlineAlpha - source.a);
                float pulse = 1.0 + (sin(_Time.y * _PulseSpeed * 6.2831853) * 0.5 + 0.5) * _PulseAmount;
                float3 outlineRgb = _OutlineColor.rgb * pulse;
                float3 glowRgb = _OuterGlowColor.rgb * _OuterGlowColor.a * outlineOnly * _UseOuterGlow;

                fixed4 color;
                color.rgb = lerp(outlineRgb + glowRgb, source.rgb, source.a);
                color.a = max(source.a, outlineOnly * _OutlineColor.a * pulse);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return saturate(color);
            }
            ENDCG
        }
    }
}
