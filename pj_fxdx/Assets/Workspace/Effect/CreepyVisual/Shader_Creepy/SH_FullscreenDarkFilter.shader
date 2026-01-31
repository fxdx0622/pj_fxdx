Shader "Custom/SH_FullscreenDarkFilter"
{
    Properties
    {
        _Strength ("Strength (0..1)", Range(0,1)) = 0.45

        // 中央を守る半径 / 最大暗化になる半径（0=中心, 1=端）
        _Inner ("Inner Radius (start)", Range(0,1)) = 0.82
        _Outer ("Outer Radius (max)",   Range(0,1)) = 0.97

        _Power     ("Falloff Power", Range(0.5,4)) = 2.2
        _Roundness ("Roundness",     Range(0.3,4)) = 2.0
        _Center    ("Center (UV 0..1)", Vector) = (0.5, 0.5, 0, 0)

        // アスペクト
        // 0=Circle（画面比で正円補正）/ 1=Display（補正なし=横長画面で楕円）/ 2=Manual（手動ストレッチ）
        [KeywordEnum(Circle,Display,Manual)] _AspectMode ("Aspect Mode", Float) = 0
        // 手動ストレッチ（Manual時のみ有効） 1で等倍、X>1で横に広い楕円、Y>1で縦に広い楕円
        _AxisStretch ("Manual Stretch (X,Y)", Vector) = (1,1,0,0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent+200" "RenderType"="Transparent" }
        Cull Off 
        ZWrite Off
        Blend DstColor OneMinusSrcAlpha
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct app { float4 v:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 p:SV_POSITION; float2 suv:TEXCOORD0; };

            float _Strength, _Inner, _Outer, _Power, _Roundness;
            float4 _Center;
            float _AspectMode;
            float4 _AxisStretch;

            v2f vert(app i){
                v2f o; o.p = UnityObjectToClipPos(i.v);
                float4 sp = ComputeScreenPos(o.p);
                o.suv = sp.xy / sp.w;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 中心基準の -1..1
                float2 uv = i.suv;
                float2 c  = _Center.xy;
                float2 p  = (uv - c) * 2.0;

                // アスペクト＆楕円モード処理
                float aspect = _ScreenParams.x / _ScreenParams.y;
                // Circle: 画面比で正円補正（横長でも正円）
                // Display: 補正なし → 16:9 などで自然に横長楕円
                // Manual : 正円補正の上で手動ストレッチ（X>1=横に広い/ Y>1=縦に広い）
                if (_AspectMode < 0.5) {
                    p.x *= aspect;                         // Circle
                } else if (_AspectMode < 1.5) {
                    // Display（何もしない）
                } else {
                    p.x *= aspect;                         // 基準は正円
                    float2 s = max(_AxisStretch.xy, float2(1e-3,1e-3));
                    p.x /= s.x;                            // X>1 で横に広い楕円
                    p.y /= s.y;                            // Y>1 で縦に広い楕円
                }

                // スーパー楕円距離（Roundnessで形状調整）
                float2 ap = abs(p);
                float d = pow(pow(ap.x, _Roundness) + pow(ap.y, _Roundness), 1.0 / _Roundness); // 0..≳1

                // 内外半径で 0..1 に正規化 → Power でカーブ → 強さ
                float t = saturate((d - _Inner) / max(1e-5, (_Outer - _Inner)));
                float v = pow(t, _Power) * _Strength;

                // 黒×α（Blendで暗化）
                return fixed4(0,0,0, v);
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
