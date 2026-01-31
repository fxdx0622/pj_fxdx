Shader "Custom/SH_FlexibleEffectFilter"
{
    Properties
    {
        // マスク用スプライト
        [MainTexture] _MainTex ("Mask Sprite", 2D) = "white" {}

        // マスク全体色
        _Color ("Mask Tint", Color) = (1,1,1,1)

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
        }

        // 通常の透明ブレンド。色変換はシェーダ側で行う。
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
                float4 screenPos   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _BlendMode;
                float  _BlendOpacity;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // 背景（Camera Sorting Layer Texture）
            TEXTURE2D(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);

            static const float3 LUMA_WEIGHTS = float3(0.299, 0.587, 0.114);

        /////////////////////////////////////////////////////////////////////////////////////
            // 共通
            float Luma(float3 c)
            {
                return dot(c, LUMA_WEIGHTS);
            }

            float3 ToGrayscale(float3 c)
            {
                float g = Luma(c);
                return g.xxx;
            }

            /////////////////////////////////////////////////////////////////////////////////////
            // 各ブレンドモード
            // Screen（明るさアップ・白飛び抑制版）
            float3 BlendScreen2D(float3 baseCol, float3 maskCol)
            {
                float maskL = Luma(maskCol);   // 0..1
                float k     = 0.5 * maskL;     // 0..0.5
                return baseCol + (1.0 - baseCol) * k;
            }

            // Multiply（乗算）
            float3 BlendMultiply2D(float3 baseCol, float3 maskCol)
            {
                return baseCol * maskCol;
            }

            // Overlay（オーバーレイ）
            float3 BlendOverlay2D(float3 baseCol, float3 maskCol)
            {
                float3 m = 2.0 * baseCol * maskCol;
                float3 s = 1.0 - 2.0 * (1.0 - baseCol) * (1.0 - maskCol);
                return lerp(m, s, step(0.5, baseCol));
            }

            // Negative（ネガポジ反転：背景だけ反転）
            float3 BlendNegative2D(float3 baseCol, float3 maskCol)
            {
                // maskCol は使わない
                return 1.0 - baseCol;
            }

            // SoftLight（ソフトライト）
            float3 BlendSoftLight2D(float3 baseCol, float3 maskCol)
            {
                // Pegtop Soft Light 近似
                return (1.0 - 2.0 * maskCol) * baseCol * baseCol + 2.0 * maskCol * baseCol;
            }

            // HardLight（コントラスト強調フィルター）
            float3 BlendHardLight2D(float3 baseCol, float3 maskCol)
            {
                float maskL = Luma(maskCol);          // 0..1
                float c     = 1.0 + 1.5 * maskL;      // 1..2.5
                return saturate((baseCol - 0.5) * c + 0.5);
            }

            // VividLight（強コントラスト＋色強調フィルター）
            float3 BlendVividLight2D(float3 baseCol, float3 maskCol)
            {
                float  maskL = Luma(maskCol);         // 0..1
                float  c2    = 1.0 + 2.0 * maskL;     // 1..3
                float3 mid   = float3(0.5, 0.5, 0.5);
                float3 cont  = saturate((baseCol - mid) * c2 + mid);

                // ほどよく色を乗せる（0.6 くらいまでマスク色に寄せる）
                float3 tint = lerp(float3(1,1,1), maskCol, 0.6);
                return saturate(cont * tint);
            }

            // Grayscale（背景をグレースケール）
            float3 BlendGrayscale2D(float3 baseCol, float3 maskCol)
            {
                // maskCol は使わない
                return ToGrayscale(baseCol);
            }

            /////////////////////////////////////////////////////////////////////////////////////
            // ブレンドモード切り替え
            float3 ApplyBlendMode(float3 baseCol, float3 maskCol, int mode)
            {
                switch (mode)
                {
                    case 0: return BlendScreen2D    (baseCol, maskCol);
                    case 1: return BlendMultiply2D  (baseCol, maskCol);
                    case 2: return BlendOverlay2D   (baseCol, maskCol);
                    case 3: return BlendNegative2D  (baseCol, maskCol);
                    case 4: return BlendSoftLight2D (baseCol, maskCol);
                    case 5: return BlendHardLight2D (baseCol, maskCol);
                    case 6: return BlendVividLight2D(baseCol, maskCol);
                    case 7: return BlendGrayscale2D (baseCol, maskCol);
                    default: return baseCol;
                }
            }


            // 頂点・フラグメント
            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv          = v.uv;
                o.color       = v.color * _Color;
                o.screenPos   = ComputeScreenPos(o.positionHCS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // マスクスプライトの色・α
                float4 maskTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                float  maskA   = maskTex.a;

                // 背景色（Camera Sorting Layer Texture から取得）
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float4 baseTex  = SAMPLE_TEXTURE2D(
                    _CameraSortingLayerTexture,
                    sampler_CameraSortingLayerTexture,
                    screenUV
                );

                float3 baseCol = baseTex.rgb;
                float3 maskCol = maskTex.rgb;

                int   mode     = (int)_BlendMode;
                float strength = saturate(maskA * _BlendOpacity);

                // モードごとにフィルタ適用
                float3 blended  = ApplyBlendMode(baseCol, maskCol, mode);
                float3 finalRGB = lerp(baseCol, blended, strength);

                return float4(finalRGB, baseTex.a);
            }

            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
