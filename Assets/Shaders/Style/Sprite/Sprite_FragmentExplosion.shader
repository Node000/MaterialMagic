Shader "Style/Sprite/FragmentExplosion"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Explosion ("Explosion", Range(0,1)) = 0
        _ShardIndex ("Shard Index", Float) = 0
        _ShardRect ("Shard Rect", Vector) = (0,0,1,1)
        _SpriteUV ("Sprite UV", Vector) = (0,0,1,1)
        _CrackWidth ("Crack Width", Range(0,0.2)) = 0.035
        _EdgeColor ("Torn Edge Color", Color) = (1,0.86,0.55,0.9)
        _PaperTint ("Paper Tint", Color) = (1,0.95,0.82,0.22)
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
            fixed4 _EdgeColor;
            fixed4 _PaperTint;
            float4 _ClipRect;
            float _Explosion;
            float _ShardIndex;
            float4 _ShardRect;
            float4 _SpriteUV;
            float _CrackWidth;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
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
                float2 normalizedUv = saturate((uv - _SpriteUV.xy) / max(_SpriteUV.zw, float2(0.0001, 0.0001)));
                float2 sourceNormalizedUv = _ShardRect.xy + normalizedUv * _ShardRect.zw;
                float2 sourceUv = _SpriteUV.xy + sourceNormalizedUv * _SpriteUV.zw;

                float edgeDistance = min(min(normalizedUv.x, 1.0 - normalizedUv.x), min(normalizedUv.y, 1.0 - normalizedUv.y));
                float shardNoise = hash21(float2(_ShardIndex, _ShardIndex * 2.37));
                float jagged = (hash21(floor(normalizedUv * float2(18.0, 18.0)) + _ShardIndex) - 0.5) * 0.018 * _Explosion;
                float crackWidth = _CrackWidth * saturate(max(0.0, _Explosion - 0.02)) * lerp(0.55, 1.25, shardNoise) + jagged;
                float cracked = smoothstep(crackWidth, crackWidth + 0.012, edgeDistance);
                float crackMask = lerp(1.0, cracked, saturate(_Explosion * 1.35));
                float tornEdge = (1.0 - smoothstep(crackWidth + 0.002, crackWidth + 0.03, edgeDistance)) * saturate(_Explosion);

                fixed4 source = (tex2D(_MainTex, sourceUv) + _TextureSampleAdd) * IN.color;
                float tintNoise = hash21(floor(normalizedUv * float2(5.0, 5.0)) + _ShardIndex * 3.17);
                source.rgb = lerp(source.rgb, _PaperTint.rgb, _PaperTint.a * saturate(_Explosion) * (0.35 + tintNoise * 0.65));
                source.rgb = lerp(source.rgb, _EdgeColor.rgb, saturate(tornEdge * _EdgeColor.a));
                source.a *= crackMask;
                source.a *= 1.0 - smoothstep(0.72, 1.0, saturate(_Explosion));

                #ifdef UNITY_UI_CLIP_RECT
                source.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(source.a - 0.001);
                #endif

                return saturate(source);
            }
            ENDCG
        }
    }
}
