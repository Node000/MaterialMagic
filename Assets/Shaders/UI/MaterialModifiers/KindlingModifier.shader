Shader "UI/MaterialModifiers/FireEnchantedArrow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BurnColor ("Flame Color", Color) = (1, 0.3, 0, 1)

        [Header(Flame Particle Controls)]
        _ParticleSize ("Particle Size (0.1 is coarse)", Range(0.01, 1.0)) = 0.2
        _LeapHeight ("Leap Distance", Range(0, 1)) = 0.4
        _LeapAngle ("Leap Angle (Deg, 90 is up)", Range(0, 360)) = 90.0
        _Intensity ("Fire Intensity", Range(0, 1)) = 0.5
        _AnimSpeed ("Animation Speed", Float) = 3.0

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
            fixed4 _BurnColor;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            
            float _ParticleSize;
            float _LeapHeight;
            float _LeapAngle;
            float _Intensity;
            float _AnimSpeed;

            // --- 核心算法：程序化伪随机噪声 (用于像素级不规则运动) ---
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

            // 判断 UV 是否在 0-1 范围内
            float InsideUv(float2 uv)
            {
                return step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0);
            }

            fixed4 SampleMain(float2 uv, fixed4 vertexColor)
            {
                // 确保超出 0~1 的 UV 不会产生重复采样
                return (tex2D(_MainTex, saturate(uv)) + _TextureSampleAdd) * vertexColor;
            }

            // 透明度混合公式 (源覆盖目标)
            fixed4 Over(fixed4 bottom, fixed4 top)
            {
                float alpha = top.a + bottom.a * (1.0 - top.a);
                float3 rgb = top.rgb * top.a + bottom.rgb * bottom.a * (1.0 - top.a);
                fixed4 result;
                result.rgb = alpha > 0.0001 ? rgb / alpha : 0;
                result.a = alpha;
                return result;
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
                float time = _Time.y * _AnimSpeed;

                // --- 1. 计算腾跃方向和角度 ---
                // 将角度转换为弧度
                float rad = _LeapAngle * 0.0174532925; 
                float2 leapDir = float2(cos(rad), sin(rad));       

                // --- 2. 生成燃烧核心噪声 ---
                // 噪声坐标。将 _ParticleSize 转换为缩放比例，值越小颗粒越粗
                float noiseScale = 1.0 / max(_ParticleSize, 0.001);
                
                // 基础噪声，让它沿着腾跃方向流动
                float rawNoise = gradientNoise(uv * noiseScale - leapDir * time);
                // 将噪声范围调整到 0~1
                float fireMask = saturate(rawNoise + 0.5); 

                // --- 3. 绘制静态箭头主体 (带有燃烧消融效果) ---
                fixed4 colBody = SampleMain(uv, IN.color);
                
                // 根据 Intensity 和噪声，让箭头主体边缘产生透明度抖动消融
                float bodyThreshold = 1.0 - _Intensity * 0.7; // 主体不完全消融
                float bodyAlpha = smoothstep(bodyThreshold, bodyThreshold + 0.2, fireMask);
                colBody.a *= (1.0 - bodyAlpha * InsideUv(uv)); // 让被消融的部分变透明

                // --- 4. 实现粒子的腾跃效果 ---
                fixed4 colParticles = fixed4(0,0,0,0);
                
                // 只有当噪声值足够高时（处于燃烧剧烈区），才产生腾跃粒子
                // Threshold 越高，粒子越少
                float particleThreshold = max(1.0 - _Intensity * 1.5, 0.0); 

                if (fireMask > particleThreshold)
                {
                    // 计算粒子个体当前的寿命/腾跃阶段 (0~1)
                    // 使用指数函数让粒子在寿命末期快速消散
                    float particleLife = pow(saturate((fireMask - particleThreshold) / max(1.0 - particleThreshold, 0.001)), 1.5);
                    
                    // 计算个体偏移量。根据寿命，寿命越长偏得越远，最高偏 _LeapHeight
                    float individualLeap = particleLife * _LeapHeight;
                    
                    // 向腾跃方向的“反方向”进行采样，从而实现像素向上跳的效果
                    float2 particleUV = uv - leapDir * individualLeap;

                    if (InsideUv(particleUV) > 0.5)
                    {
                        colParticles = SampleMain(particleUV, IN.color);
                        // 应用火焰附魔颜色，保留原贴图的亮度
                        colParticles.rgb *= _BurnColor.rgb;
                        // 粒子在寿命末期变透明
                        colParticles.a *= (1.0 - particleLife);
                        // 初始箭头如果是不透明的，这里需要应用主体被消融前的 Alpha 或者是 1
                        colParticles.a *= step(0.01, colParticles.a); // 确保原图透明的地方粒子也透明
                    }
                }

                // --- 5. 合并主体和跳跃粒子 ---
                fixed4 finalColor = Over(colBody, colParticles);

                // 应用 tint 颜色的 Alpha
                finalColor.a *= IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(finalColor.a - 0.001);
                #endif

                // 最终效果使用加色模式可能在亮部效果更好，但这里使用标准 Over 以保持箭头形状
                return saturate(finalColor);
            }
            ENDCG
        }
    }
}