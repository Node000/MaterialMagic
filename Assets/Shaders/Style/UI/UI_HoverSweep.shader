Shader "Style/UI/HoverSweep"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SweepColor ("Sweep Color", Color) = (1,0.78,0.94,0.55)
        _SweepWidth ("Sweep Width", Range(0.001,0.5)) = 0.08
        _SweepSoftness ("Sweep Softness", Range(0.001,0.5)) = 0.12
        _SweepSpeed ("Sweep Speed", Float) = 1.7
        _SweepAngle ("Sweep Angle", Range(-180,180)) = 18
        _SweepIntensity ("Sweep Intensity", Range(0,2)) = 0.55
        _SweepOffset ("Sweep Offset", Range(0,1)) = 0
        [Enum(Off,0,On,1)] _UseTime ("Use Time", Float) = 1
        _RGBSplitAmount ("RGB Split Pixels", Range(0,4)) = 0.6
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
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            fixed4 _SweepColor;
            float4 _ClipRect;
            float _SweepWidth;
            float _SweepSoftness;
            float _SweepSpeed;
            float _SweepAngle;
            float _SweepIntensity;
            float _SweepOffset;
            float _UseTime;
            float _RGBSplitAmount;

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
                float radians = _SweepAngle * 0.01745329252;
                float2 axis = float2(cos(radians), sin(radians));
                float projected = dot(uv - 0.5, axis);
                float progress = frac(_SweepOffset + (_UseTime > 0.5 ? _Time.y * _SweepSpeed : 0.0));
                float sweepCenter = progress * 1.6 - 0.8;
                float sweep = 1.0 - smoothstep(_SweepWidth, _SweepWidth + _SweepSoftness, abs(projected - sweepCenter));
                sweep *= _SweepIntensity;

                float2 split = axis * _RGBSplitAmount * _MainTex_TexelSize.xy * sweep;
                fixed4 baseColor = tex2D(_MainTex, uv) + _TextureSampleAdd;
                fixed4 splitPlus = tex2D(_MainTex, uv + split) + _TextureSampleAdd;
                fixed4 splitMinus = tex2D(_MainTex, uv - split) + _TextureSampleAdd;
                baseColor.r = lerp(baseColor.r, splitPlus.r, saturate(sweep * 0.22));
                baseColor.b = lerp(baseColor.b, splitMinus.b, saturate(sweep * 0.22));
                baseColor *= IN.color;
                baseColor.rgb += _SweepColor.rgb * _SweepColor.a * sweep;

                #ifdef UNITY_UI_CLIP_RECT
                baseColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(baseColor.a - 0.001);
                #endif

                return saturate(baseColor);
            }
            ENDCG
        }
    }
}
