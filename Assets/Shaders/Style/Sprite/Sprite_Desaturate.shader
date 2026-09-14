Shader "Style/Sprite/Desaturate"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        _GrayAmount ("Gray Amount", Range(0,1)) = 0
        _GrayTint ("Gray Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        float4 _MainTex_ST;
        float4 _Color;
        half4 _RendererColor;
        float _GrayAmount;
        half4 _GrayTint;

        struct DesaturateAttributes
        {
            float3 positionOS : POSITION;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct DesaturateVaryings
        {
            float4 positionCS : SV_POSITION;
            half4 color : COLOR;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        DesaturateVaryings DesaturateVertex(DesaturateAttributes input)
        {
            DesaturateVaryings output = (DesaturateVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            #ifdef UNITY_INSTANCING_ENABLED
            input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteFlip);
            #endif

            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            output.color = input.color * _Color * _RendererColor;

            #ifdef UNITY_INSTANCING_ENABLED
            output.color *= unity_SpriteColor;
            #endif

            return output;
        }

        half4 DesaturateFragment(DesaturateVaryings input) : SV_Target
        {
            half4 source = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            half luma = dot(source.rgb, half3(0.299, 0.587, 0.114));
            half3 gray = luma * _GrayTint.rgb;
            source.rgb = lerp(source.rgb, gray, saturate(_GrayAmount));
            return source;
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex DesaturateVertex
            #pragma fragment DesaturateFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent" }

            HLSLPROGRAM
            #pragma vertex DesaturateVertex
            #pragma fragment DesaturateFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
