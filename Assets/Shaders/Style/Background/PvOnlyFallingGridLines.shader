Shader "Style/Background/PvOnlyFallingGridLines"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _LineColor ("Line Color", Color) = (1, 0.2, 0.5, 1)
        _HorizontalDensity ("Horizontal Density", Float) = 12
        _VerticalDensity ("Vertical Density", Float) = 8
        _LineWidth ("Line Width", Float) = 1.33
        _Speed ("Vertical Fall Speed", Float) = 7
        _Phase ("Phase", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float4 vertex : SV_POSITION;
            };

            float4 _LineColor;
            float _HorizontalDensity;
            float _VerticalDensity;
            float _LineWidth;
            float _Speed;
            float _Phase;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 gridUv;
                gridUv.x = i.uv.x * _VerticalDensity;
                gridUv.y = i.uv.y * _HorizontalDensity + _Time.y * _Speed + _Phase;

                float2 wrappedUv = abs(frac(gridUv - 0.5) - 0.5);
                float2 thickness = fwidth(gridUv) * _LineWidth;
                float gridMask = max(smoothstep(thickness.x, 0.0, wrappedUv.x), smoothstep(thickness.y, 0.0, wrappedUv.y));

                fixed4 color = _LineColor * i.color;
                color.a *= gridMask;
                return color;
            }
            ENDCG
        }
    }
}
