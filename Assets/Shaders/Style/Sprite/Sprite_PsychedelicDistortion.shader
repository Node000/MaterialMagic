Shader "Style/Sprite/PsychedelicDistortion"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _NoiseTex ("Noise Texture", 2D) = "gray" {}
        _DistortAmount ("Distort Amount", Range(0,0.05)) = 0.006
        _DistortSpeed ("Distort Speed", Float) = 0.45
        _ColorShiftAmount ("Color Shift Amount", Range(0,1)) = 0.16
        _AfterImageAmount ("After Image Amount", Range(0,1)) = 0.18
        _MaskTex ("Mask Texture", 2D) = "white" {}
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
            sampler2D _NoiseTex;
            sampler2D _MaskTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _DistortAmount;
            float _DistortSpeed;
            float _ColorShiftAmount;
            float _AfterImageAmount;

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
                float mask = tex2D(_MaskTex, uv).a;
                float time = _Time.y * _DistortSpeed;
                float2 noiseUv = uv * 2.2 + float2(time * 0.13, -time * 0.07);
                float2 noise = tex2D(_NoiseTex, noiseUv).rg * 2.0 - 1.0;
                float2 wave = float2(sin((uv.y + time) * 18.0), cos((uv.x - time) * 15.0)) * 0.5;
                float2 offset = (noise + wave) * _DistortAmount * mask;

                fixed4 baseColor = (tex2D(_MainTex, uv + offset) + _TextureSampleAdd) * IN.color;
                fixed4 afterA = (tex2D(_MainTex, uv + offset + float2(_DistortAmount * 1.7, 0)) + _TextureSampleAdd) * IN.color;
                fixed4 afterB = (tex2D(_MainTex, uv + offset - float2(_DistortAmount * 1.7, 0)) + _TextureSampleAdd) * IN.color;
                float3 shifted = baseColor.rgb;
                shifted.r = lerp(shifted.r, afterA.r, _ColorShiftAmount);
                shifted.b = lerp(shifted.b, afterB.b, _ColorShiftAmount);
                shifted = lerp(shifted, afterA.rgb * float3(1.0,0.45,0.85), _AfterImageAmount * afterA.a * 0.45);
                shifted = lerp(shifted, afterB.rgb * float3(0.45,0.95,1.0), _AfterImageAmount * afterB.a * 0.35);
                baseColor.rgb = shifted;

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
