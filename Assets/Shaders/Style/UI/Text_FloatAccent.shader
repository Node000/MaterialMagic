Shader "Style/UI/TextFloatAccent"
{
    Properties
    {
        [PerRendererData] _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Vertex Tint", Color) = (1,1,1,1)
        _FaceColor ("Face Color", Color) = (0.953,0.933,0.961,1)
        _OutlineColor ("Outline Color", Color) = (0.094,0.075,0.122,1)
        _OutlineWidth ("Outline Width Pixels", Range(0,8)) = 2
        _UnderlayColor ("Underlay Color", Color) = (0,0,0,0.45)
        _UnderlayOffset ("Underlay Offset Pixels", Vector) = (2,-2,0,0)
        _GlowColor ("Glow Color", Color) = (0.94,0.35,0.65,0.35)
        _GlowPower ("Glow Power", Range(0.1,8)) = 2.2
        _CritSplitAmount ("Crit Split Pixels", Range(0,5)) = 0
        _FlashAmount ("Flash Amount", Range(0,1)) = 0
        _Opacity ("Opacity", Range(0,1)) = 1
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
            fixed4 _FaceColor;
            fixed4 _OutlineColor;
            fixed4 _UnderlayColor;
            fixed4 _GlowColor;
            float4 _UnderlayOffset;
            float4 _ClipRect;
            float _OutlineWidth;
            float _GlowPower;
            float _CritSplitAmount;
            float _FlashAmount;
            float _Opacity;

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

            float sampleAlpha(float2 uv)
            {
                return (tex2D(_MainTex, uv) + _TextureSampleAdd).a;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float centerAlpha = sampleAlpha(uv);
                float2 pixel = _MainTex_TexelSize.xy * _OutlineWidth;
                float outlineAlpha = centerAlpha;
                outlineAlpha = max(outlineAlpha, sampleAlpha(uv + float2(pixel.x, 0)));
                outlineAlpha = max(outlineAlpha, sampleAlpha(uv - float2(pixel.x, 0)));
                outlineAlpha = max(outlineAlpha, sampleAlpha(uv + float2(0, pixel.y)));
                outlineAlpha = max(outlineAlpha, sampleAlpha(uv - float2(0, pixel.y)));
                outlineAlpha = max(outlineAlpha, sampleAlpha(uv + pixel));
                outlineAlpha = max(outlineAlpha, sampleAlpha(uv - pixel));
                outlineAlpha = max(outlineAlpha, sampleAlpha(uv + float2(pixel.x, -pixel.y)));
                outlineAlpha = max(outlineAlpha, sampleAlpha(uv + float2(-pixel.x, pixel.y)));

                float underlayAlpha = sampleAlpha(uv - _UnderlayOffset.xy * _MainTex_TexelSize.xy);
                float2 split = float2(_CritSplitAmount, 0) * _MainTex_TexelSize.xy;
                float splitR = sampleAlpha(uv + split);
                float splitB = sampleAlpha(uv - split);

                float outlineOnly = saturate(outlineAlpha - centerAlpha);
                float underlayOnly = saturate(underlayAlpha - max(centerAlpha, outlineOnly));
                float3 faceRgb = _FaceColor.rgb * IN.color.rgb;
                faceRgb.r = lerp(faceRgb.r, 1.0, saturate(splitR - centerAlpha) * 0.45);
                faceRgb.b = lerp(faceRgb.b, 1.0, saturate(splitB - centerAlpha) * 0.45);

                float3 rgb = _UnderlayColor.rgb;
                rgb = lerp(rgb, _OutlineColor.rgb, saturate(outlineAlpha));
                rgb = lerp(rgb, faceRgb, centerAlpha);
                float glow = pow(saturate(outlineAlpha), _GlowPower) * _GlowColor.a;
                rgb += _GlowColor.rgb * glow;
                rgb = lerp(rgb, 1.0.xxx, _FlashAmount);

                float alpha = max(max(centerAlpha * _FaceColor.a * IN.color.a, outlineOnly * _OutlineColor.a), underlayOnly * _UnderlayColor.a);
                fixed4 color = fixed4(saturate(rgb), saturate(alpha * _Opacity));

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
