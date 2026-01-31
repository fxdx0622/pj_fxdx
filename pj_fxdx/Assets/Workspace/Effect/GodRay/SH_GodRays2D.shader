Shader "Custom/SH_GodRays2D"
{
    Properties
    {
        // マスク兼スプライトテクスチャ アルファで形状を切り抜く
        [MainTexture] _MainTex ("Sprite / Mask Texture", 2D) = "white" {}

        // レイの色 HDR対応
        [HDR] _Color ("Ray Color (HDR)", Color) = (1, 0.95, 0.8, 1.0)

        // ラジアン角 コントローラからのみ設定
        [HideInInspector]
        _Angle ("Angle (rad)", Float) = 0

        // レイの基準X位置 UV0〜1
        _CenterX ("Center X (UV)", Range(0,1)) = 0.5

        // シアー量
        _Spread   ("Spread",   Range(0, 1))   = 0.5
        // 左右カット位置
        _Cutoff   ("Cutoff",   Range(-1, 1))  = -0.2
        // 縦方向フォールオフ
        _Falloff  ("Falloff",  Range(0, 1))   = 1.0
        // 左右端のフェード幅
        _EdgeFade ("Edge Fade",Range(0, 1))   = 0.5

        // アニメーション速度
        _Speed         ("Speed",          Range(0, 5))  = 3.0
        // レイ1の密度
        _Ray1Density   ("Ray1 Density",   Range(1, 40)) = 20.0
        // レイ2の密度
        _Ray2Density   ("Ray2 Density",   Range(1, 80)) = 60.0
        // レイ2の寄与
        _Ray2Intensity ("Ray2 Intensity", Range(0, 1))  = 0.8

        // 全体の明るさ
        _Intensity ("Intensity", Range(0, 5)) = 2.0

        // フォールオフ境界の揺らぎ量 0で直線1で大きく揺れる
        _FalloffJitter ("Falloff Jitter", Range(0,1)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent"
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            float4 _Color;
            float  _Angle;
            float  _CenterX;
            float  _Spread;
            float  _Cutoff;
            float  _Falloff;
            float  _EdgeFade;

            float  _Speed;
            float  _Ray1Density;
            float  _Ray2Density;
            float  _Ray2Intensity;

            float  _Intensity;
            float  _FalloffJitter;

            // 疑似乱数
            float RandomHash(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453123);
            }

            // 補間付きノイズ
            float Noise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);

                float a = RandomHash(i);
                float b = RandomHash(i + float2(1.0, 0.0));
                float c = RandomHash(i + float2(0.0, 1.0));
                float d = RandomHash(i + float2(1.0, 1.0));

                float2 u = f * f * (3.0 - 2.0 * f); // smoothstep

                return lerp(a, b, u.x) +
                       (c - a) * u.y * (1.0 - u.x) +
                       (d - b) * u.x * u.y;
            }

            // 2D回転
            float2 Rotate(float2 v, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                float2x2 m = float2x2(c, -s, s, c);
                return mul(m, v);
            }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(v.positionOS);

                float2 uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv    = uv;
                o.color = v.color;
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;

                // マスクアルファ取得
                float4 mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                // 中心位置 Xは指定 Yは下端基準
                float2 center = float2(_CenterX, 0.0);

                // 中心まわりの回転
                float2 transformedUV = Rotate(uv - center, _Angle);

                // 縦方向広がりをつくる
                float denom = (uv.y + _Spread) - (uv.y * _Spread);
                denom = max(denom, 1e-3);
                transformedUV /= denom;

                float tRays = _Time.y * _Speed;

                // レイ1ノイズ
                float2 base1 = float2(
                    transformedUV.x * _Ray1Density + 11.3,
                    tRays * 0.18
                );
                float r1a = Noise(base1);
                float r1b = Noise(base1 * float2(1.7, 2.3) + float2(3.1, 9.7));
                float r1  = r1a * 0.7 + r1b * 0.3;

                // レイ2ノイズ
                float2 base2 = float2(
                    transformedUV.x * _Ray2Density + 47.9,
                    -tRays * 0.27 + 5.4
                );
                float r2a = Noise(base2);
                float r2b = Noise(base2 * float2(2.1, 1.3) + float2(13.8, 2.6));
                float r2  = r2a * 0.6 + r2b * 0.4;

                // CutoffとEdgeFadeで左右を制限
                float cutRangeMin = _Cutoff;
                float cutRangeMax = 1.0 - _Cutoff;

                float inside = step(cutRangeMin, transformedUV.x) * step(transformedUV.x, cutRangeMax);

                float leftFade  = smoothstep(cutRangeMin, cutRangeMin + _EdgeFade, transformedUV.x);
                float rightFade = smoothstep(cutRangeMin, cutRangeMin + _EdgeFade, 1.0 - transformedUV.x);

                float horizFade = inside * leftFade * rightFade;

                r1 *= horizFade;
                r2 *= horizFade;

                float rays = saturate(r1 + r2 * _Ray2Intensity);

                // 縦方向フォールオフと揺らぎ
                // yfは下からの距離 0が上1が下
                float yf = 1.0 - uv.y;

                float fallNoise = Noise(float2(
                    transformedUV.x * 7.3 + 91.7,
                    uv.y * 3.1 + tRays * 0.27
                ));

                float warpedY = yf + (fallNoise - 0.5) * _FalloffJitter;
                warpedY = saturate(warpedY);

                float fall = smoothstep(0.0, _Falloff, warpedY);
                rays *= fall;

                // マスクアルファで形状を切り抜く
                rays *= mask.a;

                // 明るさ適用
                rays *= _Intensity;

                float4 col;
                col.rgb = _Color.rgb * rays;
                col.a   = _Color.a   * rays;

                // 頂点カラーも反映
                col *= i.color;
                return col;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
