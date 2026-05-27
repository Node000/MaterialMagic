Shader "UI/IntentRipple"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Progress ("Progress", Range(0,1)) = 0
        _RingWidth ("Ring Width", Range(0.01,0.4)) = 0.08
        _RingSpacing ("Ring Spacing", Range(0.05,0.5)) = 0.18
        _RingCount ("Ring Count", Range(1,6)) = 3
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Progress;
            float _RingWidth;
            float _RingSpacing;
            float _RingCount;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 centered = i.texcoord - 0.5;
                float dist = length(centered) * 2.0;
                float alpha = 0;
                for (int ring = 0; ring < 6; ring++)
                {
                    if (ring >= _RingCount)
                        break;
                    float radius = _Progress - ring * _RingSpacing;
                    float ringAlpha = 1.0 - smoothstep(0, _RingWidth, abs(dist - radius));
                    alpha = max(alpha, ringAlpha);
                }
                alpha *= saturate(1.0 - _Progress);
                fixed4 col = i.color;
                col.a *= alpha;
                return col;
            }
            ENDCG
        }
    }
}
