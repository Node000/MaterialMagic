Shader "Style/UI/GlassPanel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BaseColor ("Base Color", Color) = (0.094,0.075,0.122,0.86)
        _SecondaryTint ("Secondary Tint", Color) = (0.349,0.843,1,0.18)
        _BorderColorPrimary ("Primary Border", Color) = (0.78,0.80,0.84,0.55)
        _BorderColorAccent ("Accent Border", Color) = (0.94,0.35,0.65,0.42)
        _BorderWidth ("Border Width", Range(0,0.2)) = 0.035
        _InnerGlowColor ("Inner Glow", Color) = (0.718,0.612,1,0.20)
        _InnerGlowIntensity ("Inner Glow Intensity", Range(0,1)) = 0.18
        _NoiseTex ("Noise Texture", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Float) = 34
        _NoiseIntensity ("Noise Intensity", Range(0,0.25)) = 0.035
        _ScanlineIntensity ("Scanline Intensity", Range(0,0.2)) = 0.025
        _ScanlineDensity ("Scanline Density", Float) = 180
        _Opacity ("Opacity", Range(0,1)) = 0.92
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
            sampler2D _NoiseTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            fixed4 _BaseColor;
            fixed4 _SecondaryTint;
            fixed4 _BorderColorPrimary;
            fixed4 _BorderColorAccent;
            fixed4 _InnerGlowColor;
            float4 _ClipRect;
            float _BorderWidth;
            float _InnerGlowIntensity;
            float _NoiseScale;
            float _NoiseIntensity;
            float _ScanlineIntensity;
            float _ScanlineDensity;
            float _Opacity;

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
                float2 uv = saturate(IN.texcoord);
                fixed4 sprite = tex2D(_MainTex, uv) + _TextureSampleAdd;
                float edgeDistance = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                float border = 1.0 - smoothstep(_BorderWidth, _BorderWidth + 0.012, edgeDistance);
                float accent = 1.0 - smoothstep(_BorderWidth * 0.48, _BorderWidth * 0.48 + 0.01, edgeDistance);
                float inner = pow(1.0 - saturate(edgeDistance * 2.4), 2.0) * _InnerGlowIntensity;
                float noise = tex2D(_NoiseTex, uv * _NoiseScale + float2(_Time.y * 0.013, _Time.y * 0.009)).r * 2.0 - 1.0;
                float scanline = sin((uv.y * _ScanlineDensity + _Time.y * 4.0) * 6.2831853) * 0.5 + 0.5;

                float3 baseRgb = lerp(_BaseColor.rgb, _SecondaryTint.rgb, saturate(uv.y * 0.35 + 0.1));
                baseRgb = lerp(baseRgb, _InnerGlowColor.rgb, inner * _InnerGlowColor.a);
                baseRgb = lerp(baseRgb, _BorderColorPrimary.rgb, border * _BorderColorPrimary.a);
                baseRgb = lerp(baseRgb, _BorderColorAccent.rgb, accent * _BorderColorAccent.a);
                baseRgb += noise * _NoiseIntensity;
                baseRgb *= 1.0 - scanline * _ScanlineIntensity;

                fixed4 color;
                color.rgb = saturate(baseRgb) * IN.color.rgb;
                color.a = sprite.a * IN.color.a * _Opacity;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
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
