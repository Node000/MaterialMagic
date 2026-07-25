Shader "Hidden/Style/CrayonScreenFilter"
{
    Properties
    {
        _Intensity ("Filter Intensity", Range(0, 1)) = 0.65
        _ColorSteps ("Color Steps", Range(2, 24)) = 8
        _PaperGrainStrength ("Paper Grain Strength", Range(0, 1)) = 0.18
        _PaperGrainScale ("Paper Grain Scale", Range(20, 800)) = 260
        _StrokeStrength ("Crayon Stroke Strength", Range(0, 1)) = 0.35
        _StrokeScale ("Crayon Stroke Scale", Range(8, 200)) = 72
        _StrokeAngle ("Crayon Stroke Angle", Range(0, 180)) = 18
        _Saturation ("Saturation", Range(0, 2)) = 1.08
        _Contrast ("Contrast", Range(0, 2)) = 1.12
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "CrayonScreenFilter"
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
                float _ColorSteps;
                float _PaperGrainStrength;
                float _PaperGrainScale;
                float _StrokeStrength;
                float _StrokeScale;
                float _StrokeAngle;
                float _Saturation;
                float _Contrast;
            CBUFFER_END

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float ValueNoise(float2 value)
            {
                float2 cell = floor(value);
                float2 local = frac(value);
                local = local * local * (3.0 - 2.0 * local);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + 1.0);
                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            float2 Rotate(float2 value, float degrees)
            {
                float angleRadians = degrees * 0.01745329252;
                float sine = sin(angleRadians);
                float cosine = cos(angleRadians);
                return float2(cosine * value.x - sine * value.y, sine * value.x + cosine * value.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                uv.y = 1.0 - uv.y;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float3 color = source.rgb;

                float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                float3 saturated = lerp(luminance.xxx, color, _Saturation);
                float3 contrasted = saturate((saturated - 0.5) * _Contrast + 0.5);
                float steps = max(2.0, round(_ColorSteps));
                float3 quantized = floor(contrasted * (steps - 1.0) + 0.5) / (steps - 1.0);

                float grain = ValueNoise(uv * _PaperGrainScale) - 0.5;
                float2 strokeUv = Rotate(uv - 0.5, _StrokeAngle) * _StrokeScale;
                float coarseStroke = ValueNoise(float2(strokeUv.x * 0.16, strokeUv.y));
                float fineStroke = ValueNoise(float2(strokeUv.x * 0.55, strokeUv.y * 2.7));
                float stroke = (coarseStroke * 0.65 + fineStroke * 0.35 - 0.5) * (0.45 + luminance * 0.55);

                float3 crayon = quantized + grain * _PaperGrainStrength + stroke * _StrokeStrength;
                return half4(lerp(color, saturate(crayon), _Intensity), source.a);
            }
            ENDHLSL
        }
    }
}
