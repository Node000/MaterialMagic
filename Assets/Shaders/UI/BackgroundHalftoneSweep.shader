Shader "UI/BackgroundHalftoneSweep"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        _SweepProgress ("Sweep Progress", Range(0,1)) = 0
        _SweepWidth ("Sweep Width", Range(0.01,1)) = 0.24
        _EdgeSoftness ("Sweep Edge Softness", Range(0.001,0.5)) = 0.08
        _DotRes ("Dot Resolution", Float) = 96
        _DotSize ("Dot Size", Range(0.01,1)) = 0.62
        _DotShape ("Dot Shape", Range(0,1)) = 0
        _EffectColor ("Effect Color", Color) = (0.72,0.72,0.72,1)
        _EffectStrength ("Effect Strength", Range(0,1)) = 0.28
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use UI Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; float4 worldPosition : TEXCOORD1; };
            sampler2D _MainTex;
            fixed4 _Color, _EffectColor, _TextureSampleAdd;
            float4 _ClipRect;
            float _SweepProgress, _SweepWidth, _EdgeSoftness, _DotRes, _DotSize, _DotShape, _EffectStrength;

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
                fixed4 source = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;
                float diagonal = dot(i.texcoord - 0.5, float2(-1.0, 1.0));
                float sweepCenter = lerp(-1.0, 1.0, saturate(_SweepProgress));
                float distanceToSweep = abs(diagonal - sweepCenter);
                float halfWidth = max(_SweepWidth * 0.5, 0.001);
                float sweepMask = 1.0 - smoothstep(halfWidth, halfWidth + max(_EdgeSoftness, 0.001), distanceToSweep);

                float2 grid = i.texcoord * max(_DotRes, 1.0);
                float2 local = frac(grid) - 0.5;
                float squareDistance = max(abs(local.x), abs(local.y));
                float circleDistance = length(local);
                float shapeDistance = lerp(squareDistance, circleDistance, saturate(_DotShape));
                float maxRadius = lerp(0.5, 0.7071, saturate(_DotShape));
                float dotRadius = maxRadius * saturate(_DotSize) * max(source.a, 0.001);
                float dotMask = 1.0 - smoothstep(dotRadius, dotRadius + 0.025, shapeDistance);

                float alpha = source.a * sweepMask * dotMask * saturate(_EffectStrength);
                fixed3 color = lerp(source.rgb, _EffectColor.rgb, saturate(_EffectColor.a * _EffectStrength));
                fixed4 result = fixed4(color, alpha);
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
