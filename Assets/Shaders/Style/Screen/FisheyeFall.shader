Shader "Hidden/Style/FisheyeFall"
{
    Properties
    {
        _Intensity ("Effect Intensity", Range(0, 1)) = 0
        _FisheyeStrength ("Fisheye Strength", Range(0, 1)) = 0.38
        _Zoom ("Zoom", Range(1, 1.5)) = 1.16
        _RadialBlurPixels ("Radial Blur Pixels", Range(0, 24)) = 5
        _EdgeVignette ("Edge Vignette", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "FisheyeFall"
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
                float _FisheyeStrength;
                float _Zoom;
                float _RadialBlurPixels;
                float _EdgeVignette;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                float intensity = saturate(_Intensity);
                float2 uv = input.texcoord;
                uv.y = 1.0 - uv.y;
                float2 centeredUv = uv - 0.5;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 aspectCenteredUv = centeredUv * float2(aspect, 1.0);
                float radiusSquared = dot(aspectCenteredUv, aspectCenteredUv);
                float zoom = lerp(1.0, _Zoom, intensity);
                float distortion = 1.0 + radiusSquared * _FisheyeStrength * intensity;
                float2 distortedUv = 0.5 + centeredUv * distortion / zoom;
                float2 radialDirection = centeredUv * (1.0 + radiusSquared * _FisheyeStrength * intensity);
                float2 blurStep = radialDirection * (_RadialBlurPixels * intensity / max(_ScreenParams.x, 1.0));
                half4 color = 0;
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(distortedUv - blurStep * 0.5));
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(distortedUv));
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(distortedUv + blurStep * 0.5));
                color *= 0.3333333;
                float vignette = 1.0 - smoothstep(0.3, 0.72, sqrt(radiusSquared)) * _EdgeVignette * intensity;
                return half4(color.rgb * vignette, color.a);
            }
            ENDHLSL
        }
    }
}
