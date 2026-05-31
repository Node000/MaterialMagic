Shader "Style/Screen/EventGlitchOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _RGBSplitAmount ("RGB Split Pixels", Range(0,32)) = 4
        _BlockNoiseAmount ("Block Noise Amount", Range(0,1)) = 0.35
        _LineShiftAmount ("Line Shift Pixels", Range(0,64)) = 12
        _FlashAmount ("Flash Amount", Range(0,1)) = 0.08
        _DurationAmount ("Duration Amount", Range(0,1)) = 0
        _OverlayColor ("Overlay Color", Color) = (0.94,0.35,0.65,0.16)
        _ScanlineIntensity ("Scanline Intensity", Range(0,0.4)) = 0.08
        _Seed ("Seed", Float) = 0
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
            fixed4 _OverlayColor;
            float4 _ClipRect;
            float _RGBSplitAmount;
            float _BlockNoiseAmount;
            float _LineShiftAmount;
            float _FlashAmount;
            float _DurationAmount;
            float _ScanlineIntensity;
            float _Seed;

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
                float amount = saturate(_DurationAmount);
                float2 uv = IN.texcoord;
                float2 pixelSize = max(_MainTex_TexelSize.xy, float2(0.000001,0.000001));
                float timeStep = floor((_Time.y + _Seed) * 28.0);
                float2 block = floor(uv * float2(18.0, 10.0));
                float active = step(1.0 - _BlockNoiseAmount * amount, hash21(block + timeStep));
                float rowIndex = floor(uv.y * 80.0);
                float lineOffset = (hash21(float2(rowIndex, timeStep + 17.0)) * 2.0 - 1.0) * _LineShiftAmount * pixelSize.x * active * amount;
                float2 corruptUv = frac(uv + float2(lineOffset, 0));
                float2 split = float2(_RGBSplitAmount * pixelSize.x, 0) * amount;
                fixed4 baseColor = tex2D(_MainTex, corruptUv) + _TextureSampleAdd;
                fixed4 plus = tex2D(_MainTex, corruptUv + split) + _TextureSampleAdd;
                fixed4 minus = tex2D(_MainTex, corruptUv - split) + _TextureSampleAdd;
                baseColor.r = plus.r;
                baseColor.b = minus.b;
                float scanline = sin(uv.y * 900.0 + _Time.y * 16.0) * 0.5 + 0.5;
                baseColor.rgb = lerp(baseColor.rgb, baseColor.rgb * (1.0 - _ScanlineIntensity), scanline * amount);
                baseColor.rgb = lerp(baseColor.rgb, 1.0.xxx, _FlashAmount * amount);
                baseColor.rgb += _OverlayColor.rgb * _OverlayColor.a * amount;
                baseColor *= IN.color;
                baseColor.a *= amount;

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
