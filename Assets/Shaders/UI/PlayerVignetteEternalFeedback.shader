Shader "UI/PlayerVignetteEternalFeedback"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _VignetteColor ("Vignette Color", Color) = (1, 0.85, 0.42, 0)

        [Header(Vignette Shape)]
        _InnerRadius ("Inner Radius", Float) = 0.3
        _OuterRadius ("Outer Radius", Float) = 1.0
        _AspectScale ("Aspect Scale", Float) = 1.777
        _EdgePower ("Edge Power", Float) = 1.0
        _LineCount ("Line Count", Float) = 60.0
        _Randomness ("Randomness", Range(0.0, 1.0)) = 0.6
        _FlickerSpeed ("Flicker Speed", Float) = 0.0

        [Header(Eternal Datamosh)]
        _AuraColor ("Aura Color", Color) = (1, 0.85, 0.42, 1)
        _EffectStrength ("Effect Strength", Range(0.0, 1.0)) = 0.65
        _EffectSpeed ("Effect Speed", Float) = 3.0
        _MoshIntensity ("Mosh Intensity", Range(0.0, 1.0)) = 0.36
        _BlockSize ("Block Size", Float) = 24.0
        _MoshFPS ("Mosh FPS", Float) = 4.0
        _SmearLength ("Smear Length", Range(0.0, 0.5)) = 0.09
        _ChromaShift ("Chroma Shift", Range(0.0, 0.1)) = 0.01
        _IdleSpeed ("Idle Speed", Float) = 5.0
        _IdleAmplitude ("Idle Amplitude", Range(0.0, 0.2)) = 0.05

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _VignetteColor;
            fixed4 _AuraColor;
            float4 _ClipRect;

            float _InnerRadius;
            float _OuterRadius;
            float _AspectScale;
            float _EdgePower;
            float _LineCount;
            float _Randomness;
            float _FlickerSpeed;

            float _EffectStrength;
            float _EffectSpeed;
            float _MoshIntensity;
            float _BlockSize;
            float _MoshFPS;
            float _SmearLength;
            float _ChromaShift;
            float _IdleSpeed;
            float _IdleAmplitude;

            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float2 hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float lineMask(float2 uv)
            {
                float2 centerUV = uv - 0.5;
                centerUV.x *= _AspectScale;

                float r = length(centerUV);
                float angle = atan2(centerUV.y, centerUV.x);
                float normAngle = (angle + 3.14159265) / (2.0 * 3.14159265);
                float lineCount = max(1.0, _LineCount);
                float sectorId = floor(normAngle * lineCount);
                float localAngle = frac(normAngle * lineCount);
                float timeOffset = floor(_Time.y * _FlickerSpeed);
                float rnd = hash12(float2(sectorId + timeOffset, 7.13));

                float spikeShape = 1.0 - abs(localAngle - 0.5) * 2.0;
                spikeShape = pow(max(0.001, spikeShape), max(0.1, _EdgePower));
                float spikeLength = max(0.01, _OuterRadius - _InnerRadius);
                float currentInnerLimit = _InnerRadius + rnd * spikeLength * _Randomness;
                float lineStartRadius = currentInnerLimit + (1.0 - spikeShape) * spikeLength;
                return step(lineStartRadius, r);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float2 gridUV = floor(uv * max(1.0, _BlockSize)) / max(1.0, _BlockSize);
                float tGrid = floor(_Time.y * max(0.1, _MoshFPS));
                float blockNoise = hash12(gridUV + tGrid);
                float isMoshed = step(1.0 - saturate(_MoshIntensity), blockNoise);
                float2 randomDir = hash22(gridUV + tGrid * 0.5) * 2.0 - 1.0;

                float idleTime = _Time.y * _IdleSpeed;
                float2 idleOffset = float2(sin(idleTime), cos(idleTime * 0.73)) * _IdleAmplitude;
                float2 smearOffset = (randomDir * 0.5 - idleOffset * 2.0) * _SmearLength * isMoshed * saturate(_EffectStrength);
                float2 shiftedUV = uv + smearOffset;
                float2 chromaVec = randomDir * _ChromaShift * isMoshed * saturate(_EffectStrength);

                float maskR = lineMask(shiftedUV + chromaVec);
                float maskG = lineMask(shiftedUV);
                float maskB = lineMask(shiftedUV - chromaVec);
                float mask = max(maskR, max(maskG, maskB));

                float pulse = 0.5 + 0.5 * sin(_Time.y * _EffectSpeed * 6.28318);
                float blockFlash = lerp(0.78, 1.18, saturate(blockNoise + pulse * 0.25));
                float3 baseColor = lerp(_VignetteColor.rgb, _AuraColor.rgb, saturate(0.35 + isMoshed * 0.45 + pulse * 0.2) * saturate(_EffectStrength));
                float3 chromaColor = float3(baseColor.r * maskR, baseColor.g * maskG, baseColor.b * maskB);

                fixed4 finalColor;
                finalColor.rgb = chromaColor * blockFlash;
                finalColor.a = _VignetteColor.a * mask * IN.color.a * lerp(0.75, 1.15, pulse) * lerp(1.0, blockFlash, 0.35);

                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(finalColor.a - 0.001);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}
