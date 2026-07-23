Shader "Style/Scribble/Ribbon3D"
{
    Properties
    {
        [MainColor] _Tint ("Tint", Color) = (1,1,1,1)
        _BrushTex ("Brush Tex", 2D) = "white" {}
        _BreakupTex ("Breakup Tex", 2D) = "white" {}
        _BrushTiling ("Brush Tiling", Vector) = (1,1,0,0)
        _BreakupStrength ("Breakup Strength", Range(0,1)) = 0
        _BreakupThreshold ("Breakup Threshold", Range(0,1)) = 0.5
        _WobbleAmplitude ("Wobble Amplitude", Float) = 0
        _WobbleFrequency ("Wobble Frequency", Float) = 3
        _WobbleSpeed ("Wobble Speed", Float) = 0
        _SecondaryWobble ("Secondary Wobble", Range(0,1)) = 0.35
        _Seed ("Seed", Float) = 0
        _Opacity ("Opacity", Range(0,1)) = 1
        _DepthOffset ("Depth Offset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "ScribbleRibbon"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float4 _BrushTiling;
                float _BreakupStrength;
                float _BreakupThreshold;
                float _WobbleAmplitude;
                float _WobbleFrequency;
                float _WobbleSpeed;
                float _SecondaryWobble;
                float _Seed;
                float _Opacity;
                float _DepthOffset;
            CBUFFER_END

            TEXTURE2D(_BrushTex);
            SAMPLER(sampler_BrushTex);
            TEXTURE2D(_BreakupTex);
            SAMPLER(sampler_BreakupTex);

            static const float ScribbleTwoPi = 6.28318530718;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float phase = input.uv.x * _WobbleFrequency + _Seed * 0.173;
                float primaryWobble = sin((phase + _Time.y * _WobbleSpeed) * ScribbleTwoPi);
                float secondaryWobble = sin((phase * 1.83 - _Time.y * _WobbleSpeed * 1.37 + _Seed * 0.419) * ScribbleTwoPi);
                float3 tangentDirection = normalize(input.tangentOS.xyz);
                input.positionOS.xyz += tangentDirection * (primaryWobble + secondaryWobble * _SecondaryWobble) * _WobbleAmplitude;
                input.positionOS.xyz += input.normalOS * _DepthOffset;

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 textureUv = input.uv * _BrushTiling.xy + _BrushTiling.zw;
                half4 brush = SAMPLE_TEXTURE2D(_BrushTex, sampler_BrushTex, textureUv);
                half breakupSample = SAMPLE_TEXTURE2D(_BreakupTex, sampler_BreakupTex, textureUv).r;
                half breakupMask = smoothstep(_BreakupThreshold - 0.04h, _BreakupThreshold + 0.04h, breakupSample);
                half breakup = lerp(1.0h, breakupMask, _BreakupStrength);

                half4 color = brush * input.color * _Tint;
                color.a *= breakup * _Opacity;
                clip(color.a - 0.001h);
                return color;
            }
            ENDHLSL
        }
    }
}
