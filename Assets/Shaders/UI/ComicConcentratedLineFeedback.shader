Shader "UI/ComicConcentratedLineFeedback"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _VignetteColor ("Vignette Color (C# 传入)", Color) = (1, 1, 1, 1)
        
        // 必须保留的参数，确保你的 C# 脚本 new Material 时不会报错
        _InnerRadius ("Inner Radius (内部空白半径)", Float) = 0.3
        _OuterRadius ("Outer Radius (外部基准半径)", Float) = 1.0
        _AspectScale ("Aspect Scale (屏幕长宽比)", Float) = 1.777
        _EdgePower ("Edge Power (控制三角形的锐利度)", Float) = 1.0
        
        [Header(Comic Speed Line Settings)]
        _LineCount ("集中线密度 (三角形数量)", Float) = 60.0
        _Randomness ("长度随机感", Range(0.0, 1.0)) = 0.6
        _FlickerSpeed ("动画闪烁速度 (设为0则静止)", Float) = 0.0
        
        // UGUI 模板必须的参数
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
            float4 _ClipRect;

            fixed4 _VignetteColor;
            float _InnerRadius;
            float _OuterRadius;
            float _AspectScale;
            float _EdgePower;
            
            float _LineCount;
            float _Randomness;
            float _FlickerSpeed;

            // 伪随机数生成器
            float random(float n)
            {
                return frac(sin(n) * 43758.5453123);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                
                // 1. 将 UV 居中并处理屏幕比例，确保放射线是正圆而非椭圆
                float2 centerUV = uv - 0.5;
                centerUV.x *= _AspectScale;

                // 2. 将直角坐标系转为极坐标系 (半径和角度)
                float r = length(centerUV);
                float angle = atan2(centerUV.y, centerUV.x); // 返回 -PI 到 PI

                // 将角度归一化到 0 ~ 1 范围
                float normAngle = (angle + 3.14159265) / (2.0 * 3.14159265);

                // 3. 将整个圆划分为 N 个扇形区域 (LineCount)
                float sectorId = floor(normAngle * _LineCount);
                // 当前像素在所在扇形中的局部角度位置 (0~1)
                float localAngle = frac(normAngle * _LineCount);

                // 如果赋予了闪烁速度，让随机种子随时间跳动产生狂躁的冲击感
                float timeOffset = floor(_Time.y * _FlickerSpeed);
                float rnd = random(sectorId + timeOffset);

                // 4. 构建尖刺形状 (Triangle Shape)
                // 使得每个扇区的中心处刺得最深 (1)，边缘处退回 (0)
                float spikeShape = 1.0 - abs(localAngle - 0.5) * 2.0;

                // 巧妙利用原来的 _EdgePower 参数来控制三角形的尖锐度
                // _EdgePower 越大，三角形越瘦削、锐利
                spikeShape = pow(max(0.001, spikeShape), max(0.1, _EdgePower));

                // 5. 计算当前像素所在扇区的“线条向内延伸的极限半径”
                float spikeLength = max(0.01, _OuterRadius - _InnerRadius);
                
                // 加上随机偏移，让每个三角形长短不齐，充满张力
                float currentInnerLimit = _InnerRadius + rnd * spikeLength * _Randomness;
                
                // 计算当前像素所在角度的边界半径
                float lineStartRadius = currentInnerLimit + (1.0 - spikeShape) * spikeLength;

                // 6. 硬切断：没有柔和的透明渐变！
                // 只要当前半径超过了计算出的边界，就显示线条，否则透明
                float mask = step(lineStartRadius, r);

                // 7. 输出颜色
                fixed4 finalColor = _VignetteColor;
                
                // Alpha = C#传入的颜色的Alpha * 顶点Alpha * 硬切断Mask
                finalColor.a = _VignetteColor.a * IN.color.a * mask;

                // UGUI 标准裁切
                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}