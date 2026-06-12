Shader "UI/MaterialModifiers/CardEternityDatamosh"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Virtual Bounds System)]
        _Padding ("内部透明边距 (防止位移被切边)", Range(0.0, 0.4)) = 0.2

        [Header(Idle Animation)]
        _IdleSpeed ("待机游离速度", Float) = 1.5
        _IdleAmplitude ("待机游离幅度", Range(0.0, 0.2)) = 0.03

        [Header(Datamosh Glitch Controls)]
        _MoshIntensity ("【核心】凝滞撕裂强度", Range(0.0, 1.0)) = 0.3
        _BlockSize ("视频压缩区块分辨率 (越小块越大)", Float) = 24.0
        _MoshFPS ("凝滞刷新帧率 (卡顿感)", Float) = 8.0
        _SmearLength ("像素拖拽滞留长度", Range(0.0, 0.5)) = 0.2
        _ChromaShift ("色度分离/绿紫溢出强度", Range(0.0, 0.1)) = 0.03

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
            float _IdleSpeed;
            float _IdleAmplitude;

            float _MoshIntensity;
            float _BlockSize;
            float _MoshFPS;
            float _SmearLength;
            float _ChromaShift;
            
            float4 _ClipRect;

            // --- 核心算法：高频无序哈希 ---
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
                float2 uv = IN.texcoord;
                
                // --- 1. 防止越界切边的内部空间缩放 ---
                float2 paddedUV = (uv - _Padding) / max(0.001, 1.0 - 2.0 * _Padding);

                // --- 2. 待机幽灵游离 (Idle Wobble) ---
                // 基于时间的正弦波让卡牌在局部 UV 空间内持续进行类似呼吸的 8 字形微漂移
                float timeFloat = _Time.y * _IdleSpeed;
                float2 idleOffset = float2(sin(timeFloat), cos(timeFloat * 0.73)) * _IdleAmplitude;
                float2 baseUV = paddedUV + idleOffset;

                // --- 3. 视频压缩宏区块网格 (Macroblock Grid) ---
                // 模拟 MPEG 视频压缩时 16x16 的宏区块
                float2 gridUV = floor(paddedUV * _BlockSize) / _BlockSize;
                
                // 强制将时间降低为极低的帧率 (比如 8 FPS)
                // 这会导致区块的变化是“一卡一卡”的，这是凝滞感的核心！
                float tGrid = floor(_Time.y * _MoshFPS);

                // --- 4. 生成 Datamosh 错误运动向量 ---
                // 每个区块生成一个随机数，决定它当前帧是否发生“解码故障”
                float blockNoise = hash12(gridUV + tGrid);
                // 用 Intensity 控制故障区块的数量
                float isMoshed = step(1.0 - _MoshIntensity, blockNoise);

                // 如果发生故障，生成一个用于拖拽像素的“滞留向量 (Smear Vector)”
                // 我们让向量偏向于卡牌闲置移动的相反方向，制造“上一帧留在原地”的错觉
                float2 randomDir = hash22(gridUV + tGrid * 0.5) * 2.0 - 1.0;
                float2 smearOffset = (randomDir * 0.5 - idleOffset * 2.0) * _SmearLength;

                // --- 5. 组合扭曲 UV ---
                // 正常区块使用 baseUV 移动；故障区块的 UV 被强制拖拽偏移
                float2 finalUV = baseUV + smearOffset * isMoshed;

                // 边界检测：防止采样到贴图外部出现循环伪影
                float inBounds = step(0.0, finalUV.x) * step(finalUV.x, 1.0) * step(0.0, finalUV.y) * step(finalUV.y, 1.0);
                if (inBounds < 0.5) return fixed4(0,0,0,0);

                // --- 6. 采样与色度分离 (Chroma Shift / YUV Error) ---
                fixed4 finalColor = fixed4(0,0,0,0);
                
                if (isMoshed > 0.5)
                {
                    // 当区块处于凝滞状态时，产生 YUV 视频特有的颜色分离（偏品红/偏绿）
                    // 我们分别错位采样 R 和 B 通道，保留 G 通道
                    float2 chromaVec = randomDir * _ChromaShift;
                    
                    fixed r = tex2D(_MainTex, saturate(finalUV + chromaVec)).r;
                    fixed4 gCol = tex2D(_MainTex, saturate(finalUV));
                    fixed b = tex2D(_MainTex, saturate(finalUV - chromaVec)).b;
                    
                    finalColor = fixed4(r, gCol.g, b, gCol.a) * IN.color;
                }
                else
                {
                    // 正常区块直接采样
                    finalColor = tex2D(_MainTex, finalUV) * IN.color;
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