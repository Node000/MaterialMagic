Shader "Style/Sprite/Dissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _DissolveTex ("Dissolve Texture", 2D) = "gray" {}
        _DissolveThreshold ("Dissolve Threshold", Range(0,1)) = 0
        _EdgeWidth ("Edge Width", Range(0.001,0.35)) = 0.08
        _EdgeColorA ("Edge Color A", Color) = (0.94,0.35,0.65,1)
        _EdgeColorB ("Edge Color B", Color) = (0.35,0.84,1,1)
        _NoiseScale ("Noise Scale", Float) = 1
        [Enum(Off,0,On,1)] _Invert ("Invert", Float) = 0
        _PixelModeAmount ("Pixel Mode Amount", Range(0,1)) = 0.25
        _ScanlineAmount ("Scanline Amount", Range(0,1)) = 0.15
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
            sampler2D _DissolveTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            fixed4 _EdgeColorA;
            fixed4 _EdgeColorB;
            float4 _ClipRect;
            float _DissolveThreshold;
            float _EdgeWidth;
            float _NoiseScale;
            float _Invert;
            float _PixelModeAmount;
            float _ScanlineAmount;

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
                float2 pixelUv = floor(uv * lerp(512.0, 42.0, _PixelModeAmount)) / lerp(512.0, 42.0, _PixelModeAmount);
                float noiseTex = tex2D(_DissolveTex, lerp(uv, pixelUv, _PixelModeAmount) * _NoiseScale).r;
                float scanRow = frac(uv.y * 96.0 + _Time.y * 0.7);
                float mask = lerp(noiseTex, saturate(noiseTex * 0.78 + scanRow * 0.22), _ScanlineAmount);
                mask = (_Invert > 0.5) ? 1.0 - mask : mask;

                fixed4 source = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;
                float threshold = saturate(_DissolveThreshold);
                float edge = smoothstep(threshold - _EdgeWidth, threshold, mask) - smoothstep(threshold, threshold + _EdgeWidth, mask);
                float visible = step(threshold, mask);
                fixed3 edgeColor = lerp(_EdgeColorA.rgb, _EdgeColorB.rgb, hash21(floor(uv * 22.0)));
                source.rgb = lerp(source.rgb, edgeColor, saturate(edge * max(_EdgeColorA.a, _EdgeColorB.a) * 1.6));
                source.a *= visible;
                source.a += edge * max(_EdgeColorA.a, _EdgeColorB.a) * (1.0 - threshold) * 0.65;

                #ifdef UNITY_UI_CLIP_RECT
                source.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(source.a - 0.001);
                #endif

                return saturate(source);
            }
            ENDCG
        }
    }
}
