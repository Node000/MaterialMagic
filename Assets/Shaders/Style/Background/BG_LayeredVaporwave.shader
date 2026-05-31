Shader "Style/Background/LayeredVaporwave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _TopLeftColor ("Top Left Color", Color) = (0.102,0.078,0.141,1)
        _BottomRightColor ("Bottom Right Color", Color) = (0.063,0.149,0.22,1)
        _CenterMistColor ("Center Mist Color", Color) = (0.718,0.612,1,0.13)
        _CenterHaloColorA ("Center Halo Pink", Color) = (0.94,0.35,0.65,0.13)
        _CenterHaloColorB ("Center Halo Cyan", Color) = (0.35,0.84,1,0.07)
        _HaloCenter ("Halo Center", Vector) = (0.5,0.55,0,0)
        _HaloScale ("Halo Scale", Vector) = (1.15,0.72,0,0)
        _GridColor ("Grid Color", Color) = (0.35,0.84,1,0.055)
        _GridDensity ("Grid Density", Float) = 18
        _GridPerspective ("Grid Perspective", Range(0,1)) = 0.55
        _GeometryColor ("Geometry Color", Color) = (0.953,0.933,0.961,0.06)
        _NoiseTex ("Noise Texture", 2D) = "gray" {}
        _GrainIntensity ("Grain Intensity", Range(0,0.2)) = 0.045
        _ScanlineIntensity ("Scanline Intensity", Range(0,0.12)) = 0.025
        _VignetteIntensity ("Vignette Intensity", Range(0,1)) = 0.22
        _MotionSpeed ("Motion Speed", Float) = 0.025
        _ParallaxStrength ("Parallax Strength", Range(0,0.05)) = 0.012
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
            sampler2D _NoiseTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            fixed4 _TopLeftColor;
            fixed4 _BottomRightColor;
            fixed4 _CenterMistColor;
            fixed4 _CenterHaloColorA;
            fixed4 _CenterHaloColorB;
            fixed4 _GridColor;
            fixed4 _GeometryColor;
            float4 _HaloCenter;
            float4 _HaloScale;
            float4 _ClipRect;
            float _GridDensity;
            float _GridPerspective;
            float _GrainIntensity;
            float _ScanlineIntensity;
            float _VignetteIntensity;
            float _MotionSpeed;
            float _ParallaxStrength;
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

            float lineMask(float value, float width)
            {
                float f = abs(frac(value) - 0.5);
                return 1.0 - smoothstep(0.5 - width, 0.5, f);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float t = _Time.y * _MotionSpeed;
                float2 slowUv = uv + float2(sin(t) * _ParallaxStrength, cos(t * 0.7) * _ParallaxStrength);
                float diagonal = saturate((uv.x + (1.0 - uv.y)) * 0.5);
                float3 rgb = lerp(_TopLeftColor.rgb, _BottomRightColor.rgb, diagonal);

                float2 haloUv = (uv - _HaloCenter.xy) / max(_HaloScale.xy, float2(0.001,0.001));
                float halo = exp(-dot(haloUv, haloUv) * 3.2);
                float haloB = exp(-dot(haloUv + float2(-0.08,0.04), haloUv + float2(-0.08,0.04)) * 7.0);
                rgb = lerp(rgb, _CenterMistColor.rgb, halo * _CenterMistColor.a);
                rgb += _CenterHaloColorA.rgb * halo * _CenterHaloColorA.a;
                rgb += _CenterHaloColorB.rgb * haloB * _CenterHaloColorB.a;

                float bottomMask = smoothstep(0.58, 0.12, uv.y);
                float perspective = lerp(1.0, max(0.1, 1.0 - uv.y), _GridPerspective);
                float gridX = lineMask((slowUv.x - 0.5) * _GridDensity / perspective + 0.5, 0.035);
                float gridY = lineMask((slowUv.y + t * 0.4) * _GridDensity * 0.55, 0.025);
                float grid = saturate(max(gridX, gridY) * bottomMask);
                rgb += _GridColor.rgb * grid * _GridColor.a;

                float geomA = lineMask((uv.x + uv.y * 0.55 + 0.1) * 5.0, 0.012) * smoothstep(0.58, 0.85, uv.y) * smoothstep(0.42, 0.78, uv.x);
                float geomB = lineMask((uv.x - uv.y * 0.85 + 0.3) * 4.0, 0.012) * smoothstep(0.62, 0.92, uv.x);
                rgb += _GeometryColor.rgb * saturate(geomA + geomB) * _GeometryColor.a;

                float grain = tex2D(_NoiseTex, uv * 3.5 + t).r * 2.0 - 1.0;
                float scanline = sin(uv.y * 920.0 + _Time.y * 3.0) * 0.5 + 0.5;
                float2 centerUv = uv - 0.5;
                float vignette = smoothstep(0.18, 0.78, dot(centerUv, centerUv) * 2.2);
                rgb += grain * _GrainIntensity;
                rgb *= 1.0 - scanline * _ScanlineIntensity;
                rgb *= 1.0 - vignette * _VignetteIntensity;

                fixed4 sprite = tex2D(_MainTex, uv) + _TextureSampleAdd;
                fixed4 color = fixed4(saturate(rgb) * IN.color.rgb, sprite.a * IN.color.a * _Opacity);

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
