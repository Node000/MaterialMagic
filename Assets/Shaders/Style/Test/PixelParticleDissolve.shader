Shader "Style/Test/PixelParticleDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _DissolveProgress ("Dissolve Progress", Range(0,1)) = 0
        _ParticleCount ("Particle Count", Range(8,80)) = 25
        _DissolveTex ("Dissolve Noise", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Float) = 1
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashStrength ("Flash Strength", Range(0,2)) = 1.2
        [Enum(Random,0,Directional,1)] _DissolveMode ("Dissolve Start Mode", Float) = 1
        _DissolveDirection ("Dissolve Start Direction", Vector) = (0,1,0,0)
        _DirectionalNoiseStrength ("Directional Noise Strength", Range(0,1)) = 0.65
        _CoreColor ("Core Color", Color) = (0.95,0.35,0.65,1)
        _ParticleColor ("Particle Color", Color) = (0.35,0.84,1,1)
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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; float4 worldPosition : TEXCOORD1; };
            sampler2D _MainTex;
            sampler2D _DissolveTex;
            fixed4 _Color, _FlashColor, _CoreColor, _ParticleColor;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _DissolveProgress, _ParticleCount, _NoiseScale, _FlashStrength, _DissolveMode, _DirectionalNoiseStrength;
            float4 _DissolveDirection;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.texcoord = v.texcoord;
                o.worldPosition = v.vertex;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 source = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;
                float progress = saturate(_DissolveProgress);
                if (progress <= 0.001)
                    return source;

                float count = _ParticleCount;
                float2 cell = floor(i.texcoord * count);
                float2 cellCenter = (cell + 0.5) / count;
                float2 cellUv = frac(i.texcoord * count);
                float2 centeredCellUv = cellUv - 0.5;
                float dissolveNoise = tex2D(_DissolveTex, cellCenter * _NoiseScale).r;
                float2 startDirection = _DissolveDirection.xy;
                startDirection /= max(length(startDirection), 0.001);
                float directionalOrder = dot(cellCenter - 0.5, startDirection) * 0.5 + 0.5;
                float randomOrder = hash21(cell + 31.7);
                float directionalNoise = lerp(0.5, dissolveNoise, _DirectionalNoiseStrength);
                float directionalThreshold = saturate(directionalOrder + (directionalNoise - 0.5) * 0.65);
                float dissolveOrder = lerp(randomOrder, directionalThreshold, step(0.5, _DissolveMode));
                float startThreshold = dissolveOrder;
                float localProgress = saturate((progress - startThreshold) / 0.34);
                float particleWeight = smoothstep(0.0, 0.025, localProgress);

                if (particleWeight <= 0.001)
                    return source;

                float random = hash21(cell);
                float2 direction = _DissolveDirection.xy;
                direction /= max(length(direction), 0.001);
                float flash = 1.0 - smoothstep(0.16, 0.48, localProgress);
                float shrinkProgress = smoothstep(0.34, 1.0, localProgress);
                float contraction = lerp(1.0, 0.04 + random * 0.08, shrinkProgress);
                float2 particleCenter = 0.5 + direction * (0.16 * shrinkProgress);
                float2 particleUv = cellUv - particleCenter;
                float edgeDistance = max(abs(particleUv.x), abs(particleUv.y)) / max(0.5 * contraction, 0.001);
                float shape = 1.0 - smoothstep(0.90, 1.0, edgeDistance);
                float fade = 1.0 - smoothstep(0.46, 1.0, localProgress);
                fixed4 particle = (tex2D(_MainTex, cellCenter) + _TextureSampleAdd) * i.color;
                float3 particleRgb = _FlashColor.rgb;
                float3 rgb = lerp(source.rgb, particleRgb, particleWeight);
                float alpha = source.a * lerp(1.0, shape * fade, particleWeight);
                if (progress >= 0.999)
                    alpha = 0.0;

                fixed4 color = fixed4(rgb, alpha);
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
