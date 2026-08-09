Shader "Style/UI/EmbossedWindowFrame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Emboss Mask Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _BackgroundTex ("Background Image Sprite", 2D) = "white" {}
        _BackgroundUVTransform ("Background UV Scale Offset", Vector) = (1, 1, 0, 0)
        _HighlightColor ("Top Left Highlight", Color) = (0.76, 0.87, 0.84, 1)
        _ShadowColor ("Bottom Right Shadow", Color) = (0.01, 0.018, 0.016, 1)
        _BevelWidth ("Bevel Width (Pixels)", Range(1, 32)) = 5
        _EdgeStrength ("Edge Strength", Range(0, 1)) = 0.72
        _FaceTint ("Face Tint", Color) = (0.45, 0.62, 0.56, 1)
        _FaceOpacity ("Face Tint Opacity", Range(0, 1)) = 0.08
        _EmbossAmount ("Emboss Amount", Range(-1, 1)) = 1
        _Opacity ("Opacity", Range(0, 1)) = 1
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
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "EmbossOverBackground"
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
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _BackgroundTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            fixed4 _HighlightColor;
            fixed4 _ShadowColor;
            fixed4 _FaceTint;
            float4 _BackgroundUVTransform;
            float4 _ClipRect;
            float _BevelWidth;
            float _EdgeStrength;
            float _FaceOpacity;
            float _EmbossAmount;
            float _Opacity;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.texcoord = v.texcoord;
                o.worldPosition = v.vertex;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                float mask = tex2D(_MainTex, uv).a;
                float2 bevelOffset = max(fwidth(uv), 0.00001) * _BevelWidth;
                float2 lightDirection = normalize(float2(-0.75, 0.65));
                float lightSample = tex2D(_MainTex, uv + lightDirection * bevelOffset).a;
                float shadowSample = tex2D(_MainTex, uv - lightDirection * bevelOffset).a;
                float embossBlend = _EmbossAmount * 0.5 + 0.5;
                float outerHighlight = saturate(lightSample - mask);
                float outerShadow = saturate(shadowSample - mask);
                float innerHighlight = saturate(mask - shadowSample);
                float innerShadow = saturate(mask - lightSample);
                float highlightMask = lerp(outerShadow + innerShadow, outerHighlight + innerHighlight, embossBlend);
                float shadowMask = lerp(outerHighlight + innerHighlight, outerShadow + innerShadow, embossBlend);
                float edgeMask = saturate(max(highlightMask, shadowMask));
                float faceMask = mask * (1.0 - edgeMask);
                float2 backgroundUv = uv * _BackgroundUVTransform.xy + _BackgroundUVTransform.zw;
                float3 background = tex2D(_BackgroundTex, backgroundUv).rgb;
                float3 color = lerp(background, _FaceTint.rgb, faceMask * _FaceOpacity);
                color = lerp(color, _HighlightColor.rgb, saturate(highlightMask) * _EdgeStrength);
                color = lerp(color, _ShadowColor.rgb, saturate(shadowMask) * _EdgeStrength);
                float alpha = saturate(max(mask * _FaceOpacity, edgeMask * _EdgeStrength)) * i.color.a * _Opacity;
                fixed4 result = fixed4(color * i.color.rgb, alpha);

                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
