Shader "UI/MaterialModifierElementAura"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _AuraColor ("Aura Color", Color) = (1,1,1,1)
        _EffectMode ("Effect Mode", Float) = 0
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
            float _EffectMode;
            float _EffectSpeed;
            float _EffectStrength;

            float hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 37.2);
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

            float SegmentDistance(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / max(dot(ba, ba), 0.0001));
                return length(pa - ba * h);
            }

            float Lightning(float2 uv)
            {
                float bolt = 0.0;
                float time = floor(_Time.y * _EffectSpeed * 7.0);
                for (int i = 0; i < 4; i++)
                {
                    float y0 = i * 0.24 + 0.06;
                    float y1 = y0 + 0.2;
                    float x0 = 0.12 + hash21(float2(i, time)) * 0.76;
                    float x1 = 0.12 + hash21(float2(i + 6.0, time)) * 0.76;
                    float distance = SegmentDistance(uv, float2(x0, y0), float2(x1, y1));
                    bolt = max(bolt, 1.0 - smoothstep(0.006, 0.03, distance));
                }
                return bolt;
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
                float mode = _EffectMode;
                float2 centered = abs(uv - 0.5) * 2.0;
                float edgeDistance = max(centered.x, centered.y);
                fixed4 color = SampleMain(uv, IN.color);
                float sourceAlpha = color.a;
                float edge = smoothstep(0.42, 1.0, edgeDistance) * sourceAlpha;

                if (mode < 0.5)
                {
                    float sweep = 1.0 - smoothstep(0.0, 0.09, abs(frac((uv.x + uv.y) * 0.65 - _Time.y * _EffectSpeed * 0.32) - 0.5));
                    float3 metal = lerp(color.rgb * 0.78, float3(0.96, 0.9, 0.72), sweep * sourceAlpha);
                    color.rgb = lerp(metal, _AuraColor.rgb, edge * 0.28);
                }
                else if (mode < 1.5)
                {
                    float pixelY = floor(uv.y * 18.0) / 18.0;
                    float pixelX = floor(uv.x * 22.0) / 22.0;
                    float flameNoise = hash21(float2(pixelX * 17.0, pixelY * 19.0 + floor(_Time.y * _EffectSpeed * 10.0)));
                    float flameShape = (1.0 - smoothstep(0.18, 0.84, uv.y)) * smoothstep(0.0, 0.55, uv.y);
                    float flame = step(0.48 + uv.y * 0.28, flameNoise) * flameShape;
                    float3 flameColor = lerp(float3(1.0, 0.25, 0.05), float3(1.0, 0.86, 0.18), flameNoise);
                    color.rgb = lerp(color.rgb, flameColor, flame * max(sourceAlpha, 0.35) * 0.55);
                    color.rgb += _AuraColor.rgb * edge * 0.25;
                }
                else if (mode < 2.5)
                {
                    float bolt = Lightning(uv);
                    float flicker = 0.55 + hash21(float2(floor(_Time.y * _EffectSpeed * 12.0), 4.0)) * 0.45;
                    color.rgb = lerp(color.rgb, _AuraColor.rgb, saturate((bolt * flicker + edge * 0.25) * sourceAlpha));
                    color.rgb += _AuraColor.rgb * bolt * 0.4;
                    color.a = max(color.a, bolt * 0.42 * _AuraColor.a);
                }
                else if (mode < 3.5)
                {
                    float wave = sin(uv.y * 24.0 + _Time.y * _EffectSpeed * 4.0) * 0.5 + 0.5;
                    float current = 1.0 - smoothstep(0.0, 0.12, abs(frac(uv.x * 1.4 - _Time.y * _EffectSpeed * 0.35) - 0.5));
                    color.rgb = lerp(color.rgb, _AuraColor.rgb, (wave * 0.18 + current * 0.32) * sourceAlpha);
                }
                else
                {
                    float drop = smoothstep(0.2, 1.0, uv.y) * (sin(uv.x * 32.0 + _Time.y * _EffectSpeed * 3.0) * 0.5 + 0.5);
                    float wobble = sin(uv.y * 18.0 + _Time.y * _EffectSpeed * 2.5) * 0.015 * _EffectStrength;
                    fixed4 warped = SampleMain(uv + float2(wobble, 0.0), IN.color);
                    color = warped;
                    color.rgb = lerp(color.rgb, _AuraColor.rgb, (drop * 0.22 + edge * 0.18) * color.a);
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
