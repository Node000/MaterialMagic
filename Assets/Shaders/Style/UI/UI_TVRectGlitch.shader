Shader "UI/TVRectGlitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Effect Intensity", Range(0, 1)) = 1
        _Seed ("Random Seed", Float) = 0
        _UseTime ("Animate Random Events", Float) = 1
        _CycleDuration ("Event Cycle Duration (Seconds)", Range(0.1, 15)) = 3
        _MinQuietDuration ("Minimum Quiet Duration (Seconds)", Range(0, 15)) = 0.8
        _MaxQuietDuration ("Maximum Quiet Duration (Seconds)", Range(0, 15)) = 2.2
        _MinActiveDuration ("Minimum Active Duration (Seconds)", Range(0.02, 8)) = 0.18
        _MaxActiveDuration ("Maximum Active Duration (Seconds)", Range(0.02, 8)) = 0.7
        _RectangleCount ("Rectangles Per Event", Range(1, 8)) = 3
        _MinRectWidth ("Minimum Rectangle Width (Image Fraction)", Range(0.01, 1)) = 0.12
        _MaxRectWidth ("Maximum Rectangle Width (Image Fraction)", Range(0.01, 1)) = 0.55
        _MinRectHeight ("Minimum Rectangle Height (Image Fraction)", Range(0.005, 1)) = 0.025
        _MaxRectHeight ("Maximum Rectangle Height (Image Fraction)", Range(0.005, 1)) = 0.12
        _EdgeSoftness ("Rectangle Edge Softness", Range(0, 0.05)) = 0.002
        _LineHeightPixels ("Horizontal Line Height (Pixels)", Range(1, 64)) = 3
        _HorizontalDisplacementPixels ("Horizontal Displacement (Pixels)", Range(0, 256)) = 32
        _VerticalDisplacementPixels ("Vertical Displacement (Pixels)", Range(0, 64)) = 2
        _RGBSplitPixels ("RGB Split (Pixels)", Range(0, 64)) = 5
        _ScanlineIntensity ("Scanline Intensity", Range(0, 0.6)) = 0.16
        _NoiseIntensity ("Noise Intensity", Range(0, 0.5)) = 0.06
        _FlashIntensity ("Flash Intensity", Range(0, 1)) = 0.12
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
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
            float _Seed;
            float _UseTime;
            float _CycleDuration;
            float _MinQuietDuration;
            float _MaxQuietDuration;
            float _MinActiveDuration;
            float _MaxActiveDuration;
            float _RectangleCount;
            float _MinRectWidth;
            float _MaxRectWidth;
            float _MinRectHeight;
            float _MaxRectHeight;
            float _EdgeSoftness;
            float _LineHeightPixels;
            float _HorizontalDisplacementPixels;
            float _VerticalDisplacementPixels;
            float _RGBSplitPixels;
            float _ScanlineIntensity;
            float _NoiseIntensity;
            float _FlashIntensity;

            float hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float2 hash22(float2 value)
            {
                return float2(hash21(value), hash21(value + 37.17));
            }

            fixed4 sampleMain(float2 uv)
            {
                return tex2D(_MainTex, saturate(uv)) + _TextureSampleAdd;
            }

            v2f vert(appdata_t vertex)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(vertex);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = vertex.vertex;
                output.vertex = UnityObjectToClipPos(vertex.vertex);
                output.texcoord = vertex.texcoord;
                output.color = vertex.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                fixed4 original = sampleMain(uv);
                float intensity = saturate(_Intensity);
                if (intensity <= 0.0001)
                    return original * input.color;

                float cycleDuration = max(_CycleDuration, 0.01);
                float animatedTime = _UseTime > 0.5 ? _Time.y : 0.0;
                float cycleIndex = floor(animatedTime / cycleDuration);
                float cycleTime = animatedTime - cycleIndex * cycleDuration;
                float eventSeed = _Seed + cycleIndex * 41.371;
                float quietDuration = lerp(min(_MinQuietDuration, _MaxQuietDuration), max(_MinQuietDuration, _MaxQuietDuration), hash21(float2(eventSeed, 3.17)));
                float requestedActiveDuration = lerp(min(_MinActiveDuration, _MaxActiveDuration), max(_MinActiveDuration, _MaxActiveDuration), hash21(float2(eventSeed, 9.41)));
                float activeDuration = min(requestedActiveDuration, max(0.0, cycleDuration - quietDuration));
                float eventActive = step(quietDuration, cycleTime) * step(cycleTime, quietDuration + activeDuration);
                if (eventActive <= 0.0)
                    return original * input.color;

                float2 imagePixel = uv / max(_MainTex_TexelSize.xy, float2(0.000001, 0.000001));
                float2 displacedUv = uv;
                float rectangleMask = 0.0;
                float rectangleIndex = 0.0;

                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    if (i >= (int)round(_RectangleCount))
                        break;

                    float id = (float)i;
                    float2 randomCenter = hash22(float2(eventSeed + id * 7.13, id * 13.71));
                    float width = lerp(min(_MinRectWidth, _MaxRectWidth), max(_MinRectWidth, _MaxRectWidth), hash21(float2(eventSeed, id + 21.9)));
                    float height = lerp(min(_MinRectHeight, _MaxRectHeight), max(_MinRectHeight, _MaxRectHeight), hash21(float2(eventSeed, id + 47.6)));
                    float2 halfSize = float2(width, height) * 0.5;
                    float2 center = lerp(halfSize, 1.0 - halfSize, randomCenter);
                    float2 edgeDistance = abs(uv - center) - halfSize;
                    float outsideDistance = max(edgeDistance.x, edgeDistance.y);
                    float mask = 1.0 - smoothstep(0.0, max(_EdgeSoftness, 0.00001), outsideDistance);

                    if (mask > rectangleMask)
                    {
                        rectangleMask = mask;
                        rectangleIndex = id;
                        float rowIndex = floor(imagePixel.y / max(_LineHeightPixels, 1.0));
                        float horizontalShift = (hash21(float2(rowIndex, eventSeed + id * 19.3)) * 2.0 - 1.0) * _HorizontalDisplacementPixels;
                        float verticalShift = (hash21(float2(rowIndex + 53.2, eventSeed + id * 7.7)) * 2.0 - 1.0) * _VerticalDisplacementPixels;
                        displacedUv = saturate(uv + float2(horizontalShift, verticalShift) * _MainTex_TexelSize.xy);
                    }
                }

                float effectMask = rectangleMask * eventActive * intensity;
                if (effectMask <= 0.0001)
                    return original * input.color;

                float2 splitOffset = float2(_RGBSplitPixels, 0.0) * _MainTex_TexelSize.xy * effectMask;
                fixed4 centerColor = sampleMain(displacedUv);
                float3 color;
                color.r = sampleMain(displacedUv + splitOffset).r;
                color.g = centerColor.g;
                color.b = sampleMain(displacedUv - splitOffset).b;

                float lineNoise = hash21(float2(floor(imagePixel.y), eventSeed + rectangleIndex * 31.2)) - 0.5;
                float scanline = sin(imagePixel.y * 3.14159265) * 0.5 + 0.5;
                color += lineNoise * _NoiseIntensity * effectMask;
                color *= 1.0 - scanline * _ScanlineIntensity * effectMask;
                color = lerp(color, 1.0.xxx, _FlashIntensity * effectMask);

                fixed4 output = half4(saturate(lerp(original.rgb, color, effectMask)), original.a) * input.color;
                #ifdef UNITY_UI_CLIP_RECT
                output.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(output.a - 0.001);
                #endif
                return output;
            }
            ENDHLSL
        }
    }
}
