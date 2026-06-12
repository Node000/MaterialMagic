Shader "UI/MaterialModifiers/CardRhythmPixelImpact_FixedLocal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        
        [Header(Virtual Bounds System)]
        _Padding ("内部透明边距 (防止跳出框被切边)", Range(0.0, 0.4)) = 0.2

        [Header(Physics and Spring Animation)]
        _BPM ("音乐节拍 (BPM)", Float) = 120.0
        _JumpHeight ("跃起高度 (基于卡牌高度比例)", Range(0.0, 1.0)) = 0.3
        _ImpactDepth ("撞击下砸深度", Range(0.0, 0.5)) = 0.1
        _SquashAmount ("撞击挤压程度 (压扁)", Range(0.0, 1.0)) = 0.4
        _StretchAmount ("下坠拉伸程度 (变长)", Range(0.0, 1.0)) = 0.3

        [Header(Retro Pixel Shockwave)]
        _WaveSpeed ("波段上升速度", Float) = 2.0
        _WaveWidth ("波段宽度", Range(0.05, 0.5)) = 0.25
        _FlashSteps ("闪光阶梯段数", Float) = 3.0
        
        [Header(Glitch Shake)]
        _ShakeIntensity ("横向撕裂震动强度", Range(0.0, 0.2)) = 0.05
        _VibrationRes ("像素网格分辨率", Float) = 32.0
        _GlitchFPS ("故障帧率 (卡顿感)", Float) = 15.0
        [HDR]_FlashColor ("撞击电音闪光", Color) = (0.2, 1.0, 0.8, 1)

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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };

            sampler2D _MainTex;
            fixed4 _Color;
            
            float _Padding;
            float _BPM, _JumpHeight, _ImpactDepth, _SquashAmount, _StretchAmount;
            float _WaveSpeed, _WaveWidth, _FlashSteps, _ShakeIntensity, _VibrationRes, _GlitchFPS;
            fixed4 _FlashColor;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                // 顶点着色器不再处理位移，仅传递标准的 UI 坐标
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float beat = frac(_Time.y * (_BPM / 60.0));
                
                float yOffset = 0.0;
                float scaleX = 1.0;
                float scaleY = 1.0;

                // ==========================================
                // 1. 物理引擎计算 (此时均为比例系数)
                // ==========================================
                if (beat < 0.4) 
                {
                    float t = beat / 0.4;
                    float jumpSpring = exp(-6.0 * t) * cos(15.0 * t);
                    yOffset = _JumpHeight * (1.0 - jumpSpring);
                    scaleY = 1.0 + _StretchAmount * jumpSpring * 0.5;
                    scaleX = 1.0 - _StretchAmount * jumpSpring * 0.25;
                }
                else if (beat < 0.5)
                {
                    float t = (beat - 0.4) / 0.1;
                    yOffset = lerp(_JumpHeight, -_ImpactDepth, pow(t, 2.0));
                    scaleY = lerp(1.0, 1.0 + _StretchAmount, t);
                    scaleX = lerp(1.0, 1.0 - _StretchAmount * 0.5, t);
                }
                else 
                {
                    float t = (beat - 0.5) / 0.5;
                    float rebound = exp(-8.0 * t) * cos(25.0 * t);
                    yOffset = -_ImpactDepth * rebound;
                    scaleY = 1.0 - _SquashAmount * rebound;
                    scaleX = 1.0 + _SquashAmount * rebound * 0.5;
                }

                // ==========================================
                // 2. 虚拟边距与局部 UV 物理映射
                // ==========================================
                float2 uv = IN.texcoord;
                
                // 将真实的贴图缩小，腾出跳跃空间
                float2 paddedUV = (uv - _Padding) / max(0.001, 1.0 - 2.0 * _Padding);
                
                // 基于底部中心 (0.5, 0.0) 进行形变逆运算
                float2 animatedUV = paddedUV;
                animatedUV.x = 0.5 + (animatedUV.x - 0.5) / scaleX;
                animatedUV.y = 0.0 + (animatedUV.y - 0.0) / scaleY;
                
                // UV 往下移动 = 贴图视觉往上跳
                animatedUV.y -= yOffset; 

                // 如果经过变换后，UV 跑出了 0~1 的范围，说明是透明区域
                if(animatedUV.x < 0.0 || animatedUV.x > 1.0 || animatedUV.y < 0.0 || animatedUV.y > 1.0)
                {
                    return fixed4(0,0,0,0);
                }

                // ==========================================
                // 3. 将原本的阶梯波和故障震动赋予 animatedUV
                // ==========================================
                float timeSinceImpact = beat - 0.5;
                float waveMask = 0.0;
                float blockY = floor(animatedUV.y * _VibrationRes) / _VibrationRes;
                
                if (timeSinceImpact > 0.0 && timeSinceImpact < 0.5)
                {
                    float waveCenter = timeSinceImpact * _WaveSpeed; 
                    float dist = abs(blockY - waveCenter);
                    float rawWave = max(0.0, 1.0 - (dist / _WaveWidth));
                    waveMask = floor(rawWave * _FlashSteps) / max(1.0, (_FlashSteps - 1.0));
                    waveMask *= step(timeSinceImpact, 0.4);
                }

                float glitchTime = floor(_Time.y * _GlitchFPS);
                float noise = sin(glitchTime * 2.0 + blockY * 40.0) * cos(glitchTime * 1.5 - blockY * 20.0);
                noise = sign(noise) * step(0.5, abs(noise));
                float shakeOffset = noise * _ShakeIntensity * waveMask;

                float2 rUV = animatedUV + float2(shakeOffset * 1.5, 0);
                float2 gUV = animatedUV + float2(shakeOffset, 0);
                float2 bUV = animatedUV - float2(shakeOffset * 1.5, 0);

                // 防越界采样保护
                fixed r = (rUV.x>=0&&rUV.x<=1 && rUV.y>=0&&rUV.y<=1) ? tex2D(_MainTex, rUV).r : 0.0;
                fixed4 gCol = (gUV.x>=0&&gUV.x<=1 && gUV.y>=0&&gUV.y<=1) ? tex2D(_MainTex, gUV) : fixed4(0,0,0,0);
                fixed b = (bUV.x>=0&&bUV.x<=1 && bUV.y>=0&&bUV.y<=1) ? tex2D(_MainTex, bUV).b : 0.0;

                fixed4 finalColor = fixed4(r, gCol.g, b, gCol.a) * IN.color;
                finalColor.rgb += _FlashColor.rgb * waveMask * 1.5 * finalColor.a;

                return finalColor;
            }
            ENDCG
        }
    }
}