Shader "UI/MaterialModifiers/PixelSlimeClone"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)

        [Header(Clone Animation)]
        _CycleLength ("动画周期时长 (秒)", Float) = 3.0
        _SplitDistance ("复制体分裂距离", Range(0.0, 1.0)) = 0.4
        _SplitAngle ("复制体旋转角度", Range(0.0, 1.5)) = 0.5

        // 【修复点】：去除了 Header 中的特殊符号 &，防止 Unity 解析器崩溃
        [Header(Pixel Art And Slime Settings)]
        _PixelRes ("像素网格分辨率 (匹配你的素材)", Float) = 32.0
        _BridgeWidth ("粘液拉丝粗细", Range(0.01, 0.5)) = 0.15
        _MetaballThreshold ("融球融合阈值 (决定粘稠度)", Range(0.1, 1.5)) = 0.8
        _GooNoise ("粘液边缘不规则度", Range(0.0, 0.2)) = 0.05
        
        // 【修复点】：增加了 [HDR] 后面的空格
        [HDR] _GooColor ("粘连部分颜色 (建议取箭头主色)", Color) = (0.8, 0.2, 0.3, 1)

        [Header(System)]
        _ExpandBounds ("画布膨胀系数 (防裁切)", Float) = 2.0

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

            float _CycleLength;
            float _SplitDistance;
            float _SplitAngle;
            
            float _PixelRes;
            float _BridgeWidth;
            float _MetaballThreshold;
            float _GooNoise;
            
            fixed4 _GooColor;
            float _ExpandBounds;

            // --- 旋转矩阵 ---
            float2 rotate(float2 v, float a) 
            {
                float s = sin(a);
                float c = cos(a);
                return float2(v.x * c - v.y * s, v.x * s + v.y * c);
            }

            // --- 线段距离场 (用于计算拉丝骨架) ---
            float sdLine(float2 p, float2 a, float2 b) 
            {
                float2 pa = p - a, ba = b - a;
                float h = clamp(dot(pa, ba) / max(0.0001, dot(ba, ba)), 0.0, 1.0);
                return length(pa - ba * h);
            }

            // --- 防止超出 [0,1] 产生重复平铺的裁切遮罩 ---
            float boundsMask(float2 uv) 
            {
                return step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                v.vertex.xy *= _ExpandBounds;
                
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = (v.texcoord - 0.5) * _ExpandBounds + 0.5;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 rawUV = IN.texcoord;
                float2 pixelUV = floor(rawUV * _PixelRes) / max(1.0, _PixelRes);

                float t = frac(_Time.y / max(0.1, _CycleLength));
                
                float splitProg = 0.0;
                if (t < 0.1) {
                    splitProg = 0.0; 
                } else if (t < 0.5) {
                    float normT = (t - 0.1) / 0.4;
                    splitProg = pow(normT, 1.5); 
                } else if (t < 0.6) {
                    splitProg = 1.0; 
                } else {
                    float normT = (t - 0.6) / 0.4;
                    splitProg = exp(-10.0 * normT) * cos(35.0 * normT);
                }

                float2 splitDir = normalize(float2(1.0, 0.4));
                float2 c1 = float2(0.5, 0.5) - splitDir * (_SplitDistance * splitProg * 0.15);
                float2 c2 = float2(0.5, 0.5) + splitDir * (_SplitDistance * splitProg);
                
                float angle1 = 0.0;
                float angle2 = -_SplitAngle * splitProg; 

                float2 uv1 = rotate(pixelUV - c1, angle1) + float2(0.5, 0.5);
                float2 uv2 = rotate(pixelUV - c2, angle2) + float2(0.5, 0.5);

                fixed4 col1 = (tex2D(_MainTex, uv1) + _TextureSampleAdd) * boundsMask(uv1);
                fixed4 col2 = (tex2D(_MainTex, uv2) + _TextureSampleAdd) * boundsMask(uv2);

                float lineDist = sdLine(pixelUV, c1, c2);

                float noise = sin(pixelUV.x * 45.0 + _Time.y * 15.0) * cos(pixelUV.y * 35.0 - _Time.y * 12.0);
                lineDist += noise * _GooNoise * splitProg;

                float currentBridgeW = _BridgeWidth * max(0.0, 1.0 - pow(abs(splitProg), 1.5));
                float bridgeDensity = max(0.0, 1.0 - lineDist / max(0.001, currentBridgeW));

                float totalDensity = col1.a + col2.a + bridgeDensity;
                float alphaMask = step(_MetaballThreshold, totalDensity);

                fixed4 finalCol = fixed4(0, 0, 0, 0);

                if (col2.a > 0.5) 
                {
                    finalCol = col2;
                }
                else if (col1.a > 0.5) 
                {
                    finalCol = col1;
                }
                else 
                {
                    finalCol = _GooColor;
                }

                finalCol.a = alphaMask * IN.color.a;
                finalCol.rgb *= IN.color.rgb;

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