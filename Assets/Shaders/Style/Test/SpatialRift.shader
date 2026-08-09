Shader "Style/Test/SpatialRift"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _RiftSize ("Rift Size", Range(0.05,0.48)) = 0.25
        _RiftDensity ("Rift Density", Range(2,32)) = 12
        _VoidColor ("Void Color", Color) = (0.015,0.008,0.03,1)
        _RimColorA ("Rim Pink", Color) = (0.95,0.2,0.62,1)
        _RimColorB ("Rim Cyan", Color) = (0.25,0.8,1,1)
        _BackgroundColor ("Background Color", Color) = (0.03,0.04,0.09,1)
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
            fixed4 _Color, _VoidColor, _RimColorA, _RimColorB, _BackgroundColor;
            float4 _ClipRect;
            float _RiftSize, _RiftDensity;
            v2f vert(appdata_t v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.color = v.color * _Color; o.texcoord = v.texcoord; o.worldPosition = v.vertex; return o; }
            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.texcoord - 0.5;
                p.x *= 1.55;
                float time = _Time.y * 0.22;
                float r = length(p);
                float angle = atan2(p.y, p.x);
                float teeth = sin(angle * _RiftDensity + sin(angle * 3.0 - time) * 1.7) * 0.024;
                float fracture = abs(sin(angle * (_RiftDensity * 0.55) + r * 36.0 - time * 3.0));
                float warpedRadius = _RiftSize + teeth;
                float rimDistance = abs(r - warpedRadius);
                float rim = 1.0 - smoothstep(0.008, 0.035, rimDistance);
                float glow = 1.0 - smoothstep(0.01, 0.17, rimDistance);
                float voidMask = 1.0 - smoothstep(warpedRadius - 0.012, warpedRadius + 0.008, r);
                float ray = smoothstep(0.89, 1.0, fracture) * smoothstep(warpedRadius, warpedRadius + 0.22, r) * (1.0 - smoothstep(warpedRadius + 0.22, warpedRadius + 0.45, r));
                float stars = step(0.985, hash21(floor(i.texcoord * 140.0))) * (1.0 - voidMask);
                float3 rimColor = lerp(_RimColorA.rgb, _RimColorB.rgb, sin(angle * 2.0 + time) * 0.5 + 0.5);
                float3 rgb = _BackgroundColor.rgb;
                rgb += rimColor * (rim + glow * 0.28 + ray * 0.32);
                rgb = lerp(rgb, _VoidColor.rgb, voidMask);
                rgb += stars * float3(0.5, 0.72, 1.0) * 0.35;
                fixed4 color = fixed4(saturate(rgb) * i.color.rgb, i.color.a);
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
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
