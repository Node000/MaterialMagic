Shader "Style/Scribble/ModelEdgeScribble"
{
    Properties
    {
        [MainColor] _Tint ("Tint", Color) = (1,0.32,0.12,1)
        _WobbleAmplitude ("Wobble Amplitude", Float) = 0.012
        _WobbleFrequency ("Wobble Frequency", Float) = 5
        _WobbleSpeed ("Wobble Speed", Float) = 0.7
        _SteppedAnimation ("Stepped Animation", Range(0,1)) = 1
        _AnimationFramesPerSecond ("Animation Frames Per Second", Range(1,30)) = 12
        _SecondaryWobble ("Secondary Wobble", Range(0,1)) = 0.35
        _Seed ("Seed", Float) = 17
        _Opacity ("Opacity", Range(0,1)) = 1
        [HideInInspector] _PreviewAnimationTime ("Preview Animation Time", Float) = -1
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
            Name "ModelEdgeScribble"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _WobbleAmplitude;
                float _WobbleFrequency;
                float _WobbleSpeed;
                float _SteppedAnimation;
                float _AnimationFramesPerSecond;
                float _SecondaryWobble;
                float _Seed;
                float _Opacity;
                float _PreviewAnimationTime;
            CBUFFER_END

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
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float animationTime = _PreviewAnimationTime >= 0.0 ? _PreviewAnimationTime : _Time.y;
                if (_SteppedAnimation > 0.5)
                    animationTime = floor(animationTime * _AnimationFramesPerSecond) / _AnimationFramesPerSecond;

                float phase = input.uv.x * _WobbleFrequency + input.uv.y * 2.0 + _Seed * 0.173;
                float primary = sin((phase + animationTime * _WobbleSpeed) * ScribbleTwoPi);
                float secondary = sin((phase * 1.83 - animationTime * _WobbleSpeed * 1.37 + _Seed * 0.419) * ScribbleTwoPi);
                input.positionOS.xyz += input.normalOS * (primary + secondary * _SecondaryWobble) * _WobbleAmplitude;

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Tint;
                output.color.a *= _Opacity;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.color.a - 0.001h);
                return input.color;
            }
            ENDHLSL
        }
    }
}
