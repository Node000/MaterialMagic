// Auto-split per modifier shader. Source behavior derived from UI/MaterialModifierArrowShape.
// 易碎箭头：沿方向裂开，上下两片轻微错位并高亮裂缝。
Shader "UI/MaterialModifiers/FragileArrowModifier"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _AltTex1 ("Alt Texture 1", 2D) = "white" {}
        _AltTex2 ("Alt Texture 2", 2D) = "white" {}
        _AltTex3 ("Alt Texture 3", 2D) = "white" {}
        _AltTex4 ("Alt Texture 4", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _AuraColor ("Aura Color", Color) = (1,1,1,1)
        _ArrowDirection ("Arrow Direction", Float) = 0
        _CopyCount ("Copy Count", Float) = 2
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
            sampler2D _AltTex1;
            sampler2D _AltTex2;
            sampler2D _AltTex3;
            sampler2D _AltTex4;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            fixed4 _AuraColor;
            float4 _ClipRect;
            float _ArrowDirection;
            float _CopyCount;
            float _EffectSpeed;
            float _EffectStrength;

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

            fixed4 SampleTexture(sampler2D textureSampler, float2 uv, fixed4 vertexColor)
            {
                float inside = InsideUv(uv);
                fixed4 color = (tex2D(textureSampler, saturate(uv)) + _TextureSampleAdd) * vertexColor;
                color.a *= inside;
                return color;
            }

            fixed4 Over(fixed4 bottom, fixed4 top)
            {
                float alpha = top.a + bottom.a * (1.0 - top.a);
                float3 rgb = top.rgb * top.a + bottom.rgb * bottom.a * (1.0 - top.a);
                fixed4 result;
                result.rgb = alpha > 0.0001 ? rgb / alpha : 0;
                result.a = alpha;
                return result;
            }

            float CutValue(float2 uv)
            {
                float2 p = uv - 0.5;
                float offset = sin(_Time.y * _EffectSpeed) * 0.08 * max(_EffectStrength, 0.2);
                float value = p.x + p.y * 0.55;
                if (_ArrowDirection > 0.5 && _ArrowDirection < 1.5)
                    value = -p.x + p.y * 0.55;
                else if (_ArrowDirection > 1.5 && _ArrowDirection < 2.5)
                    value = p.y - p.x * 0.55;
                else if (_ArrowDirection > 2.5)
                    value = -p.y - p.x * 0.55;
                return value + offset;
            }

            fixed4 SampleRandomCycle(float2 uv, fixed4 vertexColor)
            {
                float phase = frac(_Time.y * _EffectSpeed * 0.55) * 4.0;
                fixed4 a = SampleTexture(_AltTex1, uv, vertexColor);
                fixed4 b = SampleTexture(_AltTex2, uv, vertexColor);
                fixed4 c = SampleTexture(_AltTex3, uv, vertexColor);
                fixed4 d = SampleTexture(_AltTex4, uv, vertexColor);
                fixed4 from = a;
                fixed4 to = b;
                if (phase >= 1.0 && phase < 2.0)
                {
                    from = b;
                    to = c;
                }
                else if (phase >= 2.0 && phase < 3.0)
                {
                    from = c;
                    to = d;
                }
                else if (phase >= 3.0)
                {
                    from = d;
                    to = a;
                }
                float blendAmount = smoothstep(0.15, 0.85, frac(phase));
                return lerp(from, to, blendAmount);
            }

            float SegmentDistance(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / max(dot(ba, ba), 0.0001));
                return length(pa - ba * h);
            }

            float ReturnMark(float2 uv)
            {
                float d = 10.0;
                d = min(d, SegmentDistance(uv, float2(0.30, 0.72), float2(0.72, 0.72)));
                d = min(d, SegmentDistance(uv, float2(0.30, 0.72), float2(0.30, 0.36)));
                d = min(d, SegmentDistance(uv, float2(0.30, 0.72), float2(0.43, 0.84)));
                d = min(d, SegmentDistance(uv, float2(0.30, 0.72), float2(0.43, 0.60)));
                d = min(d, SegmentDistance(uv, float2(0.30, 0.36), float2(0.62, 0.36)));
                float mark = 1.0 - smoothstep(0.025, 0.055, d);
                float glow = 1.0 - smoothstep(0.055, 0.15, d);
                return saturate(mark + glow * 0.35);
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
                fixed4 color = SampleMain(uv, IN.color);
                float mode = 1.0;

                if (mode < 0.5)
                {
                    fixed4 source = color;
                    float cut = CutValue(uv);
                    float visible = step(0.0, cut);
                    float seam = (1.0 - smoothstep(0.0, 0.045, abs(cut))) * source.a;
                    color.a *= visible;
                    color.rgb = lerp(color.rgb, _AuraColor.rgb, seam * 0.8);
                    color.a = max(color.a, seam * _AuraColor.a);
                }
                else if (mode < 1.5)
                {
                    float cut = CutValue(uv);
                    float2 tangent = normalize(float2(1.0, -0.55));
                    float split = sin(_Time.y * _EffectSpeed) * 0.032 * max(_EffectStrength, 0.35);
                    fixed4 upper = SampleMain(uv - tangent * split, IN.color) * step(0.0, cut);
                    fixed4 lower = SampleMain(uv + tangent * split, IN.color) * (1.0 - step(0.0, cut));
                    color = Over(lower, upper);
                    float crack = (1.0 - smoothstep(0.0, 0.035, abs(cut))) * max(upper.a, lower.a);
                    color.rgb = lerp(color.rgb, _AuraColor.rgb, crack);
                    color.a = max(color.a, crack * _AuraColor.a);
                }
                else if (mode < 2.5)
                {
                    fixed4 back = SampleMain(uv - float2(0.0, 0.12), IN.color);
                    back.rgb = lerp(back.rgb, _AuraColor.rgb, 0.45);
                    back.a *= 0.56;
                    color = Over(back, color);
                }
                else if (mode < 3.5)
                {
                    float copies = clamp(_CopyCount, 2.0, 4.0);
                    float extraCopies = copies - 1.0;
                    float groupScale = 0.95 - extraCopies * 0.025;
                    float groupShift = extraCopies * 0.035;
                    float copySpacing = 0.09;
                    float2 baseUv = float2((uv.x + groupShift - 0.5) / groupScale + 0.5, (uv.y - 0.5) / groupScale + 0.5);
                    fixed4 result = SampleMain(baseUv, IN.color);
                    for (int i = 1; i < 4; i++)
                    {
                        if (i >= copies)
                            break;
                        float2 copyUv = float2((uv.x + groupShift - copySpacing * i - 0.5) / groupScale + 0.5, (uv.y - 0.5) / groupScale + 0.5);
                        fixed4 copy = SampleMain(copyUv, IN.color);
                        copy.a *= 0.72 - i * 0.16;
                        result = Over(copy, result);
                    }
                    color = result;
                }
                else if (mode < 4.5)
                {
                    float proliferateScale = 0.91;
                    float proliferateShift = 0.045;
                    float2 layoutUv = float2((uv.x + proliferateShift - 0.5) / proliferateScale + 0.5, (uv.y - 0.5) / proliferateScale + 0.5);
                    float phase = frac(_Time.y * _EffectSpeed * 0.65);
                    float band = 1.0 - smoothstep(0.0, 0.28, abs(layoutUv.x - phase));
                    float scale = 1.0 + band * _EffectStrength * 0.55;
                    float2 warpedUv = float2(layoutUv.x, 0.5 + (layoutUv.y - 0.5) / scale);
                    color = SampleMain(warpedUv, IN.color);
                    float2 ghostUv = float2((uv.x + proliferateShift - 0.145 - 0.5) / proliferateScale + 0.5, (uv.y - 0.5) / proliferateScale + 0.5);
                    fixed4 ghost = SampleMain(ghostUv, IN.color);
                    ghost.rgb = _AuraColor.rgb;
                    ghost.a *= 0.28 * (0.45 + band * 0.55);
                    color = Over(ghost, color);
                    color.rgb = lerp(color.rgb, _AuraColor.rgb, band * color.a * 0.35);
                }
                else if (mode < 5.5)
                {
                    fixed4 source = color;
                    float mark = ReturnMark(uv);
                    color.rgb *= 0.28;
                    color.a *= 0.55;
                    fixed4 symbol;
                    symbol.rgb = _AuraColor.rgb;
                    symbol.a = saturate(mark * max(_AuraColor.a, 0.75));
                    color = Over(color, symbol);
                    color.a = max(color.a, source.a * mark);
                }
                else
                {
                    color = SampleRandomCycle(uv, IN.color);
                    float sparkle = step(0.965, frac(sin(dot(floor((uv + _Time.y * 0.12) * 14.0), float2(12.9898, 78.233))) * 43758.5453));
                    color.rgb = lerp(color.rgb, _AuraColor.rgb, sparkle * color.a * 0.6);
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
