Shader "UI/MaterialModifiers/SimpleCRTGlitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)

        [Header(CRT Scanline Settings)]
        _ScanlineCount ("扫描线密集度", Float) = 150.0
        _ScanlineIntensity ("扫描线强度", Range(0.0, 1.0)) = 0.3

        [Header(Color Aberration)]
        _AberrationAmount ("RGB 颜色偏移幅度", Range(0.0, 0.05)) = 0.015

        [Header(Block Glitch Settings)]
        _BlockCount ("垂直分块数量 (块越小值越大)", Float) = 20.0
        _GlitchSpeed ("故障刷新速度", Float) = 10.0
        _GlitchIntensity ("错位偏移最大距离", Range(0.0, 0.5)) = 0.1
        _GlitchThreshold ("错位发生频率 (越高越少见)", Range(0.0, 1.0)) = 0.9

        // UI 模板保留
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
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            // CRT 参数
            float _ScanlineCount;
            float _ScanlineIntensity;
            
            // 颜色偏移
            float _AberrationAmount;

            // 块状错位
            float _BlockCount;
            float _GlitchSpeed;
            float _GlitchIntensity;
            float _GlitchThreshold;

            // --- 伪随机数生成器 ---
            float random(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453123);
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

                // ==========================================
                // 1. 块状错位计算 (Block Glitch)
                // ==========================================
                // 降低时间刷新率，产生“一卡一卡”的跳动感
                float blockTime = floor(_Time.y * _GlitchSpeed);
                
                // 将纵坐标划分为离散的网格块
                float blockY = floor(uv.y * _BlockCount);
                
                // 基于时间戳和块高度生成随机数 (0.0 到 1.0 之间)
                float noise = random(float2(blockY, blockTime));
                
                // 只有当随机数超过阈值时，才触发错位 (step 函数：noise > thresh ? 1 : 0)
                float isGlitch = step(_GlitchThreshold, noise);
                
                // 将 noise 映射到 -1 到 1 的方向，并应用强度
                float shiftOffset = (noise * 2.0 - 1.0) * _GlitchIntensity * isGlitch;
                
                // 应用横向偏移
                uv.x += shiftOffset;

                // ==========================================
                // 2. RGB 颜色分离 (Chromatic Aberration)
                // ==========================================
                // 错位发生时，颜色分离也随之加剧，增加视觉冲击力
                float currentAberration = _AberrationAmount + (abs(shiftOffset) * 0.5);

                float2 uvR = uv + float2(currentAberration, 0.0);
                float2 uvB = uv - float2(currentAberration, 0.0);

                // 采样三个通道（红色向右偏，蓝色向左偏，绿色居中）
                fixed4 colR = tex2D(_MainTex, uvR) + _TextureSampleAdd;
                fixed4 colG = tex2D(_MainTex, uv) + _TextureSampleAdd;
                fixed4 colB = tex2D(_MainTex, uvB) + _TextureSampleAdd;

                // 组合 RGB，并使用中心绿色的 Alpha 以防边缘穿帮
                fixed4 finalCol = fixed4(colR.r, colG.g, colB.b, colG.a);

                // ==========================================
                // 3. CRT 扫描线 (Scanlines)
                // ==========================================
                // 利用正弦波在 Y 轴生成密集的黑白条纹
                float scanline = sin(uv.y * _ScanlineCount * 3.14159);
                // 映射到 0~1，并通过 _ScanlineIntensity 控制明暗对比
                scanline = 1.0 - _ScanlineIntensity + ((scanline * 0.5 + 0.5) * _ScanlineIntensity);
                
                // 叠加扫描线变暗效果
                finalCol.rgb *= scanline;

                // 叠加上整体的 UI Tint Color
                finalCol *= IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                finalCol.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(finalCol.a - 0.001);
                #endif

                return finalCol;
            }
            ENDCG
        }
    }
}