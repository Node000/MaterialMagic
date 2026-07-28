Shader "Hidden/Style/PvOnlyVhsFall"
{
    Properties
    {
        _Intensity ("Fall VHS Intensity", Range(0, 1)) = 0
        _RGBSplitPixels ("RGB Split Pixels", Range(0, 24)) = 5
        _NoiseAmount ("Noise Amount", Range(0, 1)) = 0.2
        _NoiseScale ("Noise Scale", Range(1, 12)) = 4
        _LineShiftPixels ("Line Shift Pixels", Range(0, 48)) = 10
        _LineDensity ("Line Density", Range(8, 240)) = 72
        _WarpAmount ("Warp Amount", Range(0, 0.08)) = 0.008
        _WarpFrequency ("Warp Frequency", Range(0.1, 24)) = 6
        _WarpSpeed ("Warp Speed", Range(0, 16)) = 2
        _ScanlineIntensity ("Scanline Intensity", Range(0, 0.4)) = 0.06
        _TintColor ("Tint Color", Color) = (0.95, 0.24, 0.58, 1)
        _TintAmount ("Tint Amount", Range(0, 0.5)) = 0.05
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "PvOnlyVhsFall"
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
                float _RGBSplitPixels;
                float _NoiseAmount;
                float _NoiseScale;
                float _LineShiftPixels;
                float _LineDensity;
                float _WarpAmount;
                float _WarpFrequency;
                float _WarpSpeed;
                float _ScanlineIntensity;
                float4 _TintColor;
                float _TintAmount;
            CBUFFER_END

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float intensity = saturate(_Intensity);
                float2 uv = input.texcoord;
                float2 texelSize = rcp(_ScreenParams.xy);
                float timeStep = floor(_Time.y * 30.0);

                float row = floor(uv.y * _LineDensity);
                float rowNoise = Hash21(float2(row, timeStep));
                float rowActivation = step(1.0 - intensity, rowNoise);
                float lineShift = (rowNoise * 2.0 - 1.0) * _LineShiftPixels * texelSize.x * rowActivation * intensity;
                float wave = sin(uv.y * _WarpFrequency * 6.2831853 + _Time.y * _WarpSpeed) * _WarpAmount * intensity;
                float blockNoise = Hash21(floor(uv * float2(72.0, 48.0) / max(_NoiseScale, 0.001)) + timeStep) - 0.5;
                float2 distortedUv = saturate(uv + float2(lineShift + wave + blockNoise * texelSize.x * 2.0 * intensity, 0));

                float2 colorSplit = float2(_RGBSplitPixels * texelSize.x * intensity, 0);
                half4 center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distortedUv);
                half4 right = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(distortedUv + colorSplit));
                half4 left = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(distortedUv - colorSplit));
                float3 color = float3(right.r, center.g, left.b);

                float grain = Hash21(floor(uv * _ScreenParams.xy / max(_NoiseScale, 0.001)) + timeStep * 17.0) - 0.5;
                color += grain * _NoiseAmount * intensity;

                float scanline = sin(uv.y * _ScreenParams.y * 0.75) * 0.5 + 0.5;
                color *= 1.0 - scanline * _ScanlineIntensity * intensity;
                color = lerp(color, color * _TintColor.rgb, _TintAmount * intensity);
                return half4(saturate(color), center.a);
            }
            ENDHLSL
        }
    }
}
