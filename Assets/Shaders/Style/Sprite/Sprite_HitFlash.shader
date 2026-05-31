Shader "Style/Sprite/HitFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashColor ("Flash Color", Color) = (1,0.78,0.92,1)
        _FlashAmount ("Flash Amount", Range(0,1)) = 0
        _OutlineFlashColor ("Outline Flash Color", Color) = (0.349,0.843,1,0.65)
        _OutlineFlashAmount ("Outline Flash Amount", Range(0,1)) = 0.25
        _OutlineWidth ("Outline Width Pixels", Range(0,6)) = 1
        [Enum(Off,0,On,1)] _PreserveAlpha ("Preserve Alpha", Float) = 1
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
            fixed4 _FlashColor;
            fixed4 _OutlineFlashColor;
            float4 _ClipRect;
            float _FlashAmount;
            float _OutlineFlashAmount;
            float _OutlineWidth;
            float _PreserveAlpha;

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
                fixed4 source = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                float2 px = _MainTex_TexelSize.xy * _OutlineWidth;
                float outlineAlpha = sampleAlpha(IN.texcoord + float2(px.x, 0));
                outlineAlpha = max(outlineAlpha, sampleAlpha(IN.texcoord - float2(px.x, 0)));
                outlineAlpha = max(outlineAlpha, sampleAlpha(IN.texcoord + float2(0, px.y)));
                outlineAlpha = max(outlineAlpha, sampleAlpha(IN.texcoord - float2(0, px.y)));
                outlineAlpha = saturate(outlineAlpha - source.a);

                fixed4 color = source;
                color.rgb = lerp(color.rgb, _FlashColor.rgb, _FlashAmount * _FlashColor.a);
                color.rgb = lerp(color.rgb, _OutlineFlashColor.rgb, outlineAlpha * _OutlineFlashAmount * _OutlineFlashColor.a);
                color.a = (_PreserveAlpha > 0.5) ? source.a : max(source.a, outlineAlpha * _OutlineFlashColor.a * _OutlineFlashAmount);

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
