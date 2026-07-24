Shader "Hidden/Style/ScreenEdgeScribbleComposite"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1,0.32,0.12,1)
        _OutlineWidth ("Outline Width", Range(0.5,8)) = 2
        _NormalThreshold ("Normal Threshold", Range(0,1)) = 0.18
        _WobbleAmplitude ("Wobble Amplitude", Range(0,1)) = 0.3
        _WobbleFrequency ("Wobble Frequency", Range(1,32)) = 9
        _WobbleSpeed ("Wobble Speed", Range(0,8)) = 1
        _SteppedAnimation ("Stepped Animation", Range(0,1)) = 1
        _AnimationFramesPerSecond ("Animation Frames Per Second", Range(1,30)) = 12
        _Seed ("Seed", Float) = 17
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "ScreenEdgeScribbleComposite"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ScreenEdgeScribbleMask);
            SAMPLER(sampler_ScreenEdgeScribbleMask);
            float4 _ScreenEdgeScribbleMask_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _NormalThreshold;
                float _WobbleAmplitude;
                float _WobbleFrequency;
                float _WobbleSpeed;
                float _SteppedAnimation;
                float _AnimationFramesPerSecond;
                float _Seed;
            CBUFFER_END

            static const float ScribbleTwoPi = 6.28318530718;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(output.uv * 2.0 - 1.0, 0.0, 1.0);
                return output;
            }

            float SampleCoverage(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_ScreenEdgeScribbleMask, sampler_ScreenEdgeScribbleMask, uv).a;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float animationTime = _Time.y;
                if (_SteppedAnimation > 0.5)
                    animationTime = floor(animationTime * _AnimationFramesPerSecond) / _AnimationFramesPerSecond;

                float2 texel = _ScreenEdgeScribbleMask_TexelSize.xy * _OutlineWidth;
                float2 wobble = float2(
                    sin((input.uv.y * _WobbleFrequency + animationTime * _WobbleSpeed + _Seed * 0.17) * ScribbleTwoPi),
                    sin((input.uv.x * _WobbleFrequency - animationTime * _WobbleSpeed * 1.37 + _Seed * 0.41) * ScribbleTwoPi))
                    * texel * _WobbleAmplitude;

                float4 center = SAMPLE_TEXTURE2D(_ScreenEdgeScribbleMask, sampler_ScreenEdgeScribbleMask, input.uv);
                float coverage = center.a;
                float4 right = SAMPLE_TEXTURE2D(_ScreenEdgeScribbleMask, sampler_ScreenEdgeScribbleMask, input.uv + float2(texel.x, 0) + wobble);
                float4 left = SAMPLE_TEXTURE2D(_ScreenEdgeScribbleMask, sampler_ScreenEdgeScribbleMask, input.uv - float2(texel.x, 0) + wobble);
                float4 up = SAMPLE_TEXTURE2D(_ScreenEdgeScribbleMask, sampler_ScreenEdgeScribbleMask, input.uv + float2(0, texel.y) + wobble);
                float4 down = SAMPLE_TEXTURE2D(_ScreenEdgeScribbleMask, sampler_ScreenEdgeScribbleMask, input.uv - float2(0, texel.y) + wobble);

                float silhouette = max(max(abs(coverage - right.a), abs(coverage - left.a)), max(abs(coverage - up.a), abs(coverage - down.a)));
                float normalDifference = max(max(length(center.rgb - right.rgb), length(center.rgb - left.rgb)), max(length(center.rgb - up.rgb), length(center.rgb - down.rgb)));
                float fold = coverage * step(_NormalThreshold, normalDifference);
                float edge = saturate(max(silhouette, fold));

                half4 color = _OutlineColor;
                color.a *= edge;
                return color;
            }
            ENDHLSL
        }
    }
}
