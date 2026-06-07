Shader "Custom/PerspectiveGridWithSky"
{
    Properties
    {
        _BgColor ("Ground Bg Color (地面背景色)", Color) = (0.1, 0.05, 0.15, 1)
        _SkyBgColor ("Sky Bg Color (天空背景色)", Color) = (0.05, 0.02, 0.1, 1)
        _GridColor ("Grid Color (网格线条颜色)", Color) = (0.0, 1.0, 1.0, 1)
        
        _HorizonY ("Horizon Y (地平线高度)", Range(0.0, 1.0)) = 0.5
        _VanishingX ("Vanishing X (消失点水平位置)", Range(0.0, 1.0)) = 0.5
        
        _HSpacing ("Horizontal Density (横线密度)", Float) = 5.0
        _VSpacing ("Vertical Density (竖线密度)", Float) = 5.0
        _LineWidth ("Line Width (线条宽度)", Float) = 1.5
        
        _Speed ("Forward Speed (移动速度)", Float) = 2.0
        _FadeRange ("Fade Range (地平线渐隐范围)", Range(0.01, 0.5)) = 0.1
        
        // Unity 内置特性：[Toggle] 会在材质面板生成一个勾选框
        [Toggle] _EnableSkyGrid ("Enable Sky Grid (开启天空网格)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0 // 支持 fwidth 抗锯齿
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _BgColor;
            float4 _SkyBgColor;
            float4 _GridColor;
            float _HorizonY;
            float _VanishingX;
            float _HSpacing;
            float _VSpacing;
            float _LineWidth;
            float _Speed;
            float _FadeRange;
            float _EnableSkyGrid;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. 判断当前像素是在地平线上方（天空）还是下方（地面）
                float isSky = step(_HorizonY, i.uv.y);
                
                // 2. 根据所在区域，动态选择背景色
                float4 currentBgColor = lerp(_BgColor, _SkyBgColor, isSky);

                // 3. 核心改动：计算当前点到地平线的“绝对距离”
                // 使用 abs 后，无论是地面(uv.y变小)还是天空(uv.y变大)，deltaY 都会从 0 开始向两边增加
                float deltaY = abs(i.uv.y - _HorizonY);
                
                // 4. 经典透视变换
                float depth = 1.0 / max(deltaY, 0.0001);
                float perspectiveX = (i.uv.x - _VanishingX) * depth;

                // 5. 构建网格空间的 UV
                float2 gridUV;
                gridUV.x = perspectiveX * _VSpacing;
                // 动画公式：因为上下对称了，减去时间会让天空和地面的线条同时向屏幕外流动（产生向前飞行的视觉感）
                gridUV.y = depth * _HSpacing - _Time.y * _Speed;

                // 6. 计算网格线与抗锯齿
                float2 wrappedUV = abs(frac(gridUV - 0.5) - 0.5);
                float2 derivatives = fwidth(gridUV);
                float2 lineThickness = derivatives * _LineWidth;
                float2 lineSegments = smoothstep(lineThickness, 0.0, wrappedUV);
                
                float gridMask = max(lineSegments.x, lineSegments.y);

                // 7. 边缘渐隐：靠近地平线时自动淡出，防止极远处线条挤在一起产生刺眼的摩尔纹
                float fade = smoothstep(0.0, _FadeRange, deltaY);
                gridMask *= fade;

                // 8. 选项开关控制：如果在天空区域，并且关闭了开关，则将网格遮罩强制归零
                if (isSky > 0.5 && _EnableSkyGrid < 0.5)
                {
                    gridMask = 0.0;
                }

                // 9. 最终颜色混合
                return lerp(currentBgColor, _GridColor, gridMask);
            }
            ENDCG
        }
    }
}