Shader "UI/MaterialModifiers/StaticElectricVortexDisplace"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ElectricColor ("Vortex Glow Color", Color) = (0.2, 0.6, 1.0, 1.0)

        [Header(Vortex Controls)]
        _TwistAmount ("Twist Amount (Intensity)", Range(-10, 10)) = 4.0
        _RotationSpeed ("Rotation Speed", Float) = 8.0
        _VortexRadius ("Vortex Radius (Range)", Range(0.1, 1.0)) = 0.5
        _VortexDensity ("Vortex Density (Waves)", Range(1, 30)) = 12.0

        [Header(Static Frizzle Controls)]
        _ParticleSize ("Noise Particle Size", Range(0.01, 0.5)) = 0.08
        _NoiseIntensity ("Noise Jitter Intensity", Range(0, 0.1)) = 0.02

        [Header(Transparency Option)]
        [Enum(Fade Out, 0, Keep Solid, 1)] _AlphaMode ("Alpha Mode", Float) = 0

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
            
            float _TwistAmount;
            float _RotationSpeed;
            float _VortexRadius;
            float _VortexDensity;
            float _ParticleSize;
            float _NoiseIntensity;
            float _AlphaMode;

            // --- 程序化高频噪声（用于给漩涡边缘增加破碎静电感） ---
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
                float time = _Time.y * _RotationSpeed;

                // 1. 计算相对中心的距离
                float2 centerRel = uv - 0.5;
                float dist = length(centerRel);
                
                // 核心控制项：漩涡的影响范围
                float influence = smoothstep(_VortexRadius, 0.0, dist);

                // 2. 构造漩涡流动 (关键：只在这里引入时间，产生流动的扭曲感)
                // 移除原有的 + time 整体旋转，只保留sin内的波形偏移
                float wave = sin(dist * _VortexDensity - time); 

                // 3. 计算旋转角度 (只利用波形产生局部的扭曲)
                // 这里移除了 "+ time"，这样就不会带动整个图标旋转了
                float angle = _TwistAmount * wave * influence;

                // 4. 执行 UV 扭曲变换
                float cosA = cos(angle);
                float sinA = sin(angle);
                
                float2 rotatedRel = float2(
                    centerRel.x * cosA - centerRel.y * sinA,
                    centerRel.x * sinA + centerRel.y * cosA
                );

                // 5. 叠加噪声
                float noiseScale = 1.0 / max(_ParticleSize, 0.001);
                float rawNoise = gradientNoise(uv * noiseScale + time * 1.5);
                rotatedRel += (rawNoise * _NoiseIntensity * influence);

                float2 distortedUV = 0.5 + rotatedRel;

                // 6. 采样
                fixed4 finalColor = SampleMain(distortedUV, IN.color);

                // --- 后续代码保持不变 ---
                if (finalColor.a > 0.01)
                {
                    if (_AlphaMode == 0.0)
                    {
                        finalColor.a *= saturate(1.0 - (influence * 0.3));
                    }
                    else
                    {
                        finalColor.a = step(0.01, finalColor.a);
                    }
                }

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