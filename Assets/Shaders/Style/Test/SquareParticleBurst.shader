Shader "Style/Test/SquareParticleBurst"
{
    Properties
    {
        [PerRendererData] _MainTex ("Particle Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _EdgeColor ("Edge Color", Color) = (0.95,0.35,0.65,1)
        _EdgeWidth ("Edge Width", Range(0,0.45)) = 0.12
        _GlowStrength ("Glow Strength", Range(0,2)) = 0.4
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
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
            fixed4 _EdgeColor;
            float _EdgeWidth;
            float _GlowStrength;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.texcoord = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 particle = tex2D(_MainTex, i.texcoord) * i.color;
                float edgeDistance = min(min(i.texcoord.x, 1.0 - i.texcoord.x), min(i.texcoord.y, 1.0 - i.texcoord.y));
                float edge = 1.0 - smoothstep(_EdgeWidth, _EdgeWidth + 0.08, edgeDistance);
                particle.rgb = lerp(particle.rgb, _EdgeColor.rgb, edge * _EdgeColor.a);
                particle.rgb += _EdgeColor.rgb * edge * _GlowStrength * particle.a;
                return saturate(particle);
            }
            ENDCG
        }
    }
}
