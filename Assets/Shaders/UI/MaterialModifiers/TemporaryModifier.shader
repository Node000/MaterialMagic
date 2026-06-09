Shader "UI/MaterialModifiers/VoidPointCloud"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)

        [Header(Void Disintegration)]
        _FillAmount ("实体留存率 (控制消散动画 1->0)", Range(0.0, 1.0)) = 1.0
        
        [Header(Point Cloud Settings)]
        _DotRes ("点云网格分辨率", Float) = 80.0
        _DotShape ("点阵形状 (0=方形像素, 1=圆点)", Range(0.0, 1.0)) = 0.0

        [Header(Quantum Disturbance)]
        _Disturbance ("扰动强度 (像素大小变化)", Range(0.0, 0.5)) = 0.2
        _WaveSpeed ("扰动波动速度", Float) = 3.0
        _WaveScale ("扰动波纹密度", Float) = 0.15
        
        [Header(Void Energy)]
        [HDR]_VoidColor ("虚无能量光晕 (快消散时发光)", Color) = (0.5, 0.0, 1.0, 1)
        _GlowIntensity ("光晕强度", Range(0.0, 2.0)) = 1.0

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

            float _FillAmount;
            float _DotRes;
            float _DotShape;
            float _Disturbance;
            float _WaveSpeed;
            float _WaveScale;
            fixed4 _VoidColor;
            float _GlowIntensity;

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
                
                // 1. 获取原图颜色与透明度
                fixed4 baseColor = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;
                float sourceAlpha = baseColor.a;

                // 2. 点云网格化 (Grid System)
                float2 gridUV = uv * _DotRes;
                float2 cellID = floor(gridUV);       // 当前像素属于哪个网格 (整数ID)
                float2 localUV = frac(gridUV);       // 网格内部的局部坐标 (0.0 ~ 1.0)

                // 3. 周期性量子扰动计算 (Quantum Disturbance)
                // 基于每个网格的 ID 生成复合正弦波，形成流动的扰动场
                float t = _Time.y * _WaveSpeed;
                float wave = sin(cellID.x * _WaveScale + t) 
                           * cos(cellID.y * _WaveScale - t * 0.8) 
                           + sin((cellID.x - cellID.y) * _WaveScale * 1.5 + t * 1.2);
                wave = wave * 0.333; // 将波形归一化到大概 -1 到 1 的范围

                // 4. 计算当前网格的实体填充率
                // 基础填充率 + 扰动波幅
                float currentFill = _FillAmount + wave * _Disturbance;
                currentFill = saturate(currentFill); // 限制在 0 ~ 1 之间

                // 5. 将填充率转化为像素块的大小 (Radius)
                // 原图的 alpha 也会乘进来，这意味着原图半透明的边缘，会自动变成极小的点，完美过渡！
                // 方形最大半径是 0.5，圆形为了填满整个方块最大半径需要是 0.707 (根号0.5)
                float maxRadius = lerp(0.5, 0.707, _DotShape);
                float radius = currentFill * sourceAlpha * maxRadius;

                // 6. 形状距离计算 (Shape Distance)
                // 局部坐标中心为 0.5。计算当前像素到中心的距离
                float2 distToCenter = abs(localUV - 0.5);
                float squareDist = max(distToCenter.x, distToCenter.y); // 方形距离
                float circleDist = length(localUV - 0.5);               // 圆形距离
                
                // 根据参数混合方形或圆形
                float shapeDist = lerp(squareDist, circleDist, _DotShape);

                // 7. 硬边缘切断 (核心机制：大小代替透明度)
                // 只要距离大于设定的半径，alpha 瞬间变为 0；小于半径，alpha 瞬间为 1
                float dotMask = step(shapeDist, radius);

                // 8. 虚无能量自发光 (Void Energy Glow)
                // 当像素变小（即将消散）时，为其叠加虚无的能量颜色
                float shrinkFactor = 1.0 - currentFill; // 点越小，这个值越接近1
                baseColor.rgb += _VoidColor.rgb * shrinkFactor * _GlowIntensity;

                // 9. 应用最终透明度
                baseColor.a = dotMask;

                #ifdef UNITY_UI_CLIP_RECT
                baseColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(baseColor.a - 0.001);
                #endif

                return baseColor;
            }
            ENDCG
        }
    }
}