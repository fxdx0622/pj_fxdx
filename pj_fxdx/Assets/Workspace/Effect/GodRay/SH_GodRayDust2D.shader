Shader "Custom/SH_GodRayDust2D"
{
    Properties
    {
        // パーティクル用テクスチャ
        [MainTexture] _MainTex ("Particle Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)

        // マスク
        [HideInInspector] _MaskCenterWS ("Mask Center WS", Vector) = (0,0,0,0)
        [HideInInspector] _MaskRightWS  ("Mask Right WS (dir * halfWidth)", Vector) = (1,0,0,0)
        [HideInInspector] _MaskUpWS     ("Mask Up WS (dir * halfHeight)",  Vector) = (0,1,0,0)
        [HideInInspector] _MaskHalfSize ("Mask Half Size (x=width/2,y=height/2)", Vector) = (0.5,0.5,0,0)
        [HideInInspector] _MaskFlipY    ("Mask Flip Y (0/1)", Float) = 0

        [HideInInspector] _MaskThreshold ("Mask Alpha Threshold", Range(0,1)) = 0.02

        // GodRay
        [HideInInspector] _Angle    ("Angle (rad)", Float) = 0
        [HideInInspector] _CenterX  ("Center X (UV)", Float) = 0.5
        [HideInInspector] _Spread   ("Spread",   Float) = 0.02
        [HideInInspector] _Cutoff   ("Cutoff",   Float) = -0.2
        [HideInInspector] _FalloffJitter ("Falloff Jitter", Float) = 0.1

        [HideInInspector] _Speed         ("Speed",          Float) = 3.0
        [HideInInspector] _Ray1Density   ("Ray1 Density",   Float) = 20.0
        [HideInInspector] _Ray2Density   ("Ray2 Density",   Float) = 60.0
        [HideInInspector] _Ray2Intensity ("Ray2 Intensity", Float) = 0.8

        // Dust側で個別調整したい項目
        _Falloff   ("Dust Falloff",   Range(0,1)) = 1.0
        _EdgeFade  ("Dust Edge Fade", Range(0,1)) = 0.5
        _Intensity ("Dust Intensity", Range(0,3)) = 2.0
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
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float3 worldPos   : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            float4 _Color;

            // マスク平面
            float4 _MaskCenterWS;
            float4 _MaskRightWS;
            float4 _MaskUpWS;
            float4 _MaskHalfSize;
            float  _MaskFlipY;
            float  _MaskThreshold;

            // GodRay本体と同期される値
            float  _Angle;
            float  _CenterX;
            float  _Spread;
            float  _Cutoff;
            float  _FalloffJitter;

            float  _Speed;
            float  _Ray1Density;
            float  _Ray2Density;
            float  _Ray2Intensity;

            // Dust側で公開する3つ
            float  _Falloff;
            float  _EdgeFade;
            float  _Intensity;

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

                float2 u = f * f * (3.0 - 2.0 * f);

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

                float3 worldPos = TransformObjectToWorld(v.positionOS);
                o.worldPos = worldPos;

                o.positionCS = TransformWorldToHClip(worldPos);

                o.uv    = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                // 1 worldPosからGodRayのUVへ変換
                float2 posWS    = i.worldPos.xy;
                float2 centerWS = _MaskCenterWS.xy;
                float2 d        = posWS - centerWS;

                float2 right    = _MaskRightWS.xy;
                float2 up       = _MaskUpWS.xy;
                float2 halfSize = _MaskHalfSize.xy;

                float2 rightN = normalize(right);
                float2 upN    = normalize(up);

                float u = 0.5 + dot(d, rightN) / (halfSize.x * 2.0);
                float v = 0.5 + dot(d, upN)    / (halfSize.y * 2.0);
                float2 uv = float2(u, v);

                // SpriteRenderer.flipYに合わせてYを反転
                if (_MaskFlipY > 0.5)
                {
                    uv.y = 1.0 - uv.y;
                }

                // UV範囲外は破棄
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                {
                    clip(-1);
                }

                // 中心位置 Xは指定 Yは下端基準
                float2 center = float2(_CenterX, 0.0);

                // 中心まわりの回転
                float2 transformedUV = Rotate(uv - center, _Angle);

                // 縦方向広がりをつくる
                float denom = (uv.y + _Spread) - (uv.y * _Spread);
                denom = max(denom, 1e-3);
                transformedUV /= denom;

                float t = _Time.y * _Speed;

                // レイ1ノイズ
                float2 base1 = float2(
                    transformedUV.x * _Ray1Density + 11.3,
                    t * 0.18
                );
                float r1a = Noise(base1);
                float r1b = Noise(base1 * float2(1.7, 2.3) + float2(3.1, 9.7));
                float r1  = r1a * 0.7 + r1b * 0.3;

                // レイ2ノイズ
                float2 base2 = float2(
                    transformedUV.x * _Ray2Density + 47.9,
                    -t * 0.27 + 5.4
                );
                float r2a = Noise(base2);
                float r2b = Noise(base2 * float2(2.1, 1.3) + float2(13.8, 2.6));
                float r2  = r2a * 0.6 + r2b * 0.4;

                // 左右カットとフェード
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
                    uv.y * 3.1 + t * 0.27
                ));

                float warpedY = yf + (fallNoise - 0.5) * _FalloffJitter;
                warpedY = saturate(warpedY);

                float fall = smoothstep(0.0, _Falloff, warpedY);
                rays *= fall;

                // Dust用の明るさ
                rays *= _Intensity;

                // raysが小さい部分は破棄
                float alphaMask = saturate(rays);
                clip(alphaMask - _MaskThreshold);

                // パーティクル本体
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;

                col.rgb *= alphaMask;
                col.a   *= alphaMask;

                return col;
            }

            ENDHLSL
        }
    }
}
