// Auto-split per modifier shader. Source behavior derived from UI/MaterialModifierScreenEffect.
// 懒惰箭头：纵向波浪形拖拽变形。
Shader "UI/MaterialModifiers/LazyModifier"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _AuraColor ("Aura Color", Color) = (1,1,1,1)
        _EffectSpeed ("Effect Speed", Float) = 1
        _EffectStrength ("Effect Strength", Range(0,1)) = 0.3
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
            #pragma target 2.0
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

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            fixed4 _AuraColor;
            float4 _ClipRect;
            float _EffectSpeed;
            float _EffectStrength;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float InsideUv(float2 uv)
            {
                return step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0);
            }

            fixed4 SampleMain(float2 uv, fixed4 vertexColor)
            {
                float inside = InsideUv(uv);
                fixed4 color = (tex2D(_MainTex, saturate(uv)) + _TextureSampleAdd) * vertexColor;
                color.a *= inside;
                return color;
            }

            float2 SwirlUv(float2 uv, float amount)
            {
                float2 centered = uv - 0.5;
                float radius = length(centered);
                float angle = atan2(centered.y, centered.x);
                angle += (1.0 - saturate(radius * 2.0)) * amount;
                return 0.5 + float2(cos(angle), sin(angle)) * radius;
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
                float mode = 4.0;
                fixed4 color = SampleMain(uv, IN.color);

                if (mode < 0.5)
                {
                    float lineId = floor(uv.y * 90.0);
                    float stepTime = floor(_Time.y * _EffectSpeed * 12.0);
                    float glitch = step(hash21(float2(lineId, stepTime)), 0.08 + _EffectStrength * 0.08);
                    float shift = (hash21(float2(lineId, stepTime + 17.0)) * 2.0 - 1.0) * 0.022 * glitch;
                    fixed4 baseColor = SampleMain(uv + float2(shift, 0.0), IN.color);
                    fixed4 red = SampleMain(uv + float2(0.006, 0.0), IN.color);
                    fixed4 cyan = SampleMain(uv - float2(0.006, 0.0), IN.color);
                    float scan = sin(uv.y * 720.0 + _Time.y * _EffectSpeed * 8.0) * 0.5 + 0.5;
                    baseColor.rgb = lerp(baseColor.rgb, baseColor.rgb * (0.72 + _EffectStrength * 0.16), scan);
                    baseColor.rgb += red.r * float3(0.32, 0.02, 0.02) + cyan.b * float3(0.02, 0.18, 0.32);
                    baseColor.rgb = lerp(baseColor.rgb, _AuraColor.rgb, baseColor.a * 0.12);
                    color = baseColor;
                }
                else if (mode < 1.5)
                {
                    float pulse = (sin(_Time.y * _EffectSpeed * 2.0) * 0.5 + 0.5) * _EffectStrength;
                    float3 inverted = 1.0 - color.rgb;
                    color.rgb = lerp(color.rgb, inverted, pulse);
                    float loop = 1.0 - smoothstep(0.0, 0.02, abs(frac(uv.y + _Time.y * _EffectSpeed * 0.25) - 0.5));
                    color.rgb += _AuraColor.rgb * loop * color.a * 0.45;
                }
                else if (mode < 2.5)
                {
                    float gray = dot(color.rgb, float3(0.299, 0.587, 0.114));
                    float noise = hash21(floor((uv + _Time.y * 0.04) * 24.0));
                    float stripe = step(0.58, frac(uv.y * 18.0 + _Time.y * _EffectSpeed));
                    color.rgb = lerp(color.rgb, float3(gray, gray, gray) * 0.72, 0.86);
                    color.rgb = lerp(color.rgb, float3(0.05, 0.05, 0.06), stripe * color.a * 0.22);
                    color.rgb += (noise - 0.5) * 0.08 * _EffectStrength;
                    color.rgb = lerp(color.rgb, _AuraColor.rgb * 0.45, color.a * 0.12);
                }
                else if (mode < 3.5)
                {
                    float breath = sin(_Time.y * _EffectSpeed * 2.0) * 0.5 + 0.5;
                    color.a *= lerp(0.56, 1.0, breath);
                    color.rgb = lerp(color.rgb, _AuraColor.rgb, color.a * breath * 0.28);
                }
                else if (mode < 4.5)
                {
                    float wave = sin(uv.x * 18.0 + _Time.y * _EffectSpeed * 3.0);
                    float2 warpedUv = uv + float2(0.0, wave * 0.022 * max(_EffectStrength, 0.25));
                    color = SampleMain(warpedUv, IN.color);
                    float wobbleLine = abs(frac(uv.x * 3.0 - _Time.y * _EffectSpeed * 0.35) - 0.5);
                    color.rgb = lerp(color.rgb, _AuraColor.rgb, (1.0 - smoothstep(0.0, 0.28, wobbleLine)) * color.a * 0.22);
                }
                else
                {
                    float swirlAmount = sin(_Time.y * _EffectSpeed) * 0.8 + 1.2;
                    float2 swirlUv = SwirlUv(uv, swirlAmount * max(_EffectStrength, 0.25));
                    color = SampleMain(swirlUv, IN.color);
                    float2 centered = uv - 0.5;
                    float radius = length(centered);
                    float ring = sin(radius * 36.0 - _Time.y * _EffectSpeed * 5.0) * 0.5 + 0.5;
                    float centerGlow = 1.0 - smoothstep(0.04, 0.48, radius);
                    color.rgb = lerp(color.rgb, _AuraColor.rgb, ring * centerGlow * color.a * 0.42);
                }

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return saturate(color);
            }
            ENDCG
        }
    }
}
