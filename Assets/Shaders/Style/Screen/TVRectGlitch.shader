Shader "Hidden/Style/TVRectGlitch"
{
    Properties
    {
        _Intensity ("Effect Intensity", Range(0, 1)) = 1
        _Seed ("Random Seed", Float) = 0
        _UseTime ("Animate Random Events", Float) = 1
        _CycleDuration ("Event Cycle Duration (Seconds)", Range(0.1, 15)) = 3
        _MinQuietDuration ("Minimum Quiet Duration (Seconds)", Range(0, 15)) = 0.8
        _MaxQuietDuration ("Maximum Quiet Duration (Seconds)", Range(0, 15)) = 2.2
        _MinActiveDuration ("Minimum Active Duration (Seconds)", Range(0.02, 8)) = 0.18
        _MaxActiveDuration ("Maximum Active Duration (Seconds)", Range(0.02, 8)) = 0.7
        _RectangleCount ("Rectangles Per Event", Range(1, 8)) = 3
        _MinRectWidthPixels ("Minimum Rectangle Width (Pixels)", Range(4, 1920)) = 90
        _MaxRectWidthPixels ("Maximum Rectangle Width (Pixels)", Range(4, 1920)) = 420
        _MinRectHeightPixels ("Minimum Rectangle Height (Pixels)", Range(2, 1080)) = 12
        _MaxRectHeightPixels ("Maximum Rectangle Height (Pixels)", Range(2, 1080)) = 80
        _EdgeSoftnessPixels ("Rectangle Edge Softness (Pixels)", Range(0, 24)) = 1
        _LineHeightPixels ("Horizontal Line Height (Pixels)", Range(1, 64)) = 3
        _HorizontalDisplacementPixels ("Horizontal Displacement (Pixels)", Range(0, 256)) = 32
        _VerticalDisplacementPixels ("Vertical Displacement (Pixels)", Range(0, 64)) = 2
        _RGBSplitPixels ("RGB Split (Pixels)", Range(0, 64)) = 5
        _ScanlineIntensity ("Scanline Intensity", Range(0, 0.6)) = 0.16
        _NoiseIntensity ("Noise Intensity", Range(0, 0.5)) = 0.06
        _FlashIntensity ("Flash Intensity", Range(0, 1)) = 0.12
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "TVRectGlitch"
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Seed;
                float _UseTime;
                float _CycleDuration;
                float _MinQuietDuration;
                float _MaxQuietDuration;
                float _MinActiveDuration;
                float _MaxActiveDuration;
                float _RectangleCount;
                float _MinRectWidthPixels;
                float _MaxRectWidthPixels;
                float _MinRectHeightPixels;
                float _MaxRectHeightPixels;
                float _EdgeSoftnessPixels;
                float _LineHeightPixels;
                float _HorizontalDisplacementPixels;
                float _VerticalDisplacementPixels;
                float _RGBSplitPixels;
                float _ScanlineIntensity;
                float _NoiseIntensity;
                float _FlashIntensity;
            CBUFFER_END

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float2 Hash22(float2 value)
            {
                return float2(Hash21(value), Hash21(value + 37.17));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float intensity = saturate(_Intensity);
                if (intensity <= 0.0001)
                    return original;

                float cycleDuration = max(_CycleDuration, 0.01);
                float animatedTime = _UseTime > 0.5 ? _Time.y : 0.0;
                float cycleIndex = floor(animatedTime / cycleDuration);
                float cycleTime = animatedTime - cycleIndex * cycleDuration;
                float eventSeed = _Seed + cycleIndex * 41.371;
                float quietDuration = lerp(min(_MinQuietDuration, _MaxQuietDuration), max(_MinQuietDuration, _MaxQuietDuration), Hash21(float2(eventSeed, 3.17)));
                float requestedActiveDuration = lerp(min(_MinActiveDuration, _MaxActiveDuration), max(_MinActiveDuration, _MaxActiveDuration), Hash21(float2(eventSeed, 9.41)));
                float activeDuration = min(requestedActiveDuration, max(0.0, cycleDuration - quietDuration));
                float eventActive = step(quietDuration, cycleTime) * step(cycleTime, quietDuration + activeDuration);
                if (eventActive <= 0.0)
                    return original;

                float2 screenSize = max(_ScreenParams.xy, 1.0);
                float2 pixel = uv * screenSize;
                float rectangleMask = 0.0;
                float2 displacedUv = uv;
                float rectangleIndex = 0.0;

                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    if (i >= (int)round(_RectangleCount))
                        break;

                    float id = (float)i;
                    float2 randomCenter = Hash22(float2(eventSeed + id * 7.13, id * 13.71));
                    float width = lerp(min(_MinRectWidthPixels, _MaxRectWidthPixels), max(_MinRectWidthPixels, _MaxRectWidthPixels), Hash21(float2(eventSeed, id + 21.9)));
                    float height = lerp(min(_MinRectHeightPixels, _MaxRectHeightPixels), max(_MinRectHeightPixels, _MaxRectHeightPixels), Hash21(float2(eventSeed, id + 47.6)));
                    float2 halfSize = float2(width, height) * 0.5;
                    float2 center = lerp(halfSize, screenSize - halfSize, randomCenter);
                    float2 edgeDistance = abs(pixel - center) - halfSize;
                    float outsideDistance = max(edgeDistance.x, edgeDistance.y);
                    float mask = 1.0 - smoothstep(0.0, max(_EdgeSoftnessPixels, 0.001), outsideDistance);

                    if (mask > rectangleMask)
                    {
                        rectangleMask = mask;
                        rectangleIndex = id;
                        float rowIndex = floor(pixel.y / max(_LineHeightPixels, 1.0));
                        float horizontalShift = (Hash21(float2(rowIndex, eventSeed + id * 19.3)) * 2.0 - 1.0) * _HorizontalDisplacementPixels;
                        float verticalShift = (Hash21(float2(rowIndex + 53.2, eventSeed + id * 7.7)) * 2.0 - 1.0) * _VerticalDisplacementPixels;
                        displacedUv = saturate(uv + float2(horizontalShift, verticalShift) / screenSize);
                    }
                }

                float effectMask = rectangleMask * eventActive * intensity;
                if (effectMask <= 0.0001)
                    return original;

                float2 splitOffset = float2(_RGBSplitPixels / screenSize.x, 0.0) * effectMask;
                half4 centerColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, displacedUv);
                float3 color;
                color.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(displacedUv + splitOffset)).r;
                color.g = centerColor.g;
                color.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(displacedUv - splitOffset)).b;

                float lineNoise = Hash21(float2(floor(pixel.y), eventSeed + rectangleIndex * 31.2)) - 0.5;
                float scanline = sin(pixel.y * 3.14159265) * 0.5 + 0.5;
                color += lineNoise * _NoiseIntensity * effectMask;
                color *= 1.0 - scanline * _ScanlineIntensity * effectMask;
                color = lerp(color, 1.0.xxx, _FlashIntensity * effectMask);
                return half4(saturate(lerp(original.rgb, color, effectMask)), original.a);
            }
            ENDHLSL
        }
    }
}
