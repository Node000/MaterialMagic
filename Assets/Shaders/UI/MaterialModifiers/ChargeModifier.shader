Shader "UI/MaterialModifiers/StaticElectricArrowDisplace"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ElectricColor ("Lightning Color", Color) = (0.2, 0.6, 1.0, 1.0)

        [Header(Static Frizzle Controls)]
        _ParticleSize ("Particle Size (0.1 is coarse)", Range(0.01, 0.5)) = 0.08
        _FrizzleDistance ("Frizzle Dist (Displacement)", Range(0, 1)) = 0.25
        _Intensity ("Electric Intensity", Range(0, 1)) = 0.6
        _FlickerSpeed ("Flicker Speed", Float) = 8.0

        [Header(Transparency Option)]
        [Enum(Particles Fade Out, 0, Keep Solid Pixel, 1)] _AlphaMode ("Alpha Mode", Float) = 0

        // UI 遮罩相关参数保留以兼容 UGUI 机制
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
            #pragma target 3.0
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
            fixed4 _ElectricColor;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            
            float _ParticleSize;
            float _FrizzleDistance;
            float _Intensity;
            float _FlickerSpeed;
            float _AlphaMode;

            // --- 核心算法：程序化高频噪声 ---
            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float gradientNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f*f*(3.0-2.0*f);
                return lerp( lerp( dot( hash2(i + float2(0.0,0.0)), f - float2(0.0,0.0) ), 
                                   dot( hash2(i + float2(1.0,0.0)), f - float2(1.0,0.0) ), u.x),
                             lerp( dot( hash2(i + float2(0.0,1.0)), f - float2(0.0,1.0) ), 
                                   dot( hash2(i + float2(1.0,1.0)), f - float2(1.0,1.0) ), u.x), u.y);
            }

            float InsideUv(float2 uv)
            {
                return step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0);
            }

            fixed4 SampleMain(float2 uv, fixed4 vertexColor)
            {
                return (tex2D(_MainTex, saturate(uv)) + _TextureSampleAdd) * vertexColor * InsideUv(uv);
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
                float time = _Time.y * _FlickerSpeed;

                // --- 1. 计算向四周扩散的放射单位向量 ---
                float2 centerDir = uv - 0.5;
                float distFromCenter = length(centerDir);
                float2 radialDir = distFromCenter > 0.001 ? centerDir / distFromCenter : float2(1.0, 0.0);

                // --- 2. 生成程序化噪声 ---
                float noiseScale = 1.0 / max(_ParticleSize, 0.001);
                float rawNoise = gradientNoise(uv * noiseScale + time);
                float elecMask = saturate(rawNoise + 0.5);

                // --- 3. 核心修改：让箭头自身的像素采样产生静电位移变化 ---
                // 基于静电强度和噪声，直接计算当前像素坐标的偏移量
                float displacementFactor = elecMask * _Intensity;
                float individualLeap = displacementFactor * _FrizzleDistance;
                
                // 向中心方向进行逆向采样错位 (- radialDir)
                // 这会导致原本处于箭头内部的实体像素被“推”向外部，产生由于电荷自扩散导致的炸毛、解体效果
                float2 distortedUV = uv - radialDir * individualLeap;

                // 直接对形变后的 UV 坐标进行采样，使整个箭头本体粒子化
                fixed4 finalColor = SampleMain(distortedUV, IN.color);

                // --- 4. 动态电荷染色与边缘透明度处理 ---
                if (finalColor.a > 0.01)
                {
                    // 当局部噪声强度较高时，将形变后的像素染上静电颜色
                    float electricThresh = 1.0 - _Intensity * 0.7;
                    if (elecMask > electricThresh)
                    {
                        float colorLerp = saturate((elecMask - electricThresh) / max(1.0 - electricThresh, 0.001));
                        finalColor.rgb = lerp(finalColor.rgb, _ElectricColor.rgb, colorLerp * 0.75);
                    }

                    // 透明度模式控制
                    if (_AlphaMode == 0.0)
                    {
                        // 随着飞出/错位距离拉长，像素自然淡出，形成电荷消散的视觉毛边
                        finalColor.a *= saturate(1.0 - displacementFactor * 0.8);
                    }
                    else
                    {
                        // 硬边模式：确保飞出的扭曲像素在末端依然保持完整的实心切片感
                        finalColor.a = step(0.01, finalColor.a);
                    }
                }

                // --- 5. UI 裁剪与最终输出 ---
                finalColor.a *= IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(finalColor.a - 0.001);
                #endif

                return saturate(finalColor);
            }
            ENDCG
        }
    }
}