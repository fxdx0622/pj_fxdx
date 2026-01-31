Shader "Custom/SH_FlexibleEffectSprite"
{
    Properties
    {
        // オブジェクト自身のスプライト
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}

        // マスク色（ブレンドに使う色。白=ニュートラル寄り）
        _Color ("Mask Color", Color) = (1,1,1,1)

        // 0:Screen, 1:Multiply, 2:Overlay, 3:Negative,
        // 4:SoftLight, 5:HardLight, 6:VividLight, 7:Grayscale
        _BlendMode ("Blend Mode", Range(0,7)) = 2

        // エフェクトの強さ（0 = 無効, 1 = 最大）
        _BlendOpacity ("Blend Opacity", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "SpriteUnlit"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _BlendMode;     // 0..7
                float  _BlendOpacity;  // 0..1
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER  (sampler_MainTex);

            static const float3 LUMA_WEIGHTS = float3(0.299, 0.587, 0.114);

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                // SRP Batcher 対応のため _MainTex_ST は使わず生 UV
                o.uv    = v.uv;
                o.color = v.color;
                return o;
            }

            float3 ToGrayscale(float3 c)
            {
                float g = dot(c, LUMA_WEIGHTS);
                return g.xxx;
            }

            // Multiply（乗算）
            float3 BlendMultiply(float3 B, float3 S)
            {
                return B * S;
            }

            // Overlay（オーバーレイ）
            float3 BlendOverlay(float3 B, float3 S)
            {
                float3 m = 2.0 * B * S;
                float3 s = 1.0 - 2.0 * (1.0 - B) * (1.0 - S);
                return lerp(m, s, step(0.5, B));
            }

            // SoftLight（ソフトライト）
            float3 BlendSoftLight(float3 B, float3 S)
            {
                // Pegtop Soft Light 近似
                return (1.0 - 2.0 * S) * B * B + 2.0 * S * B;
            }

            // 各モード専用のフィルタ計算
            // baseCol: 元スプライトの色, maskCol: _Color.rgb
            float3 ApplyBlendMode(float3 baseCol, float3 maskCol, int mode)
            {
                float3 result = baseCol;
                float  maskL  = dot(maskCol, LUMA_WEIGHTS); // 0..1

                switch (mode)
                {
                    case 0: // Screen（明るさアップ・白飛び抑制版）
                    {
                        // 暗部を中心に 0.5*maskL 分だけ 1 に近づける
                        float k = 0.5 * maskL;               // 0..0.5
                        result = baseCol + (1.0 - baseCol) * k;
                        break;
                    }

                    case 1: // Multiply（乗算）
                        result = BlendMultiply(baseCol, maskCol);
                        break;

                    case 2: // Overlay（オーバーレイ）
                        result = BlendOverlay(baseCol, maskCol);
                        break;

                    case 3: // Negative（ネガポジ反転：元色をそのまま反転）
                        result = 1.0 - baseCol;
                        break;

                    case 4: // SoftLight（柔らかコントラスト）
                        result = BlendSoftLight(baseCol, maskCol);
                        break;

                    case 5: // HardLight（コントラスト強調フィルター）
                    {
                        // 中間値 0.5 を中心にコントラストを上げる
                        float c = 1.0 + 1.5 * maskL;         // 1..2.5
                        result  = saturate((baseCol - 0.5) * c + 0.5);
                        break;
                    }

                    case 6: // VividLight（強コントラスト＋色強調）
                    {
                        float  c2   = 1.0 + 2.0 * maskL;     // 1..3
                        float3 mid  = float3(0.5, 0.5, 0.5);
                        float3 cont = saturate((baseCol - mid) * c2 + mid);

                        // ほどよく色を乗せる（0.6 くらいマスク色に寄せる）
                        float3 tint = lerp(float3(1,1,1), maskCol, 0.6);
                        result = saturate(cont * tint);
                        break;
                    }

                    case 7: // Grayscale（元色をグレースケール）
                        result = ToGrayscale(baseCol);
                        break;

                    default:
                        result = baseCol;
                        break;
                }

                return result;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // 元スプライト色
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float4 src = tex * i.color;

                float3 baseCol = src.rgb;
                float  baseA   = src.a;

                float3 maskCol = _Color.rgb;

                int   mode     = (int)_BlendMode;
                float strength = saturate(_BlendOpacity); // 0..1（必要なら頂点αと掛けてもよい）

                // モードごとにフィルタ適用
                float3 blended  = ApplyBlendMode(baseCol, maskCol, mode);
                float3 finalRGB = lerp(baseCol, blended, strength);

                // アルファは元スプライトのまま
                return float4(finalRGB, baseA);
            }

            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
