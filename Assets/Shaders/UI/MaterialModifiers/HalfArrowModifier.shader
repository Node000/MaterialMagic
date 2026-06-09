Shader "UI/MaterialModifiers/HalfArrowModifier"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        // --- 新增的控制参数 ---
        [Enum(Show Both,0,Show Upper Half,1,Show Lower Half,2)] _VisibleSide ("Visible Side", Float) = 0
        _LineAngle ("Fallback Line Angle (Degrees)", Range(0, 360)) = 45.0
        _FireLineAngle ("Fire/Up Line Angle (Degrees)", Range(0, 360)) = 45.0
        _WaterLineAngle ("Water/Down Line Angle (Degrees)", Range(0, 360)) = 225.0
        _WindLineAngle ("Wind/Left Line Angle (Degrees)", Range(0, 360)) = 135.0
        _EarthLineAngle ("Earth/Right Line Angle (Degrees)", Range(0, 360)) = 315.0
        _ArrowDirection ("Arrow Direction", Float) = 0
        _LineWidth ("Line Width", Range(0, 0.5)) = 0.02
        _LineLength ("Line Length", Float) = 1.5
        
        // --- 保留原有常用的效果控制参数 ---
        _EffectSpeed ("Effect Speed", Float) = 1
        _EffectStrength ("Effect Strength", Range(0,1)) = 0.3
        
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
            #pragma target 2.0
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
            
            // 我们的新参数
            float _VisibleSide;
            float _LineAngle;
            float _FireLineAngle;
            float _WaterLineAngle;
            float _WindLineAngle;
            float _EarthLineAngle;
            float _ArrowDirection;
            float _LineWidth;
            float _LineLength;
            float _EffectSpeed;
            float _EffectStrength;

            float InsideUv(float2 uv)
            {
                return step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0);
            }

            fixed4 SampleMain(float2 uv, fixed4 vertexColor)
            {
                float inside = InsideUv(uv);
                fixed4 color = (tex2D(_MainTex, saturate(uv)) + _TextureSampleAdd) * vertexColor;
                color.a *= inside;
                return color;
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
                float2 p = uv - 0.5; 

                // 1. 计算角度与方向
                float selectedLineAngle = _LineAngle;
                if (_ArrowDirection > 0.5 && _ArrowDirection < 1.5)
                    selectedLineAngle = _WaterLineAngle;
                else if (_ArrowDirection > 1.5 && _ArrowDirection < 2.5)
                    selectedLineAngle = _WindLineAngle;
                else if (_ArrowDirection > 2.5)
                    selectedLineAngle = _EarthLineAngle;
                else
                    selectedLineAngle = _FireLineAngle;

                float rad = selectedLineAngle * 0.0174532925; 
                float2 dir = float2(cos(rad), sin(rad));       
                float2 normal = float2(-sin(rad), cos(rad));   

                // 2. 根据模式计算切线：脆弱箭头固定切线并错位两半；半箭头让切线本身周期偏移
                float wave = sin(_Time.y * _EffectSpeed) * _EffectStrength;
                float lineOffset = _VisibleSide == 0.0 ? 0.0 : wave;
                float distToLine = dot(p, normal) - lineOffset;
                float projLength = dot(p, dir);
                float side = step(0.0, distToLine);

                fixed4 color;
                if (_VisibleSide == 0.0)
                {
                    float2 uvUpper = uv - dir * wave;
                    float2 uvLower = uv + dir * wave;
                    fixed4 colUpper = SampleMain(uvUpper, IN.color);
                    fixed4 colLower = SampleMain(uvLower, IN.color);
                    color = lerp(colLower, colUpper, side);
                }
                else
                {
                    color = SampleMain(uv, IN.color);
                    float visibility = _VisibleSide == 1.0 ? side : 1.0 - side;
                    color.a *= visibility;
                }

                // 3. 绘制中心纯黑线
                float inLineWidth = step(abs(distToLine), _LineWidth * 0.5);
                float inLineLength = step(abs(projLength), _LineLength * 0.5);
                float isBlackLine = inLineWidth * inLineLength;

                color.rgb = lerp(color.rgb, fixed3(0.0, 0.0, 0.0), isBlackLine);
                color.a = max(color.a, isBlackLine);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return saturate(color);
            }
            ENDCG
        }
    }
}