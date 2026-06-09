// Auto-split per modifier shader. Source behavior derived from UI/MaterialModifierAura.
// 周期箭头：通用边缘光与斜向扫光。
Shader "UI/MaterialModifiers/PeriodArrowModifier"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _AuraColor ("Aura Color", Color) = (1,0.8,0.35,1)
        _GradientColor1 ("Gradient Color 1", Color) = (1,1,1,1)
        _GradientColor2 ("Gradient Color 2", Color) = (1,0.75,0.25,1)
        _GradientColor3 ("Gradient Color 3", Color) = (1,0.25,0.75,1)
        _GradientColor4 ("Gradient Color 4", Color) = (0.25,0.75,1,1)
        _GradientPosition2 ("Gradient Position 2", Range(0,1)) = 0.33
        _GradientPosition3 ("Gradient Position 3", Range(0,1)) = 0.66
        _GradientAngle ("Gradient Angle", Range(0,6.28318)) = 0.7854
        _GradientScale ("Gradient Scale", Float) = 1
        _GradientOffset ("Gradient Offset", Float) = 0
        _GradientScrollSpeed ("Gradient Scroll Speed", Float) = 0
        _GradientIntensity ("Gradient Intensity", Range(0,1)) = 0
        _PulseSpeed ("Pulse Speed", Float) = 2.2
        _PulseStrength ("Pulse Strength", Range(0,1)) = 0.18
        _SweepSpeed ("Sweep Speed", Float) = 1.2
        _SweepWidth ("Sweep Width", Range(0.001,0.5)) = 0.11
        _SweepIntensity ("Sweep Intensity", Range(0,2)) = 0.55
        _EdgeIntensity ("Edge Intensity", Range(0,2)) = 0.45
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
            fixed4 _GradientColor1;
            fixed4 _GradientColor2;
            fixed4 _GradientColor3;
            fixed4 _GradientColor4;
            float _GradientPosition2;
            float _GradientPosition3;
            float _GradientAngle;
            float _GradientScale;
            float _GradientOffset;
            float _GradientScrollSpeed;
            float _GradientIntensity;
            float4 _ClipRect;
            float _PulseSpeed;
            float _PulseStrength;
            float _SweepSpeed;
            float _SweepWidth;
            float _SweepIntensity;
            float _EdgeIntensity;


            fixed3 SampleGradientRamp(float2 uv)
            {
                float2 direction = float2(cos(_GradientAngle), sin(_GradientAngle));
                float t = dot(uv - 0.5, direction) * max(_GradientScale, 0.0001) + 0.5 + _GradientOffset + _Time.y * _GradientScrollSpeed;
                t = frac(t);
                float p2 = saturate(_GradientPosition2);
                float p3 = max(saturate(_GradientPosition3), p2 + 0.0001);
                fixed3 c12 = lerp(_GradientColor1.rgb, _GradientColor2.rgb, saturate(t / max(p2, 0.0001)));
                fixed3 c23 = lerp(_GradientColor2.rgb, _GradientColor3.rgb, saturate((t - p2) / max(p3 - p2, 0.0001)));
                fixed3 c34 = lerp(_GradientColor3.rgb, _GradientColor4.rgb, saturate((t - p3) / max(1.0 - p3, 0.0001)));
                fixed3 ramp = t < p2 ? c12 : (t < p3 ? c23 : c34);
                return lerp(_AuraColor.rgb, ramp, saturate(_GradientIntensity));
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
                fixed4 color = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;
                float sourceAlpha = color.a;

                float2 centered = abs(uv - 0.5) * 2.0;
                float edgeDistance = max(centered.x, centered.y);
                float edge = smoothstep(0.45, 1.0, edgeDistance) * _EdgeIntensity;

                float sweepCenter = frac(_Time.y * _SweepSpeed) * 1.6 - 0.8;
                float sweepDistance = abs((uv.x + uv.y) * 0.5 - sweepCenter);
                float sweep = (1.0 - smoothstep(_SweepWidth, _SweepWidth + 0.18, sweepDistance)) * _SweepIntensity;

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseStrength;
                float aura = saturate((edge + sweep) * sourceAlpha);
                color.rgb = saturate(color.rgb * pulse + SampleGradientRamp(uv) * aura * _AuraColor.a);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
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
