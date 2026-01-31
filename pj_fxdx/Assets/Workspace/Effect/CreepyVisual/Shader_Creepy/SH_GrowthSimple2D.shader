Shader "Unlit/SH_GrowthSimple2D"
{
    Properties
    {
        // ベース
        _MainTex   ("Base (RGB) A", 2D) = "white" {}
        _Color     ("Tint", Color) = (1,1,1,1)

        // 成長制御（0→1で広がる）
        _Growth    ("Growth (0..1)", Range(0,1)) = 0.0
        _EdgeWidth ("Edge Width", Range(0.001,0.2)) = 0.05

        // ノイズ（縁のムラ）
        _NoiseTex  ("Noise (R)", 2D) = "gray" {}
        _NoiseScale("Noise Scale", Float) = 3.0
        _NoiseAmp  ("Noise Amp (0..1)", Range(0,1)) = 0.2
        _NoiseOffset ("Noise Offset (UV)", Vector) = (0,0,0,0)

        // 到達順マップ（白=先に侵食、黒=最後）
        [Toggle] _UseMap ("Use GrowthMap", Float) = 0
        _GrowthMap ("Growth Map (R)", 2D) = "white" {}

        // 放射（中心から半径）
        _Center    ("Center (UV)", Vector) = (0.5,0.5,0,0)
        _R0        ("Start Radius", Range(0,1)) = 0.0
        _R1        ("End Radius",   Range(0,2)) = 1.2

        // 表示色（“血/汚染”カラー）
        _BloodColor("Blood Color", Color) = (0.40,0.02,0.02,1)
        _RimColor  ("Rim Color",   Color) = (1.00,0.20,0.10,1)
        _RimBoost  ("Rim Boost", Range(0,3)) = 1.0
        _EdgeNoiseScroll ("Edge Noise Scroll (U,V)/sec", Vector) = (0, 0, 0, 0)
        _EdgeFlowStrength("Edge Flow Strength", Range(0,0.2)) = 0.00
        _RimWobbleAmp    ("Rim Wobble Amp (0..1)", Range(0,1)) = 0.00
        _RimWobbleFreq   ("Rim Wobble Freq (Hz)",  Float)      = 2.00
        _RadiusPulseAmp  ("Radius Pulse Amp (0..0.2)", Range(0,0.2)) = 0.00
        _RadiusPulseFreq ("Radius Pulse Freq (Hz)",    Float)        = 0.80

        // ベースの透明度の扱い
        [Toggle] _IgnoreBaseAlpha ("Ignore Base Alpha", Float) = 0

        // 仕上げブレンド
        [Enum(Normal,0, Multiply,1, Overlay,2, Add,3)] _BlendMode ("Blend Mode", Float) = 1
        _BloodMix   ("Blood Mix (0..1)", Range(0,1)) = 0.5
        _InsideAlpha("Inside Alpha in Fill (0..1)", Range(0,1)) = 1.0

        // 赤の内側だけに“黒い模様”を重ねる（デフォルトOFF）
        [Header(Detail Black Inside)]
        [Toggle] _UseDetailBlack ("Use Black Detail Inside", Float) = 0
        _BlackTex        ("Black Detail Noise (R)", 2D) = "gray" {}
        _BlackTint       ("Black Tint (multiply)", Color) = (0.05,0.04,0.04,1)
        _BlackOpacity    ("Black Opacity (0..1)", Range(0,1)) = 0.5
        _BlackThreshold  ("Black Threshold", Range(0,1)) = 0.55
        _BlackFeather    ("Black Feather",   Range(0,0.5)) = 0.08
        _BlackSpotExp    ("Black Spot Sharpness", Range(0.25,4)) = 1.5
        _BlackNoiseScale ("Black Noise Scale", Float) = 2.0
        _BlackNoiseScroll("Black Scroll (U,V)/sec", Vector) = (0.2, 0.0, 0, 0)
        _BlackOffset     ("Black Offset (U,V)", Vector) = (0,0,0,0)

        // デバッグ（黒内包だけ確認したい時）
        _BlackDebug ("Black Debug View (0/1)", Range(0,1)) = 0
    }

    SubShader
    {
        Tags {
            "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"
            "RenderPipeline"="UniversalRenderPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        #define MAX_SEEDS 8
        float  _UseMultiCenter;                // 0:従来(単一点)  1:多点
        float  _CenterCount;                   // 使用するシード数 (0..MAX_SEEDS)
        float4 _Centers[MAX_SEEDS];            // 各シードのUV (x,yを使用)

        struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
        struct Varyings   { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };

        TEXTURE2D(_MainTex);   SAMPLER(sampler_MainTex);
        TEXTURE2D(_NoiseTex);  SAMPLER(sampler_NoiseTex);
        TEXTURE2D(_GrowthMap); SAMPLER(sampler_GrowthMap);

        // 黒詳細ノイズ
        TEXTURE2D(_BlackTex);  SAMPLER(sampler_BlackTex);

        float4 _MainTex_ST, _Color;
        float  _Growth, _EdgeWidth, _NoiseScale, _NoiseAmp, _UseMap, _IgnoreBaseAlpha;
        float4 _Center, _BloodColor, _RimColor;
        float4 _NoiseOffset;
        float  _R0, _R1, _RimBoost;
        float  _BlendMode, _BloodMix, _InsideAlpha;

        // 黒詳細パラメータ
        float  _UseDetailBlack, _BlackOpacity, _BlackThreshold, _BlackFeather, _BlackSpotExp;
        float  _BlackNoiseScale;
        float4 _BlackNoiseScroll, _BlackOffset, _BlackTint;
        float  _BlackDebug;

        float4 _EdgeNoiseScroll;
        float  _EdgeFlowStrength;
        float  _RimWobbleAmp;
        float  _RimWobbleFreq;
        float  _RadiusPulseAmp;
        float  _RadiusPulseFreq;

        Varyings vert(Attributes v)
        {
            Varyings o;
            o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
            o.color = v.color * _Color;
            return o;
        }

        float n11(float v){ return v * 2.0 - 1.0; }
        float edgeBand(float x, float w){ return 1.0 - smoothstep(0.0, w, abs(x)); }

        half4 frag(Varyings i) : SV_Target
        {
            float4 baseCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
            float3 baseRGB = baseCol.rgb;
            float  baseA   = (_IgnoreBaseAlpha > 0.5) ? 1.0 : baseCol.a;

            // 縁の“ムラ”ノイズ
            float n = 0.0;
            if (_NoiseAmp > 0.0){
                float2 nuv = i.uv * _NoiseScale + _NoiseOffset.xy;
                n = n11(SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nuv).r) * _NoiseAmp;
            }

            // 侵食境界 x（UseMap or 放射）
            float x;
            if (_UseMap > 0.5) {
                float arrival = SAMPLE_TEXTURE2D(_GrowthMap, sampler_GrowthMap, i.uv).r;
                float g = saturate(_Growth + n * 0.5);
                x = g - arrival;
            } else {
                float R = lerp(_R0, _R1, _Growth);
                R *= (1.0 + _RadiusPulseAmp * sin(_RadiusPulseFreq * _Time.y));
                float minD;
                if (_CenterCount > 0.5) {
                    minD = 1e9;
                    [unroll] for (int k = 0; k < MAX_SEEDS; k++) {
                        if (k >= (int)_CenterCount) break;
                        float2 c = _Centers[k].xy;
                        minD = min(minD, distance(i.uv, c));
                    }
                } else {
                    minD = distance(i.uv, _Center.xy); // 後方互換（単一点）
                }
                x = (R + n) - minD;
            }

            // —— リム“だけ”を動かす処理（ここだけ1回）——
            float n_edge = 0.0;
            {
                float2 enuv = i.uv * _NoiseScale
                            + _NoiseOffset.xy
                            + _EdgeNoiseScroll.xy * _Time.y; // 時間で流す
                n_edge = n11(SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, enuv).r);
            }
            float wob   = (_RimWobbleAmp > 0.0) ? (_RimWobbleAmp * sin(_RimWobbleFreq * _Time.y)) : 0.0;
            float edgeW = max(1e-4, _EdgeWidth * (1.0 + wob));

            float fill = step(0.0, x);
            float rim  = edgeBand(x + n_edge * _EdgeFlowStrength, edgeW) * _RimBoost;

            // 合成（Normal/Multiply/Overlay/Add）
            float t = saturate(fill * _BloodMix);
            float3 colNormal   = lerp(baseRGB, _BloodColor.rgb, t);
            float3 colMultiply = lerp(baseRGB, baseRGB * _BloodColor.rgb, t);
            float3 lo = 2.0 * baseRGB * _BloodColor.rgb;
            float3 hi = 1.0 - 2.0 * (1.0 - baseRGB) * (1.0 - _BloodColor.rgb);
            float3 m  = step(0.5, baseRGB);
            float3 colOverlay  = lerp(lo, hi, m);
            colOverlay = lerp(baseRGB, colOverlay, t);
            float3 colAdd      = saturate(baseRGB + _BloodColor.rgb * t);

            float3 col =
                (_BlendMode < 0.5) ? colNormal :
                (_BlendMode < 1.5) ? colMultiply :
                (_BlendMode < 2.5) ? colOverlay  : colAdd;

            // 赤の“内側だけ”に黒い模様を重ねる
            if (_UseDetailBlack > 0.5)
            {
                // 黒ノイズをUVスクロール
                float2 buv = i.uv * _BlackNoiseScale + _BlackNoiseScroll.xy * _Time.y + _BlackOffset.xy;
                float  bn  = SAMPLE_TEXTURE2D(_BlackTex, sampler_BlackTex, buv).r;
                // しきい値＋フェザーで二値化
                float  bMask = smoothstep(_BlackThreshold, _BlackThreshold + max(1e-5, _BlackFeather), bn);
                // 必ず赤の“内側だけ”
                float  blackInside = fill * bMask;

                if (_BlackDebug > 0.5)
                {
                    // ピンクで内包だけ可視化
                    return float4(1.0, 0.2, 0.6, blackInside);
                }

                // 乗算系で暗部を付加（黒Tintに寄せる）
                float3 mulTint = _BlackTint.rgb;
                col = lerp(col, col * mulTint, saturate(_BlackOpacity) * blackInside);
            }

            // リムは最後に足す（黒で潰さない）
            col += rim * _RimColor.rgb;

            float alpha = lerp(baseA, baseA * _InsideAlpha, fill);
            return float4(col, alpha);
        }
        ENDHLSL

        // URP2D
        Pass
        {
            Name "Universal2D"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }

        // URP3D
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }

        // 透明物の DepthOnly 用パス
        Pass
        {
            Name "TransparentDepthOnly"
            Tags { "LightMode" = "TransparentDepthOnly" "Queue" = "Geometry" "RenderType" = "Opaque"}

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            #pragma vertex   DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // ★ ここは別名にしてメインの Attributes/Varyings と被らないようにする
            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float2 uv         : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float _ZWriteValue;

            DepthVaryings DepthOnlyVertex(DepthAttributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // HLSLINCLUDE 内の _MainTex / _MainTex_ST / TRANSFORM_TEX をそのまま利用
                output.uv        = TRANSFORM_TEX(input.uv, _MainTex);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(DepthVaryings input, out float outputDepth : SV_Depth) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a - 0.5);
                outputDepth = _ZWriteValue;
                return 0;
            }
            ENDHLSL
        }
    }
}
