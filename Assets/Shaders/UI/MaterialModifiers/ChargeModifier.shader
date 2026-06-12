Shader "UI/MaterialModifiers/StaticTexturePixelDiscrete"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Bounds Settings)]
        _Padding ("防止超框的内部边距", Range(0.0, 0.4)) = 0.2

        [Header(Discrete Pixel Controls)]
        _ParticleRes ("离散颗粒分辨率 (越高方块越小)", Float) = 64.0
        _FrizzleDistance ("像素撕裂逸出距离", Range(0.0, 0.5)) = 0.15
        _Intensity ("剥离/撕裂像素密度", Range(0.0, 1.0)) = 0.3
        _FlickerFPS ("像素跳跃刷新帧率", Float) = 24.0
        
        [Header(Glow Effect)]
        _ParticleGlow ("飞出粒子的亮度倍率 (1为原图亮度)", Range(1.0, 5.0)) = 1.5

        // UI 遮罩相关参数保留
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
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
            
            float _Padding;
            float _ParticleRes;
            float _FrizzleDistance;
            float _Intensity;
            float _FlickerFPS;
            float _ParticleGlow;
            float4 _ClipRect;

            // --- 核心算法：高频无序哈希 (Hash) ---
            float hash12(float2 p)
            {
                float3 p3  = frac(float3(p.xyx) * .1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float2 hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(.1031, .1030, .0973));
                p3 += dot(p3, p3.yzx+33.33);
                return frac((p3.xx+p3.yz)*p3.zy);
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
                // --- 1. 计算边距防止超框 ---
                float2 uv = IN.texcoord;
                float2 paddedUV = (uv - _Padding) / max(0.001, 1.0 - 2.0 * _Padding);
                float inBounds = step(0.0, paddedUV.x) * step(paddedUV.x, 1.0) * step(0.0, paddedUV.y) * step(paddedUV.y, 1.0);

                // --- 2. 底图渲染 (绝对不膨胀变形) ---
                fixed4 baseColor = fixed4(0,0,0,0);
                if (inBounds > 0.5) 
                {
                    baseColor = tex2D(_MainTex, paddedUV) * IN.color;
                }

                // --- 3. 离散网格化 (Pixelation Grid) ---
                float2 gridUV = floor(paddedUV * _ParticleRes) / _ParticleRes;
                float t = floor(_Time.y * _FlickerFPS);

                // --- 4. 生成乱数偏移，抓取真正的贴图纹理 ---
                float noiseVal = hash12(gridUV + t);
                float isSpark = step(1.0 - _Intensity, noiseVal);

                // 决定这个网格块将要“偷取”哪里的原图象素
                float2 randomDir = hash22(gridUV + t + 15.0) * 2.0 - 1.0;
                
                // ⚠️ 关键点：用平滑的 paddedUV 加上网格化的偏移，
                // 这样飞出去的粒子方块内部，依然保留着原贴图的高清细节纹理！
                float2 distUV = paddedUV + randomDir * _FrizzleDistance;
                float distInBounds = step(0.0, distUV.x) * step(distUV.x, 1.0) * step(0.0, distUV.y) * step(distUV.y, 1.0);
                
                fixed4 finalColor = baseColor;

                // --- 5. 覆盖离散像素块 ---
                if (isSpark > 0.5 && distInBounds > 0.5)
                {
                    // 直接对偏移后的 UV 采样原始贴图
                    fixed4 sparkColor = tex2D(_MainTex, distUV) * IN.color;
                    
                    // 只有当抓取到的地方是实体（非透明）像素时，才显示飞出块
                    if (sparkColor.a > 0.05)
                    {
                        // 将抓取到的像素覆盖上去，并乘以提亮倍率
                        finalColor.rgb = lerp(finalColor.rgb, sparkColor.rgb * _ParticleGlow, sparkColor.a);
                        finalColor.a = max(finalColor.a, sparkColor.a);
                    }
                }

                // --- 6. 表面像素的闪烁躁动 (Optional) ---
                // 不使用任何杂色，仅仅让卡牌本体的某些像素块亮度跳动，增加电流干扰的错觉
                float surfaceNoise = hash12(gridUV - t * 2.0);
                float isSurfaceStatic = step(1.0 - _Intensity * 0.2, surfaceNoise) * step(0.1, baseColor.a);
                if (isSurfaceStatic > 0.5)
                {
                    finalColor.rgb *= 1.3; // 原图象素瞬间高亮
                }

                // UI 裁剪逻辑
                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(finalColor.a - 0.001);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}