Shader "Custom/SH_CreepyFilter"
{
    Properties
    {
        // 入力
        _MainTex  ("Sprite (shape/alpha)", 2D) = "white" {}
        _CameraRT ("2ndTexture 〈Source=Texture の時のみ参照〉", 2D) = "white" {}
        [Header(Source)]
        [Enum(2ndTexture,0, BaseColor,1)] _Src ("Source", Float) = 1
        _BaseColor("BaseColor 〈Source=BaseColor の時のみ有効〉", Color) = (1,1,1,1)

        // 色処理
        [Header(Color_Processing)]
        [Toggle] _UseTintColor ("UseTintColor 〈ON の時のみTint有効〉", Float) = 0
        _TintColor ("TintColor", Color) = (1,1,1,1)
        _TintAmount("TintAmount 〈UseTint=ON の時のみ有効〉", Range(0,1)) = 0.0
        _WhitePush ("WhitePush (全域白寄せ)", Range(0,1)) = 0.0

        // グラデーション
        [Header(Gradient_Settings)]
        [Enum(Vertical,0, Horizontal,1)] _GradAxis ("Gradient Axis", Float) = 0
        _GradTopY     ("GradStart (Top/Start)",   Range(0,1)) = 1
        _GradBottomY  ("GradEnd   (Bottom/End)",  Range(0,1)) = 0
        _GradPower    ("GradCurve (1=Linear)",    Range(0.1,5)) = 1
        [Toggle] _GradUseColor ("UseGradColor 〈ON の時のみGradColor 有効〉", Float) = 0
        _GradColor    ("Gradient Color", Color) = (1,1,1,1)
        _GradAmount   ("Grad Amount", Range(0,1)) = 0.5

        // 透明度
        [Header(Alpha)]
        [Enum(Shape,0, Gradient,1, Constant,2)] _AlphaMode ("Alpha Mode", Float) = 1
        _AlphaMin   ("AlphaGradMin 〈Mode=Gradient の時のみ有効〉", Range(0,1)) = 0.0
        _AlphaMax   ("AlphaGradMax 〈Mode=Gradient の時のみ有効〉", Range(0,1)) = 1.0
        _AlphaConst ("AlphaConstant 〈Mode=Constant の時のみ有効〉", Range(0,1)) = 1.0
        [Toggle] _MulShapeA ("Multiply by Sprite Alpha 〈全モードで最終αに shape.a を乗算〉", Float) = 0

        // UV反転
        [Header(Flip_RT_Sampling)]
        [Toggle] _FlipRT_V ("FlipVertical 〈uvの上下反転〉",   Float) = 0
        [Toggle] _FlipRT_H ("FlipHorizontal 〈uvの左右反転〉", Float) = 0

        // スクロール（トグル→速度→ラップ）
        [Header(Scroll)]
        [Toggle] _ScrollRT ("EnableUVScroll", Float) = 1
        _UVScroll ("UVScrollSpeed", Vector) = (0.02, 0, 0, 0)
        [Toggle] _Wrap ("WrapUV (Repeat 1 / Clamp 0)", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f     { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float2 uvRaw:TEXCOORD1; fixed4 color:COLOR; };

            sampler2D _MainTex;   float4 _MainTex_ST;
            sampler2D _CameraRT;  float4 _CameraRT_ST;

            // 入力
            float  _Src;
            fixed4 _BaseColor;

            // 色処理
            float  _UseTintColor;
            fixed4 _TintColor; float _TintAmount;
            float  _WhitePush;

            // グラデ
            float  _GradAxis;
            float  _GradTopY, _GradBottomY, _GradPower, _GradAmount;
            fixed4 _GradColor; float _GradUseColor;

            // アルファ
            float _AlphaMode;
            float _AlphaMin, _AlphaMax, _AlphaConst;
            float _MulShapeA;

            // UV反転
            float _FlipRT_V, _FlipRT_H;

            // スクロール
            float  _ScrollRT;
            float4 _UVScroll;
            float  _Wrap;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = TRANSFORM_TEX(v.uv, _MainTex);
                o.uvRaw = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // UVスクロール（継ぎ目対策）
                float2 baseUV_Main = i.uvRaw;

                // 連続UVを作る
                float2 uvRT_unwrapped = TRANSFORM_TEX(baseUV_Main, _CameraRT);

                // スクロール量加算
                float2 scrollPerSec = _UVScroll.xy * _CameraRT_ST.xy;
                uvRT_unwrapped += (_ScrollRT > 0.5) ? (scrollPerSec * _Time.y) : float2(0,0);

                // 反転
                bool flipH = (_FlipRT_H > 0.5);
                bool flipV = (_FlipRT_V > 0.5);
                uvRT_unwrapped.x = flipH ? (1.0 - uvRT_unwrapped.x) : uvRT_unwrapped.x;
                uvRT_unwrapped.y = flipV ? (1.0 - uvRT_unwrapped.y) : uvRT_unwrapped.y;
                float2 uvRT_wrapped = (_Wrap > 0.5) ? frac(uvRT_unwrapped) : saturate(uvRT_unwrapped);
                float2 du = ddx(uvRT_unwrapped);
                float2 dv = ddy(uvRT_unwrapped);

                // flip した軸の勾配は符号が反転しているはずなので反映
                if (flipH) { du.x = -du.x; dv.x = -dv.x; }
                if (flipV) { du.y = -du.y; dv.y = -dv.y; }

                // サンプリングは tex2Dgrad で勾配を明示 → 境界の細線が消える
                fixed3 baseRT = tex2Dgrad(_CameraRT, uvRT_wrapped, du, dv).rgb;

                // ベース色
                fixed3 col = (_Src < 0.5) ? baseRT : _BaseColor.rgb;

                // 全域色処理
                float useTint = step(0.5, _UseTintColor);
                col = lerp(col, _TintColor.rgb, saturate(_TintAmount) * useTint);
                col = lerp(col, 1.0.xxx, saturate(_WhitePush));

                // グラデ
                float sAxis = (_GradAxis < 0.5) ? i.uv.y : i.uv.x;
                float startPos = _GradTopY;
                float endPos   = _GradBottomY;
                float delta = startPos - endPos;
                float t = (delta >= 0.0)
                    ? saturate((startPos - sAxis) / max(1e-5, delta))
                    : saturate((sAxis - startPos) / max(1e-5, -delta));
                t = pow(smoothstep(0.0, 1.0, t), _GradPower);

                fixed3 gradTarget = lerp(1.0.xxx, _GradColor.rgb, step(0.5, _GradUseColor));
                col = lerp(col, gradTarget, saturate(_GradAmount) * t);

                //  透明度
                float aShape = tex2D(_MainTex, i.uv).a;
                float a;
                if      (_AlphaMode < 0.5) a = aShape;
                else if (_AlphaMode < 1.5) a = lerp(_AlphaMin, _AlphaMax, t);
                else a = _AlphaConst;
                a *= lerp(1.0, aShape, step(0.5, _MulShapeA));

                col *= i.color.rgb;
                a   = saturate(a * i.color.a);
                
                return fixed4(col, a);
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
