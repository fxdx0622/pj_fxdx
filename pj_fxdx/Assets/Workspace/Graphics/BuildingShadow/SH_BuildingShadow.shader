Shader "Custom/SH_BuildingShadow"
{
    Properties
    {
        [MainTexture] _MainTex("Sprite", 2D) = "white" {}
        _ShadowColor("Shadow Color", Color) = (0, 0, 0, 0)
        _ShadowStrength("Shadow Strength", Range(0, 1)) = 0.6

        _ShearX("Shear X", Float) = 0.3
        _HeightScale("Height Scale", Range(0, 1)) = 1.0

        _ExpandFar("Expand Far",  Range(0, 1)) = 0

        _AlphaThreshold("Alpha Threshold", Range(0, 1)) = 0.1
        _MainTex_ST("", Vector) = (1,1,0,0)
    }

        SubShader
        {
            Tags
            {
                "Queue" = "Transparent"
                "RenderType" = "Transparent"
                "RenderPipeline" = "UniversalPipeline"
                "UniversalMaterialType" = "SpriteUnlit"
            }

            Cull Off
            ZWrite Off
            ZTest Always

            Pass
            {
                Blend DstColor OneMinusSrcAlpha

                Stencil
                {
                    Ref 1
                    Comp NotEqual
                    Pass Replace
                }

                HLSLPROGRAM
                #pragma vertex vert
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
                };

                TEXTURE2D(_MainTex);
                SAMPLER(sampler_MainTex);
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;

                float4 _ShadowColor;
                float _ShadowStrength;
                float  _ShearX;
                float  _HeightScale;
                float  _AlphaThreshold;
                float  _ExpandFar;

                Varyings vert(Attributes v)
                {
                    Varyings o;

                    float2 uv = TRANSFORM_TEX(v.uv, _MainTex);
                    float3 pos = v.positionOS.xyz;

                    float y01 = saturate(uv.y);

                    float spriteHeight = _MainTex_TexelSize.w;

                    // uv.y=0 を positionOS.y の下端に合わせる
                    float baseY = pos.y - (y01 * spriteHeight);

                    // 高さスケール（下端基準）
                    pos.y = -baseY + (y01 * spriteHeight * _HeightScale);

                    // uv.x を中心(0.5)基準で左右に分ける
                    float side = (uv.x - 0.5) >= 0.0 ? 1.0 : -1.0;

                    // 高さに応じて左右へ拡張
                    float shadowExtend = lerp(0, _ExpandFar, y01);
                    pos.x += side * shadowExtend;

                    // 傾き
                    pos.x += _ShearX * y01;

                    o.positionHCS = TransformObjectToHClip(pos);
                    o.uv = uv;
                    return o;
                }

                inline void MaxAlpha(float2 baseUV, float2 offset, inout float aMax)
                {
                    aMax = max(
                        aMax,
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, baseUV + offset).a
                    );
                }

                half4 frag(Varyings i) : SV_Target
                {
                    float alpha =
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;

                    clip(alpha - _AlphaThreshold);

                    float shadowAlpha = alpha * _ShadowStrength;
                    return float4(_ShadowColor.rgb, shadowAlpha);
                }


                ENDHLSL
            }
        }
}