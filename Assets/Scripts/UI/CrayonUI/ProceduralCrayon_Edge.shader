Shader "UI/ProceduralCrayon_Edge"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        _PaperColor ("Paper Color", Color) = (0.97, 0.945, 0.88, 1)
        _CrayonA ("Crayon A", Color) = (0.58, 0.73, 0.59, 1)
        _CrayonB ("Crayon B", Color) = (0.35, 0.56, 0.47, 1)

        _Scale ("Stroke Scale", Range(1, 20)) = 5
        _Angle ("Base Stroke Angle", Range(-180, 180)) = -18
        _CrossAngle ("Layer Angle Offset", Range(0, 90)) = 47
        _Coverage ("Pigment Coverage", Range(0, 2)) = 1.25
        _Dryness ("Dryness / Paper Gaps", Range(0, 1)) = 0.8
        _GrainDensity ("Paper Grain Density", Range(100, 1000)) = 500
        _PaperStrength ("Paper Grain Contrast", Range(0, 0.25)) = 0.09

        _RedrawFPS ("Redraw FPS (0 = Freeze)", Range(0, 24)) = 10
        _RedrawStrength ("Redraw Amount", Range(0, 1)) = 0.8
        _DirectionVariation ("Local Direction Variation (Degrees)", Range(0, 90)) = 40
        _DirectionScale ("Local Direction Patch Density", Range(0.25, 4)) = 1.4
        _RedrawOffset ("Local Mark Resampling", Range(0, 0.5)) = 0.12
        _PressureVariation ("Local Pressure Variation", Range(0, 0.2)) = 0.06
        _Jitter ("Local Registration Jitter", Range(0, 0.005)) = 0.0007

        _EdgeLength ("Edge Tips Length (UI Units)", Range(0, 16)) = 7
        _EdgeWidth ("Edge Tips Width (UI Units)", Range(0.4, 4)) = 1.1
        _EdgeDensity ("Edge Tips Density", Range(0, 1)) = 0.65

        _Seed ("Pattern Seed", Float) = 7
        _Aspect ("UI Aspect Ratio (Width/Height)", Float) = 1

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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 uv3 : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
                float4 imageData : TEXCOORD2;
                float4 spriteUV : TEXCOORD3;
                float edgePadding : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _TextureSampleAdd;
            float4 _PaperColor, _CrayonA, _CrayonB, _ClipRect;
            float _Scale, _Angle, _CrossAngle, _Coverage, _Dryness;
            float _GrainDensity, _PaperStrength, _Jitter, _Seed, _Aspect;
            float _RedrawFPS, _RedrawStrength, _DirectionVariation;
            float _DirectionScale, _RedrawOffset, _PressureVariation;
            float _EdgeLength, _EdgeWidth, _EdgeDensity;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.localPosition = v.vertex.xy;
                o.uv = v.uv;
                o.color = v.color;
                o.imageData = v.uv1;
                o.spriteUV = v.uv2;
                o.edgePadding = v.uv3.x;
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(0.1031, 0.11369));
                p += dot(p, p.yx + 19.19);
                return frac((p.x + p.y) * p.x);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float2 w = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(Hash(cell), Hash(cell + float2(1, 0)), w.x),
                    lerp(Hash(cell + float2(0, 1)),
                         Hash(cell + float2(1, 1)), w.x), w.y);
            }

            float FilteredNoiseGrad(float2 p, float2 dx, float2 dy)
            {
                float footprint = max(length(dx), length(dy));
                return lerp(Noise(p), 0.5, smoothstep(0.6, 1.4, footprint));
            }

            float FilteredNoise(float2 p)
            {
                return FilteredNoiseGrad(p, ddx(p), ddy(p));
            }

            float2 RotateCS(float2 p, float2 cs)
            {
                return float2(cs.x * p.x - cs.y * p.y,
                              cs.y * p.x + cs.x * p.y);
            }

            float2 Rotate(float2 p, float degrees)
            {
                float angle = degrees * 0.01745329252;
                return RotateCS(p, float2(cos(angle), sin(angle)));
            }

            // 一个固定位置的局部涂抹区域。只重画细排线，不移动色块或纸张。
            float LocalMarks(float2 p, float2 dx, float2 dy, float2 anchor,
                             float density, float seed, float tick)
            {
                float2 key = anchor + float2(seed, seed * 0.73);
                float2 frameKey = key + tick * float2(0.75487766, 0.56984029);
                float4 randoms = float4(
                    Hash(frameKey + 11.7), Hash(frameKey + 37.1),
                    Hash(frameKey + 71.9), Hash(frameKey + 109.3)) * 2.0 - 1.0;
                float amount = saturate(_RedrawStrength);

                // 每个区域有自己的方向。时间只决定当前画稿，不用于插值旋转。
                float restingAngle = (Hash(key + 19.3) * 2.0 - 1.0)
                                   * _DirectionVariation * 0.2;
                float angle = (restingAngle + randoms.x * _DirectionVariation * amount)
                            * 0.01745329252;
                float2 cs = float2(cos(angle), sin(angle));
                float2 local = (p - anchor) / density;
                float2 q = RotateCS(local, cs);

                // 为每张画稿重新落笔，而非让同一张纹理随时间连续滑动。
                q += randoms.yz * (_RedrawOffset + _Jitter * _Scale) * amount;
                float2 frequency = float2(1.1, 38.0);
                float2 sampleP = q * frequency + float2(seed, seed + 23.7);

                // 显式传入连续坐标的导数，避免网格 anchor 切换时出现滤波接缝。
                float2 sampleDX = RotateCS(dx / density, cs) * frequency;
                float2 sampleDY = RotateCS(dy / density, cs) * frequency;
                float lines = FilteredNoiseGrad(sampleP, sampleDX, sampleDY);
                return lines + randoms.w * _PressureVariation * amount;
            }

            float Pigment(float2 p, float seed, float coverage, float tick)
            {
                // 大块色斑及其轻微弯曲始终固定，作为每张画稿共享的底稿。
                float patches = FilteredNoise(p * float2(2.2, 4.5) + seed + 31.0);
                float warp = FilteredNoise(p * 1.1 + seed) - 0.5;
                float2 markP = p + float2(0.0, warp * 0.12);

                float density = max(_DirectionScale, 0.25);
                // 两层网格错开，防止混合边界重合。
                float2 gridOffset = float2(Hash(float2(seed, 3.1)),
                                           Hash(float2(seed, 9.7)));
                float2 grid = markP * density + gridOffset;
                float2 cell = floor(grid);
                float2 f = frac(grid);
                float2 w = f * f * (3.0 - 2.0 * f);
                float2 dx = ddx(grid);
                float2 dy = ddy(grid);

                // 相邻四个局部方向平滑衔接；权重不随时间变化。
                float n00 = LocalMarks(grid, dx, dy, cell, density, seed, tick);
                float n10 = LocalMarks(grid, dx, dy, cell + float2(1, 0), density, seed, tick);
                float n01 = LocalMarks(grid, dx, dy, cell + float2(0, 1), density, seed, tick);
                float n11 = LocalMarks(grid, dx, dy, cell + float2(1, 1), density, seed, tick);
                float lines = lerp(lerp(n00, n10, w.x), lerp(n01, n11, w.x), w.y);

                float pressure = smoothstep(0.22, 0.79, lines * 0.65 + patches * 0.35);
                return saturate(coverage * (0.15 + pressure * 0.95));
            }

            // uv 是图片自身的 0..1 坐标，允许落在外扩区域。
            // 显式清空图片范围外的 alpha，并限制采样，防止读到图集邻图。
            float4 SampleImage(float2 uv, float4 bounds, float2 dx, float2 dy)
            {
                float2 lo = min(bounds.xy, bounds.zw);
                float2 hi = max(bounds.xy, bounds.zw);
                float2 inset = min(abs(_MainTex_TexelSize.xy) * 0.5, (hi - lo) * 0.49);
                float2 sampleUV = clamp(lerp(bounds.xy, bounds.zw, saturate(uv)), lo + inset, hi - inset);
                float4 sampleColor = tex2Dgrad(_MainTex, sampleUV, dx, dy) + _TextureSampleAdd;
                sampleColor.a *= step(0.0, uv.x) * step(uv.x, 1.0)
                               * step(0.0, uv.y) * step(uv.y, 1.0);
                return sampleColor;
            }

            // 两组排线各自形成细长的 alpha 外扩，避免变成一圈均匀描边。
            // 返回 xy=方向，z=这一条线的随机长度，w=实心线条遮罩（0 或 1）。
            float4 EdgeTip(float2 position, float2 paperUV, float baseAngle,
                           float seed, float tick, float maxLength)
            {
                float amount = saturate(_RedrawStrength);
                float2 frameOffset = tick * float2(0.75487766, 0.56984029);
                float localDirection = Noise(paperUV * _Scale * _DirectionScale + seed + frameOffset);
                float angle = (baseAngle + (localDirection * 2.0 - 1.0)
                            * _DirectionVariation * amount) * 0.01745329252;
                float2 direction = float2(cos(angle), -sin(angle));
                float2 perpendicular = float2(-direction.y, direction.x);
                float stripe = dot(position, perpendicular) / max(_EdgeWidth * 2.3, 0.25);
                float lane = floor(stripe);
                float2 stableKey = float2(lane, seed);
                float stableLength = Hash(stableKey + 17.9);
                float lengthRandom = lerp(stableLength, Hash(stableKey + frameOffset + 17.9), amount);
                float occupancy = lerp(Hash(stableKey + 71.1), Hash(stableKey + frameOffset + 71.1), amount);

                float distanceToLine = abs(frac(stripe) - 0.5);
                // 只改变线头是否出现，不用半透明表达疏密或细线覆盖率。
                float stripeMask = step(distanceToLine, 0.29);
                float presence = step(1.0 - saturate(_EdgeDensity), occupancy);
                float length = maxLength * lerp(0.2, 1.0, lengthRandom);
                return float4(direction, length, stripeMask * presence);
            }

            void GatherEdge(float2 uv, float2 size, float4 bounds,
                            float2 dx, float2 dy, float4 tip, inout float4 best)
            {
                // 四个距离、两个方向：从线头反向寻找原图中有颜料的位置。
                // 同时带回原图 RGB，避免透明区域的黑色污染伸出的线头。
                [unroll]
                for (int k = 1; k <= 4; k++)
                {
                    float t = k * 0.25;
                    float2 offset = tip.xy * (tip.z * t) / size;
                    float4 positive = SampleImage(uv + offset, bounds, dx, dy);
                    float4 negative = SampleImage(uv - offset, bounds, dx, dy);
                    // 原图 alpha 只判断有无笔触；线头从根部到末端均为实心。
                    positive.a = step(0.5, positive.a) * tip.w;
                    negative.a = step(0.5, negative.a) * tip.w;
                    // 优先使用最近的有效颜色；所有遮罩始终保持 0 或 1。
                    best = lerp(best, positive, positive.a * (1.0 - best.a));
                    best = lerp(best, negative, negative.a * (1.0 - best.a));
                }
            }

            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // CrayonUIEdge 提供图片自身坐标；图集 UV 不再影响纹理比例。
                bool hasEdgeMesh = i.edgePadding > 0.0 && min(i.imageData.z, i.imageData.w) > 0.0;
                float2 imageUV = hasEdgeMesh ? i.imageData.xy : i.uv;
                float2 imageSize = max(i.imageData.zw, float2(1.0, 1.0));
                float aspect = hasEdgeMesh ? imageSize.x / imageSize.y : max(_Aspect, 0.001);
                float2 paperUV = (imageUV - 0.5) * float2(aspect, 1.0);

                // 纸张纹理完全静止。不要把 tick、随机位移或旋转加到这里。
                float grain = FilteredNoise(paperUV * _GrainDensity + _Seed);
                float fine = FilteredNoise(paperUV * _GrainDensity * 1.91 + 53.0);
                float tooth = smoothstep(0.22, 0.78, grain * 0.7 + fine * 0.3);
                float adhesion = lerp(1.0, tooth, _Dryness);

                // 唯一的时间入口：每帧保持同一张画稿，到下一帧再重画。
                float tick = fmod(floor(_Time.y * max(_RedrawFPS, 0.0)), 4096.0);
                float2 p = paperUV * _Scale;

                // 整层基准方向固定；变化仅发生在 Pigment 内部的局部细排线。
                float a = Pigment(Rotate(p, _Angle), _Seed, _Coverage, tick);
                float b = Pigment(Rotate(p, _Angle + _CrossAngle),
                                  _Seed + 83.0, _Coverage * 1.1, tick);

                float3 paper = _PaperColor.rgb * (1.0 + (grain - 0.5) * _PaperStrength);
                float3 color = lerp(paper, _CrayonA.rgb, saturate(a * adhesion * _CrayonA.a));
                color = lerp(color, _CrayonB.rgb, saturate(b * adhesion * 0.48 * _CrayonB.a));

                float2 textureDX = ddx(i.uv);
                float2 textureDY = ddy(i.uv);
                float4 tex = hasEdgeMesh
                    ? SampleImage(imageUV, i.spriteUV, textureDX, textureDY)
                    : tex2D(_MainTex, i.uv) + _TextureSampleAdd;

                // 留出 1 UI 单位的透明边距，防止线头在扩展网格边缘被截平。
                float maxLength = min(max(_EdgeLength, 0.0), max(i.edgePadding - 1.0, 0.0));
                float2 position = (imageUV - 0.5) * imageSize;
                // 两组实心线头共享画稿帧号，但分别决定长度与出现位置。
                float4 tipA = EdgeTip(position, paperUV, _Angle, _Seed, tick, maxLength);
                float4 tipB = EdgeTip(position, paperUV, _Angle + _CrossAngle, _Seed + 83.0, tick, maxLength);
                float4 extension = float4(0, 0, 0, 0);
                [branch]
                if (hasEdgeMesh && maxLength > 0.0 && _EdgeDensity > 0.0
                    && _Coverage > 0.0 && tex.a < 0.999)
                {
                    GatherEdge(imageUV, imageSize, i.spriteUV, textureDX, textureDY, tipA, extension);
                    GatherEdge(imageUV, imageSize, i.spriteUV, textureDX, textureDY, tipB, extension);
                }
                // 不再用长度、纸纹或颜料覆盖量降低线头 alpha。
                float extensionAlpha = extension.a;
                float addedAlpha = extensionAlpha * (1.0 - saturate(tex.a));
                float combinedAlpha = saturate(tex.a) + addedAlpha;
                float3 imageColor = (tex.rgb * saturate(tex.a) + extension.rgb * addedAlpha)
                                  / max(combinedAlpha, 0.00001);
                // 线头自身不透明；整体 UI 淡入淡出与遮罩仍按原有设置生效。
                float4 result = float4(color * imageColor, combinedAlpha * _PaperColor.a) * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(i.localPosition, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}

