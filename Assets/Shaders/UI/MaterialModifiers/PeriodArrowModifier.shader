Shader "UI/MaterialModifiers/CardPixelMetalDeflection"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("整体变色 (Tint)", Color) = (1,1,1,1)

        [Header(3D Deflection Settings)]
        _TiltSpeed ("偏转周期速度", Float) = 1.5
        _TiltAmount ("偏转幅度 (角度)", Range(0.0, 0.6)) = 0.35
        _Perspective ("伪透视强度 (近大远小)", Range(0.0, 0.005)) = 0.002
        _Bulge ("卡牌表面微凸感", Range(0, 2)) = 0.5

        [Header(Pixelated Metal Settings)]
        _PixelResolution ("像素化网格分辨率", Float) = 64
        _ColorSteps ("金属色阶数量 (复古感)", Range(2, 16)) = 4
        _BandDensity ("金属反射纹理密度", Float) = 15
        _MetalContrast ("金属质感对比度", Range(0.5, 5.0)) = 2.0
        
        [HDR]_LightMetalColor ("金属亮部颜色 (流光)", Color) = (1, 0.95, 0.7, 1)
        _DarkMetalColor ("金属暗部颜色", Color) = (0.2, 0.15, 0.05, 1)
        _ShineIntensity ("高光叠加强度", Range(0, 3)) = 1.5

        // UI 模板相关
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
                float2 angles : TEXCOORD2; // 用于传递倾斜角度给片元着色器
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _TiltSpeed;
            float _TiltAmount;
            float _Perspective;
            float _Bulge;

            float _PixelResolution;
            float _ColorSteps;
            float _BandDensity;
            float _MetalContrast;
            fixed4 _LightMetalColor;
            fixed4 _DarkMetalColor;
            float _ShineIntensity;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                // ==========================================
                // 1. 真实的 3D 偏转 (应用在 UI 本地坐标)
                // ==========================================
                float t = _Time.y * _TiltSpeed;
                float angleX = sin(t) * _TiltAmount;
                // 乘以 0.8 让 XY 的摇晃不同步，显得更生动自然
                float angleY = cos(t * 0.8) * _TiltAmount; 

                float sY = sin(angleY); float cY = cos(angleY);
                float sX = sin(angleX); float cX = cos(angleX);

                float3 pos = v.vertex.xyz;

                // 绕 Y 轴旋转
                float x1 = pos.x * cY;
                float z1 = -pos.x * sY; // UI 初始 Z 为 0

                // 绕 X 轴旋转
                float y2 = pos.y * cX - z1 * sX;
                float z2 = pos.y * sX + z1 * cX;

                // 伪透视：因为 UI 是正交相机，Z 轴变化不会产生近大远小
                // 我们手动除以一个透视系数，让翻向背面的顶点缩小
                float perspective = 1.0 + z2 * _Perspective;
                pos.x = x1 / perspective;
                pos.y = y2 / perspective;
                pos.z = z2;

                OUT.worldPosition = float4(pos, 1.0); // 传给 UI 裁切
                OUT.vertex = UnityObjectToClipPos(float4(pos, 1.0));
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                OUT.angles = float2(angleX, angleY);

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // 原图色彩
                fixed4 baseColor = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;
                float sourceAlpha = baseColor.a;

                // ==========================================
                // 2. 像素化网格 (Pixelation) 
                // ==========================================
                // 仅对光照计算进行像素化，让金属反光产生像素块，但保留原图精度
                float2 pixelUV = floor(uv * _PixelResolution) / _PixelResolution;

                // ==========================================
                // 3. 计算基于偏转的动态法线
                // ==========================================
                // 利用像素化 UV 制造一个微凸的表面法线
                float3 norm = normalize(float3((pixelUV.x - 0.5) * _Bulge, (pixelUV.y - 0.5) * _Bulge, 1.0));

                float angleX = IN.angles.x;
                float angleY = IN.angles.y;
                float sY = sin(angleY); float cY = cos(angleY);
                float sX = sin(angleX); float cX = cos(angleX);

                // 根据顶点的偏转角度同步旋转法线
                // 绕 Y
                float nx1 = norm.x * cY - norm.z * sY;
                float nz1 = norm.x * sY + norm.z * cY;
                norm.x = nx1; norm.z = nz1;
                // 绕 X
                float ny2 = norm.y * cX - norm.z * sX;
                float nz2 = norm.y * sX + norm.z * cX;
                norm.y = ny2; norm.z = nz2;
                
                norm = normalize(norm);

                // ==========================================
                // 4. 复合频段金属反射 (Multi-band Anisotropic)
                // ==========================================
                // 假设视角看向屏幕内部 (0,0,-1)
                float3 viewDir = float3(0, 0, -1);
                // 计算反射向量
                float3 R = reflect(viewDir, norm); 

                // 利用反射向量的 X 和 Y 生成 3 层交错的正弦波，模拟复杂的周边环境反射
                float wave1 = sin(R.x * _BandDensity + R.y * _BandDensity * 0.5);
                float wave2 = sin(R.x * _BandDensity * -0.8 + R.y * _BandDensity * 1.2);
                float wave3 = cos((R.x + R.y) * _BandDensity * 1.5);

                // 合并频段并映射到 0~1 (此时会产生极度真实的金属亮暗斑块)
                float metalVal = (wave1 + wave2 + wave3) * 0.333;
                metalVal = metalVal * 0.5 + 0.5;

                // ==========================================
                // 5. 像素感与色阶强化 (Color Stepping)
                // ==========================================
                // 增加对比度，让金属反光变硬朗
                metalVal = pow(metalVal, _MetalContrast);
                // 阶梯化处理：将平滑的渐变切断为 N 个色阶，产生复古像素游戏的限制发色数质感
                metalVal = floor(metalVal * _ColorSteps) / _ColorSteps;

                // ==========================================
                // 6. 最终颜色合成
                // ==========================================
                // 混合金属明暗颜色
                fixed3 metalColor = lerp(_DarkMetalColor.rgb, _LightMetalColor.rgb, metalVal);
                
                // 将金属底色与原图正片叠底，再加上强烈的像素高光
                baseColor.rgb = (baseColor.rgb * metalColor) + (metalVal * _LightMetalColor.rgb * _ShineIntensity);

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