Shader "UI/MaterialModifiers/CardPixelMetalDeflection_Fixed"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("整体变色 (Tint)", Color) = (1,1,1,1)

        [Header(Local 3D Deflection)]
        _Padding ("防止超框的虚拟边距", Range(0.0, 0.4)) = 0.15
        _PerspectiveDist ("透视强度 (越小3D感越强)", Range(0.5, 5.0)) = 1.5
        _UIAspect ("卡牌宽高比 (宽除以高)", Float) = 0.7
        _TiltSpeed ("偏转周期速度", Float) = 1.5
        _TiltAmount ("偏转幅度 (角度)", Range(0.0, 0.6)) = 0.35
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
                float2 angles : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _Padding;
            float _PerspectiveDist;
            float _UIAspect;
            float _TiltSpeed;
            float _TiltAmount;
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
                
                // 仅计算角度传给片元着色器，绝不修改顶点坐标系！
                float t = _Time.y * _TiltSpeed;
                float angleX = sin(t) * _TiltAmount;
                float angleY = cos(t * 0.8) * _TiltAmount; 

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                OUT.angles = float2(angleX, angleY);

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // ==========================================
                // 1. Ray-Plane Intersection (数学推导真3D UV)
                // ==========================================
                float2 uv = IN.texcoord;
                float2 p = uv - 0.5;

                // 腾出内边距防止 3D 偏转时边缘被切断
                p = p / max(0.001, 1.0 - 2.0 * _Padding);
                // 修正长宽比带来的旋转扭曲
                p.y /= _UIAspect;

                // 取出顶点传来的旋转角
                float sY = sin(IN.angles.y); float cY = cos(IN.angles.y);
                float sX = sin(IN.angles.x); float cX = cos(IN.angles.x);

                // 构建旋转后的 3D 坐标轴 (Tangent, Bitangent, Normal)
                float3 T = float3(cY, sY * sX, -sY * cX);
                float3 B = float3(0, cX, sX);
                float3 N = float3(-sY, cY * sX, -cY * cX);

                // 虚拟相机发射射线
                float dist = _PerspectiveDist;
                float3 rayDir = float3(p.x, p.y, dist);
                
                // 计算射线与 3D 偏转卡牌平面的交点 t
                float t_int = (dist * N.z) / dot(rayDir, N);
                float3 P = float3(0, 0, -dist) + t_int * rayDir;

                // 把 3D 交点映射回 2D UV 空间
                float2 localUV;
                localUV.x = dot(P, T);
                localUV.y = dot(P, B);

                // 还原长宽比并居中
                localUV.y *= _UIAspect;
                localUV += 0.5;

                // 越界裁切
                if (localUV.x < 0.0 || localUV.x > 1.0 || localUV.y < 0.0 || localUV.y > 1.0)
                    return fixed4(0,0,0,0);

                // ==========================================
                // 2. 像素化与 3D 动态法线同步
                // ==========================================
                float2 pixelUV = floor(localUV * _PixelResolution) / _PixelResolution;

                // 先计算未旋转的微凸法线
                float3 localNorm = normalize(float3((pixelUV.x - 0.5) * _Bulge, (pixelUV.y - 0.5) * _Bulge, 1.0));
                
                // 【核心】将法线也用同样的矩阵进行真实的 3D 旋转！
                float3 worldNorm = localNorm.x * T + localNorm.y * B - localNorm.z * N; // (N朝向-z)
                worldNorm = normalize(worldNorm);

                // ==========================================
                // 3. 复合频段像素金属反射
                // ==========================================
                fixed4 baseColor = (tex2D(_MainTex, localUV) + _TextureSampleAdd) * IN.color;

                float3 viewDir = float3(0, 0, -1);
                float3 R = reflect(viewDir, worldNorm); 

                float wave1 = sin(R.x * _BandDensity + R.y * _BandDensity * 0.5);
                float wave2 = sin(R.x * _BandDensity * -0.8 + R.y * _BandDensity * 1.2);
                float wave3 = cos((R.x + R.y) * _BandDensity * 1.5);

                float metalVal = (wave1 + wave2 + wave3) * 0.333;
                metalVal = metalVal * 0.5 + 0.5;

                metalVal = pow(metalVal, _MetalContrast);
                metalVal = floor(metalVal * _ColorSteps) / _ColorSteps;

                fixed3 metalColor = lerp(_DarkMetalColor.rgb, _LightMetalColor.rgb, metalVal);
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