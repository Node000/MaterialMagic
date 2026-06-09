Shader "UI/MaterialModifiers/CardRhythmPixelImpact"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)

        [Header(Physics and Spring Animation)]
        _BPM ("音乐节拍 (BPM)", Float) = 120.0
        _JumpHeight ("跃起高度 (UI像素)", Float) = 30.0
        _ImpactDepth ("撞击下砸深度 (UI像素)", Float) = 20.0
        _SquashAmount ("撞击挤压程度 (压扁)", Range(0.0, 1.0)) = 0.5
        _StretchAmount ("下坠拉伸程度 (变长)", Range(0.0, 1.0)) = 0.3

        [Header(Retro Pixel Shockwave)]
        _WaveSpeed ("波段上升速度", Float) = 2.0
        _WaveWidth ("波段宽度 (UV)", Range(0.05, 0.5)) = 0.25
        _FlashSteps ("闪光阶梯段数 (复古感)", Float) = 3.0
        
        [Header(Glitch Shake)]
        _ShakeIntensity ("横向撕裂震动强度", Range(0.0, 0.2)) = 0.05
        _VibrationRes ("像素网格分辨率", Float) = 32.0
        _GlitchFPS ("故障帧率 (卡顿感)", Float) = 15.0
        [HDR]_FlashColor ("撞击电音闪光", Color) = (0.2, 1.0, 0.8, 1)

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

            // 物理参数
            float _BPM;
            float _JumpHeight;
            float _ImpactDepth;
            float _SquashAmount;
            float _StretchAmount;

            // 像素波与震动参数
            float _WaveSpeed;
            float _WaveWidth;
            float _FlashSteps;
            float _ShakeIntensity;
            float _VibrationRes;
            float _GlitchFPS;
            fixed4 _FlashColor;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float beat = frac(_Time.y * (_BPM / 60.0));
                
                float yOffset = 0.0;
                float scaleX = 1.0;
                float scaleY = 1.0;

                // ==========================================
                // 三段式物理动画：起跳(带惯性) -> 下坠(拉伸) -> 撞击(挤压反弹)
                // ==========================================
                if (beat < 0.4) 
                {
                    // 1. 蓄力起跳阶段 (0.0 ~ 0.4)
                    float t = beat / 0.4;
                    // 使用阻尼弹簧模拟起跳越过最高点后的回落惯性
                    float jumpSpring = exp(-6.0 * t) * cos(15.0 * t);
                    yOffset = _JumpHeight * (1.0 - jumpSpring);
                    
                    // 起跳瞬间也会有一点反向形变
                    scaleY = 1.0 + _StretchAmount * jumpSpring * 0.5;
                    scaleX = 1.0 - _StretchAmount * jumpSpring * 0.25;
                }
                else if (beat < 0.5)
                {
                    // 2. 极速下坠阶段 (0.4 ~ 0.5)
                    float t = (beat - 0.4) / 0.1;
                    // 指数加速下坠
                    yOffset = lerp(_JumpHeight, -_ImpactDepth, pow(t, 2.0));
                    
                    // 极速下落产生严重的视觉拉伸 (Stretch)
                    scaleY = lerp(1.0, 1.0 + _StretchAmount, t);
                    scaleX = lerp(1.0, 1.0 - _StretchAmount * 0.5, t);
                }
                else 
                {
                    // 3. 落地撞击与回弹阶段 (0.5 ~ 1.0)
                    float t = (beat - 0.5) / 0.5;
                    // 高频阻尼弹簧模拟砸地后的余震回弹
                    float rebound = exp(-8.0 * t) * cos(25.0 * t);
                    yOffset = -_ImpactDepth * rebound;
                    
                    // 撞击瞬间 (rebound最高) 产生强烈的挤压压扁 (Squash)
                    scaleY = 1.0 - _SquashAmount * rebound;
                    scaleX = 1.0 + _SquashAmount * rebound * 0.5;
                }

                float3 pos = v.vertex.xyz;
                
                // 应用形变与位移 (在UI局部坐标系中)
                pos.x *= scaleX;
                pos.y *= scaleY;
                pos.y += yOffset;

                OUT.worldPosition = float4(pos, 1.0);
                OUT.vertex = UnityObjectToClipPos(float4(pos, 1.0));
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float beat = frac(_Time.y * (_BPM / 60.0));
                
                // 将撞击点对齐到 beat = 0.5
                float timeSinceImpact = beat - 0.5;
                float waveMask = 0.0;
                
                // ==========================================
                // 1. 阶梯像素化网格 (Pixelated Grid)
                // ==========================================
                // 强制将纵坐标砍成低分辨率的马赛克块，所有的计算都基于这个粗糙的坐标
                float blockY = floor(uv.y * _VibrationRes) / _VibrationRes;
                
                // ==========================================
                // 2. 阶梯状断层扫描波 (Stepped Wave)
                // ==========================================
                if (timeSinceImpact > 0.0 && timeSinceImpact < 0.5)
                {
                    // 波中心向上移动
                    float waveCenter = timeSinceImpact * _WaveSpeed; 
                    
                    // 计算当前像素块距离波中心的距离
                    float dist = abs(blockY - waveCenter);
                    
                    // 计算基础线性波形，限制在波宽范围内
                    float rawWave = max(0.0, 1.0 - (dist / _WaveWidth));
                    
                    // **关键点**：硬切断阶梯渐变！取代平滑渐变，营造16bit复古色阶感
                    // 例如分为3阶，则只有 1.0, 0.66, 0.33, 0.0 四种发光强度
                    waveMask = floor(rawWave * _FlashSteps) / max(1.0, (_FlashSteps - 1.0));
                    
                    // 随着时间硬切断消失（不用渐变消失）
                    waveMask *= step(timeSinceImpact, 0.4);
                }

                // ==========================================
                // 3. 低帧率故障撕裂 (Low FPS Glitch)
                // ==========================================
                // 强制降低时间的刷新率，产生卡顿的故障感
                float glitchTime = floor(_Time.y * _GlitchFPS);
                
                // 基于降帧时间和像素块生成伪随机噪声
                float noise = sin(glitchTime * 2.0 + blockY * 40.0) * cos(glitchTime * 1.5 - blockY * 20.0);
                
                // 将噪声二值化：强制转为 -1, 0 或 1 的极端值，彻底拒绝平滑移动
                noise = sign(noise) * step(0.5, abs(noise));
                
                // 震动只发生在波段覆盖的地方
                float shakeOffset = noise * _ShakeIntensity * waveMask;

                // ==========================================
                // 4. 三通道错位采样 (Chromatic Aberration)
                // ==========================================
                float2 rUV = uv + float2(shakeOffset * 1.5, 0);
                float2 gUV = uv + float2(shakeOffset, 0);
                float2 bUV = uv - float2(shakeOffset * 1.5, 0);

                fixed r = (tex2D(_MainTex, rUV) + _TextureSampleAdd).r;
                fixed4 gCol = tex2D(_MainTex, gUV) + _TextureSampleAdd; 
                fixed b = (tex2D(_MainTex, bUV) + _TextureSampleAdd).b;

                fixed4 finalColor = fixed4(r, gCol.g, b, gCol.a) * IN.color;

                // 叠加阶梯状闪光
                finalColor.rgb += _FlashColor.rgb * waveMask * 1.5 * finalColor.a;

                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(finalColor.a - 0.001);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}