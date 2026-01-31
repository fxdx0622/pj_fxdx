Shader "Custom/SH_PixelArtShader"
{
    Properties
    {
        _MainTex("MainTexture", 2D) = "white"{}
        _TextureWidth("TextureWidth", int) = 32
        _TextureHeight("TextureHeight", int) = 32
        _ColorResolution("ColorResolution", int) = 16
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Cull Back
            ZWrite Off
            BlendOp Add
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
            
            struct appData
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct v2f
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            int _TextureWidth;
            int _TextureHeight;
            int _ColorResolution;

            v2f vert(appData IN)
            {
                v2f o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS);
                o.uv = IN.uv;
                return o;
            }

            half4 frag(v2f input) : SV_Target
            {
                float x = floor(input.uv.x * _TextureWidth) / _TextureWidth;
                float y = floor(input.uv.y * _TextureHeight) / _TextureHeight;

                float2 uv2 = float2(x, y);

                float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv2);

                color.r = floor(color.r * _ColorResolution) / _ColorResolution;
                color.g = floor(color.g * _ColorResolution) / _ColorResolution;
                color.b = floor(color.b * _ColorResolution) / _ColorResolution;
                color.a = floor(color.a * _ColorResolution) / _ColorResolution;

                return color;
            }

            ENDHLSL
        }
    }
}
