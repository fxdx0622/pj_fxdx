Shader "Custom/SmokeNoise"
{
    Properties
    {
        // 色と透明度
        _Color("Color", Color) = (0.38,0.34,0.29,1)

        // ノイズ関連
        _NoiseScale("Noise Scale", Float) = 5.0
        _NoiseSpeed("Noise Speed (0 = static)", Float) = 3.0

        // アルファと不透明度
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.3
        _Opacity("Opacity", Range(0,1)) = 0.4

        // 円形マスク
        _Radius("Circle Radius", Range(0,1)) = 0.4
        _Feather("Edge Feather", Range(0,1)) = 0.15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            //--------------------------------------
            // 構造体定義
            //--------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            //--------------------------------------
            // プロパティ（C#から制御可能）
            //--------------------------------------
            float4 _Color;
            float  _NoiseScale;
            float  _NoiseSpeed;
            float  _Cutoff;
            float  _Opacity;
            float  _Radius;
            float  _Feather;

            //--------------------------------------
            // ヘルパー関数
            //--------------------------------------

            // 疑似乱数
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            // 補間ノイズ（Value Noise）
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            //--------------------------------------
            // 頂点シェーダー
            //--------------------------------------
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            //--------------------------------------
            // フラグメントシェーダー
            //--------------------------------------
            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // --- ノイズ計算 ---
                float2 timeOffset = float2(_Time.y * _NoiseSpeed, -_Time.y * _NoiseSpeed * 0.43);
                float n = noise(uv * _NoiseScale + timeOffset);

                // --- ノイズからアルファを生成 ---
                float baseAlpha = saturate((n - _Cutoff) / max(0.0001, (1.0 - _Cutoff)));
                baseAlpha *= _Opacity;

                // --- 円形マスク ---
                float dist   = distance(uv, float2(0.5, 0.5));
                float inner  = saturate(_Radius - _Feather);
                float outer  = saturate(_Radius + _Feather);

                float circle = (outer <= inner + 1e-5)
                               ? ((dist <= inner) ? 1.0 : 0.0)
                               : (1.0 - smoothstep(inner, outer, dist));

                // --- 最終アルファ ---
                float finalAlpha = baseAlpha * circle * _Color.a;

                return float4(_Color.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}
