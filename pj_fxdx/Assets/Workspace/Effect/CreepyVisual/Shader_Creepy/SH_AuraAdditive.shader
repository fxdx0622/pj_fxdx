Shader "Custom/SH_AuraAdditive"
{
    Properties
    {
        // 入力
        _MainTex   ("Mask (R/A/Luma accepted)", 2D) = "white" {}
        _NoiseTex  ("Flow Noise (Gray/R)",      2D) = "gray"  {}

        // 色
        _InnerColor ("Inner Color", Color) = (1.0, 0.12, 0.10, 1)
        _OuterColor ("Outer Color", Color) = (1.0, 0.55, 0.10, 1)
        _Opacity    ("Opacity", Range(0,1)) = 1.0

        // マスクの二値化
        _MaskCut   ("Mask Cut (0..1)",  Range(0,1))   = 0.5
        _MaskSoft  ("Mask Soft (0..1)", Range(0,0.5)) = 0.15

        // ノイズ帯域
        _Threshold ("Cutoff",  Range(0,1))   = 0.55
        _Feather   ("Feather", Range(0,0.5)) = 0.12

        // ノイズ移動
        _NoiseScale ("Noise Scale", Float) = 2.0
        _NoiseSpeed ("Noise Speed (U,V)", Vector) = (0.07, -0.03, 0, 0)
        _PulseHz    ("Pulse (Hz)", Range(0,3)) = 0.8

        // 帯状ゆらぎ（X/Y/両方 切替）
        [Toggle] _UseWave ("Enable Wave", Float) = 0
        [Enum(Y,0, X,1, Both,2)] _WaveAxis ("Wave Axis", Float) = 0
        _WaveAmp      ("Wave Amplitude (UV)", Range(0,0.5)) = 0.05
        _WaveFreq     ("Wave Frequency (cycles across perpendicular axis)", Float) = 4
        _WaveSpeed    ("Wave Speed (Hz)", Float) = 1.0
        _WavePhaseDeg ("Wave Start Phase (deg)", Range(0,360)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent+50" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off 
        ZWrite Off
        Blend One One   // 加算
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f     { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            sampler2D _MainTex;  float4 _MainTex_ST;
            sampler2D _NoiseTex; float4 _NoiseTex_ST;

            float4 _InnerColor, _OuterColor;
            float  _Opacity, _MaskCut, _MaskSoft;
            float  _Threshold, _Feather;
            float  _NoiseScale;   float4 _NoiseSpeed;
            float  _PulseHz;

            // Wave
            float  _UseWave, _WaveAxis, _WaveAmp, _WaveFreq, _WaveSpeed, _WavePhaseDeg;

            inline float luma709(float3 c){ return dot(c, float3(0.2126,0.7152,0.0722)); }
            inline float remap01(float x, float a, float b){ return saturate((x - a) / max(1e-5, b - a)); }

            // マスク：R/α/輝度の“強い方”を取り、滑らかに二値化
            float sampleMask(sampler2D tex, float2 uv){
                float4 m = tex2D(tex, uv);
                float raw = max(max(m.a, m.r), luma709(m.rgb));
                return smoothstep(_MaskCut - _MaskSoft, _MaskCut + _MaskSoft, raw);
            }
            // グレー取得：Rと輝度のmax
            float sampleGray(sampler2D tex, float2 uv){
                float4 t = tex2D(tex, uv);
                return max(t.r, luma709(t.rgb));
            }

            v2f vert (appdata v){
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 帯状ゆらぎ：X/Y/両方に対応
                float2 uvBase = i.uv;
                if (_UseWave > 0.5)
                {
                    float t = _Time.y;
                    float phase = radians(_WavePhaseDeg);
                    // 直交座標に沿ってサインを生成
                    float sFromX = sin(6.2831853 * (_WaveFreq * uvBase.x + _WaveSpeed * t) + phase); // Xに沿って変化
                    float sFromY = sin(6.2831853 * (_WaveFreq * uvBase.y + _WaveSpeed * t) + phase); // Yに沿って変化

                    // _WaveAxis: 0=Yを揺らす(縞はX方向に走る) / 1=Xを揺らす(縞はY方向) / 2=両方
                    if      (_WaveAxis < 0.5) { uvBase.y += _WaveAmp * sFromX; }       // Y揺れ（横縞）
                    else if (_WaveAxis < 1.5) { uvBase.x += _WaveAmp * sFromY; }       // X揺れ（縦縞）
                    else                      { uvBase.y += _WaveAmp * sFromX;         // 両方
                                               uvBase.x += _WaveAmp * sFromY; }
                }
                // ※テクスチャは Wrap=Repeat 推奨（継ぎ目に強い）

                // マスク
                float aMask = sampleMask(_MainTex, uvBase);

                // ノイズ（同じ揺れを適用）
                float2 uvN = (uvBase * _NoiseScale) + _NoiseSpeed.xy * _Time.y;
                float n1 = sampleGray(_NoiseTex, uvN);
                float n2 = sampleGray(_NoiseTex, uvN * 2.13);
                float n  = saturate(n1*0.6 + n2*0.4);

                // 帯域（しきい値＋フェザー＋脈動）
                float pulse = 0.5 + 0.5 * sin(6.28318 * _PulseHz * _Time.y);
                float th = saturate(_Threshold + (pulse - 0.5) * 0.06);
                float band  = remap01(n * aMask, th, th + _Feather);
                float alpha = band * _Opacity;

                // 色
                float3 col = lerp(_InnerColor.rgb, _OuterColor.rgb, n*n) * alpha;

                return float4(col, alpha);
            }
            ENDHLSL
        }
        
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

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            struct Attributes
            {
                float4 position     : POSITION;
                float2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float _ZWriteValue;

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.positionCS = TransformObjectToHClip(input.position.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input, out float outputDepth : SV_Depth) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a - 0.5);
                outputDepth = _ZWriteValue;
                return 0;
            }
            ENDHLSL
        }
    }
}
