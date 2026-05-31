Shader "Style/Sprite/HologramCRT"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ScanlineIntensity ("Scanline Intensity", Range(0,0.6)) = 0.12
        _JitterAmount ("Jitter Amount", Range(0,0.03)) = 0.004
        _GlitchChance ("Glitch Chance", Range(0,1)) = 0.08
        _GlitchStrength ("Glitch Strength", Range(0,0.08)) = 0.012
        _DualColorA ("Dual Color A", Color) = (0.35,0.84,1,0.35)
        _DualColorB ("Dual Color B", Color) = (0.94,0.35,0.65,0.35)
        _Opacity ("Opacity", Range(0,1)) = 0.82
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
            fixed4 _DualColorA;
            fixed4 _DualColorB;
            float4 _ClipRect;
            float _ScanlineIntensity;
            float _JitterAmount;
            float _GlitchChance;
            float _GlitchStrength;
            float _Opacity;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
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
                float lineId = floor(uv.y * 96.0);
                float timeStep = floor(_Time.y * 10.0);
                float lineNoise = hash21(float2(lineId, timeStep));
                float active = lineNoise < _GlitchChance ? 1.0 : 0.0;
                float lineShift = (hash21(float2(lineId, timeStep + 21.1)) * 2.0 - 1.0) * _GlitchStrength * active;
                float verticalJitter = sin(_Time.y * 18.0 + uv.y * 35.0) * _JitterAmount;
                float2 baseUv = uv + float2(lineShift + verticalJitter, 0);
                fixed4 baseColor = (tex2D(_MainTex, baseUv) + _TextureSampleAdd) * IN.color;
                fixed4 cyan = (tex2D(_MainTex, baseUv + float2(0.006,0)) + _TextureSampleAdd) * _DualColorA;
                fixed4 pink = (tex2D(_MainTex, baseUv - float2(0.006,0)) + _TextureSampleAdd) * _DualColorB;
                float scanline = sin(uv.y * 720.0 + _Time.y * 8.0) * 0.5 + 0.5;
                baseColor.rgb = lerp(baseColor.rgb, baseColor.rgb * (1.0 - _ScanlineIntensity), scanline);
                baseColor.rgb += cyan.rgb * cyan.a * 0.45 + pink.rgb * pink.a * 0.45;
                baseColor.a *= _Opacity;

                #ifdef UNITY_UI_CLIP_RECT
                baseColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(baseColor.a - 0.001);
                #endif

                return saturate(baseColor);
            }
            ENDCG
        }
    }
}
