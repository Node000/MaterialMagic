Shader "Custom/RotatingPerspectiveGridLayers"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BgColor ("Ground Bg Color (地面背景色)", Color) = (0.1, 0.05, 0.15, 1)
        _SkyBgColor ("Sky Bg Color (天空背景色)", Color) = (0.05, 0.02, 0.1, 1)
        _GridColor ("Grid Color (网格线条颜色)", Color) = (0.0, 1.0, 1.0, 1)

        _HorizonY ("Horizon Y (地平线高度)", Range(0.0, 1.0)) = 0.5
        _VanishingX ("Vanishing X (消失点水平位置)", Range(0.0, 1.0)) = 0.5
        _GridRotationAngle ("Grid Rotation Angle (网格旋转角度)", Float) = 0.0
        _LayerAngleSpacing ("Layer Angle Spacing (层间角度)", Float) = 15.0
        _RenderAngleRange ("Render Angle Range (渲染角度范围)", Float) = 45.0
        _RenderAngleFade ("Render Range Edge Fade (边界淡化角度)", Float) = 8.0

        _HSpacing ("Horizontal Density (横线密度)", Float) = 5.0
        _VSpacing ("Vertical Density (竖线密度)", Float) = 5.0
        _LineWidth ("Line Width (线条宽度)", Float) = 1.5
        _Speed ("Forward Speed (移动速度)", Float) = 2.0
        _PerspectivePower ("Fall Perspective Power (下坠透视)", Range(1.0, 3.0)) = 1.0
        _GridVerticalOffset ("Fall Distance (下坠距离)", Float) = 0.0
        _FadeRange ("Fade Range (地平线渐隐范围)", Range(0.01, 0.5)) = 0.1
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
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            float4 _BgColor;
            float4 _SkyBgColor;
            float4 _GridColor;
            float _HorizonY;
            float _VanishingX;
            float _GridRotationAngle;
            float _LayerAngleSpacing;
            float _RenderAngleRange;
            float _RenderAngleFade;
            float _HSpacing;
            float _VSpacing;
            float _LineWidth;
            float _Speed;
            float _PerspectivePower;
            float _GridVerticalOffset;
            float _FadeRange;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float HorizontalLineMask(float coordinate)
            {
                float distanceToLine = abs(frac(coordinate - 0.5) - 0.5);
                float lineThickness = abs(ddy(coordinate)) * _LineWidth;
                return smoothstep(max(lineThickness, 0.00001), 0.0, distanceToLine);
            }

            float VerticalLineMask(float coordinate)
            {
                float distanceToLine = abs(frac(coordinate - 0.5) - 0.5);
                float lineThickness = min(abs(ddx(coordinate)) * _LineWidth, 0.45);
                return smoothstep(max(lineThickness, 0.00001), 0.0, distanceToLine);
            }

            float GridMask(float2 gridUV)
            {
                return max(VerticalLineMask(gridUV.x), HorizontalLineMask(gridUV.y));
            }

            float RotatingGridMask(float2 screenUV, float layerAngle)
            {
                float rotation = sin(radians(layerAngle));
                float rotationMagnitude = abs(rotation);
                float safeRotation = rotation >= 0.0 ? max(rotation, 0.0001) : min(rotation, -0.0001);
                float sourcePlaneDistance = -((screenUV.y - _HorizonY) / safeRotation);
                float visiblePlane = step(0.00001, sourcePlaneDistance) * smoothstep(0.0, 0.0005, rotationMagnitude);
                if (visiblePlane <= 0.0)
                    return 0.0;

                float depth = pow(1.0 / max(sourcePlaneDistance, 0.0001), _PerspectivePower);
                float2 gridUV;
                gridUV.x = (screenUV.x - _VanishingX) * depth * _VSpacing;
                gridUV.y = depth * _HSpacing - _Time.y * _Speed - _GridVerticalOffset;
                return GridMask(gridUV) * smoothstep(0.0, _FadeRange, sourcePlaneDistance) * visiblePlane;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float isSky = step(_HorizonY, i.uv.y);
                float4 currentBgColor = lerp(_BgColor, _SkyBgColor, isSky);
                float gridMask = 0.0;
                float layerSpacing = max(abs(_LayerAngleSpacing), 0.001);
                float angleRange = max(_RenderAngleRange, 0.0);
                float edgeFade = min(max(_RenderAngleFade, 0.0001), max(angleRange, 0.0001));
                float rotationPhase = fmod(_GridRotationAngle, layerSpacing);

                [unroll]
                for (int layer = 0; layer < 16; layer++)
                {
                    float layerAngle = rotationPhase + (layer - 7.5) * layerSpacing;
                    float angleMagnitude = abs(layerAngle);
                    if (angleMagnitude > angleRange)
                        continue;

                    float rangeFade = 1.0 - smoothstep(angleRange - edgeFade, angleRange, angleMagnitude);
                    gridMask = max(gridMask, RotatingGridMask(i.uv, layerAngle) * rangeFade);
                }

                return lerp(currentBgColor, _GridColor, gridMask);
            }
            ENDCG
        }
    }
}
