Shader "UI/JpegCorruption"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0, 1)) = 0.65
        _BlockSize ("Block Size (Pixels)", Range(2, 128)) = 24
        _BlockProbability ("Block Probability", Range(0, 1)) = 0.45
        _MaxBlockOffset ("Max Block Offset (Pixels)", Range(0, 128)) = 22
        _RGBSplit ("RGB Split (Pixels)", Range(0, 32)) = 3
        _ChromaOffset ("Chroma Offset (Pixels)", Range(0, 64)) = 5
        _LumaSteps ("Luma Steps", Range(2, 64)) = 18
        _ChromaSteps ("Chroma Steps", Range(2, 64)) = 8
        _LineJitter ("Line Jitter (Pixels)", Range(0, 64)) = 6
        _Speed ("Speed", Float) = 1
        _UpdateRate ("Update Rate", Float) = 8
        _Seed ("Seed", Float) = 0
        [Enum(Off, 0, On, 1)] _UseTime ("Use Time", Float) = 1
        [Enum(Off, 0, On, 1)] _PreserveAlpha ("Preserve Alpha", Float) = 1
        _Saturation ("Saturation", Range(0, 2)) = 1.15
        _Contrast ("Contrast", Range(0, 2)) = 1.08
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
            HLSLPROGRAM
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
            float4 _ClipRect;
            float _Intensity;
            float _BlockSize;
            float _BlockProbability;
            float _MaxBlockOffset;
            float _RGBSplit;
            float _ChromaOffset;
            float _LumaSteps;
            float _ChromaSteps;
            float _LineJitter;
            float _Speed;
            float _UpdateRate;
            float _Seed;
            float _UseTime;
            float _PreserveAlpha;
            float _Saturation;
            float _Contrast;

            static const float3 LUMA_WEIGHTS = float3(0.299, 0.587, 0.114);

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 hash22(float2 p)
            {
                return float2(hash21(p), hash21(p + 37.17));
            }

            fixed4 sampleMain(float2 uv)
            {
                return tex2D(_MainTex, uv) + _TextureSampleAdd;
            }

            float posterize01(float value, float steps)
            {
                steps = max(1.0, steps);
                return floor(saturate(value) * steps) / steps;
            }

            float3 posterizeChroma(float3 chroma, float steps)
            {
                steps = max(1.0, steps);
                return floor((saturate(chroma * 0.5 + 0.5)) * steps) / steps * 2.0 - 1.0;
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
                fixed4 original = sampleMain(uv);
                float intensity = saturate(_Intensity);

                if (intensity <= 0.0001)
                {
                    fixed4 untouched = original * IN.color;

                    #ifdef UNITY_UI_CLIP_RECT
                    untouched.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                    #endif

                    #ifdef UNITY_UI_ALPHACLIP
                    clip(untouched.a - 0.001);
                    #endif

                    return untouched;
                }

                float2 texelSize = max(_MainTex_TexelSize.xy, float2(0.000001, 0.000001));
                float2 textureSize = max(_MainTex_TexelSize.zw, float2(1.0, 1.0));
                float2 pixel = uv * textureSize;
                float blockSize = max(1.0, _BlockSize);
                float2 blockId = floor(pixel / blockSize);
                float updateRate = max(0.001, _UpdateRate);
                float timeStep = (_UseTime > 0.5) ? floor(_Time.y * max(0.0, _Speed) * updateRate) : 0.0;
                float seed = _Seed + timeStep * 19.19;

                float blockChance = saturate(_BlockProbability);
                float blockActive = hash21(blockId + seed) < blockChance ? 1.0 : 0.0;
                float2 blockRandom = hash22(blockId + seed + 11.11) * 2.0 - 1.0;
                float2 blockOffsetPixels = blockRandom * _MaxBlockOffset * blockActive * intensity;

                float lineHeight = max(1.0, blockSize * 0.5);
                float lineId = floor(pixel.y / lineHeight);
                float lineRandom = hash21(float2(lineId, seed + 71.3));
                float lineActive = lineRandom > 0.68 ? 1.0 : 0.0;
                float lineOffsetPixels = (hash21(float2(lineId, seed + 17.7)) * 2.0 - 1.0) * _LineJitter * lineActive * intensity;

                float2 corruptUV = frac(uv + (blockOffsetPixels + float2(lineOffsetPixels, 0.0)) * texelSize);
                float splitAngle = hash21(blockId + seed + 23.23) * 6.2831853;
                float2 splitDir = float2(cos(splitAngle), sin(splitAngle));
                float2 rgbOffset = splitDir * _RGBSplit * texelSize * intensity;

                float3 rgb;
                rgb.r = sampleMain(frac(corruptUV + rgbOffset)).r;
                rgb.g = sampleMain(corruptUV).g;
                rgb.b = sampleMain(frac(corruptUV - rgbOffset)).b;

                float chromaAngle = hash21(blockId + seed + 53.53) * 6.2831853;
                float2 chromaDir = float2(cos(chromaAngle), sin(chromaAngle));
                float3 chromaSource = sampleMain(frac(corruptUV + chromaDir * _ChromaOffset * texelSize * intensity)).rgb;
                float luma = dot(rgb, LUMA_WEIGHTS);
                float chromaLuma = dot(chromaSource, LUMA_WEIGHTS);
                float3 chroma = chromaSource - chromaLuma;
                float3 yccRgb = luma + chroma;

                float quantizedLuma = posterize01(dot(yccRgb, LUMA_WEIGHTS), _LumaSteps);
                float3 quantizedChroma = posterizeChroma(yccRgb - dot(yccRgb, LUMA_WEIGHTS), _ChromaSteps);
                float3 corruptedRgb = saturate(quantizedLuma + quantizedChroma);
                float adjustedLuma = dot(corruptedRgb, LUMA_WEIGHTS);
                corruptedRgb = lerp(adjustedLuma.xxx, corruptedRgb, _Saturation);
                corruptedRgb = (corruptedRgb - 0.5) * _Contrast + 0.5;

                fixed4 corrupted = sampleMain(corruptUV);
                corrupted.rgb = saturate(corruptedRgb);
                corrupted.a = (_PreserveAlpha > 0.5) ? original.a : lerp(original.a, corrupted.a, intensity);

                fixed4 color;
                color.rgb = lerp(original.rgb, corrupted.rgb, intensity);
                color.a = corrupted.a;
                color *= IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDHLSL
        }
    }
}
