Shader "Hidden/Style/CrtScanLine"
{
    Properties
    {
        _Intensity ("Effect Intensity", Range(0, 1)) = 1
        _ScanColor ("Scan Color", Color) = (0.16, 0.56, 0.28, 1)
        _ScanSpeed ("Scan Speed", Range(0.01, 2)) = 0.16
        _ScanLineWidthPixels ("Scan Line Width (Pixels)", Range(1, 32)) = 2
        _ScanGlowWidthPixels ("Scan Glow Width (Pixels)", Range(2, 240)) = 58
        _ScanGlowIntensity ("Scan Glow Intensity", Range(0, 1)) = 0.16
        _DistortionWidthPixels ("Distortion Band Width (Pixels)", Range(2, 240)) = 38
        _HorizontalDisplacementPixels ("Horizontal Displacement (Pixels)", Range(0, 96)) = 10
        _DistortionFrequency ("Distortion Frequency", Range(0.1, 60)) = 8
        _StaticScanlineDensity ("Static Scanline Density", Range(0, 1200)) = 480
        _StaticScanlineIntensity ("Static Scanline Intensity", Range(0, 0.5)) = 0.06
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "CrtScanLine"
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
                float4 _ScanColor;
                float _ScanSpeed;
                float _ScanLineWidthPixels;
                float _ScanGlowWidthPixels;
                float _ScanGlowIntensity;
                float _DistortionWidthPixels;
                float _HorizontalDisplacementPixels;
                float _DistortionFrequency;
                float _StaticScanlineDensity;
                float _StaticScanlineIntensity;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                float intensity = saturate(_Intensity);
                float2 uv = input.texcoord;
                uv.y = 1.0 - uv.y;
                float2 screenSize = max(_ScreenParams.xy, 1.0);
                float scanCenter = frac(_Time.y * _ScanSpeed);
                float distanceToScanPixels = abs(frac(uv.y - scanCenter + 0.5) - 0.5) * screenSize.y;
                float distortionBand = 1.0 - smoothstep(_DistortionWidthPixels, _DistortionWidthPixels + 8.0, distanceToScanPixels);
                float scanGlow = 1.0 - smoothstep(_ScanGlowWidthPixels, _ScanGlowWidthPixels + 12.0, distanceToScanPixels);
                float scanCore = 1.0 - smoothstep(_ScanLineWidthPixels, _ScanLineWidthPixels + 1.0, distanceToScanPixels);
                float rowPhase = uv.y * _DistortionFrequency * 6.2831853 + _Time.y * 18.0;
                float horizontalOffset = (sin(rowPhase) * 0.65 + sin(rowPhase * 2.17 + 1.4) * 0.35) * _HorizontalDisplacementPixels;
                float2 displacedUv = saturate(uv + float2(horizontalOffset * distortionBand * intensity / screenSize.x, 0.0));
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, displacedUv);
                float staticScanline = sin(uv.y * _StaticScanlineDensity * 6.2831853) * 0.5 + 0.5;
                float3 color = source.rgb * (1.0 - staticScanline * _StaticScanlineIntensity * intensity);
                color += _ScanColor.rgb * (scanGlow * _ScanGlowIntensity + scanCore * _ScanGlowIntensity) * intensity;
                return half4(color, source.a);
            }
            ENDHLSL
        }
    }
}
