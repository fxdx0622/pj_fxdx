Shader "Custom/SH_GridEffect"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Grid Effect"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            float4 _BaseColor;
            float4 _GridColor;
            float _GridCount;
            float _FadeDuration;

            float random(float2 seeds)
            {
                return frac(sin(dot(seeds, float2(12.9898, 78.233))) * 43758.5453);
            }

            float SmoothRandom(float2 uv, float time)
            {
                float v = 0.0;

                // 周波数1
                {
                    float tt = frac(time * 0.5);
                    float r0 = random(uv + floor(time * 0.5));
                    float r1 = random(uv + floor(time * 0.5) + 1.0);
                    v += lerp(r0, r1, smoothstep(0.0, 1.0, tt)) * 0.6;
                }

                // 周波数2
                {
                    float tt = frac(time * 1.3);
                    float r0 = random(uv + floor(time * 1.3));
                    float r1 = random(uv + floor(time * 1.3) + 1.0);
                    v += lerp(r0, r1, smoothstep(0.0, 1.0, tt)) * 0.3;
                }

                // 周波数3
                {
                    float tt = frac(time * 2.1);
                    float r0 = random(uv + floor(time * 2.1));
                    float r1 = random(uv + floor(time * 2.1) + 1.0);
                    v += lerp(r0, r1, smoothstep(0.0, 1.0, tt)) * 0.1;
                }

                return v;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Original
                float2 uv = input.texcoord;

                float2 timeJitter = float2(
                    SmoothRandom(uv, _Time.y * 1.1),
                    SmoothRandom(uv + 10.0, _Time.y * 1.7)
                    ) * 0.3;     // 揺れの強さ（0.1〜0.5 推奨）

                float2 movingBlock = floor((uv + timeJitter) * _GridCount) / _GridCount;
                float smoothRand = SmoothRandom(movingBlock, _Time.y * 10); // 10〜30 で速さ調整

                float offset = smoothRand * 0.1 / 5;
              
                float4 original = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);

                // Noise
                float2 noiseUV = float2(uv.x, uv.y);
                float4 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV + float2(offset, offset));

                noise.a = 0.8;

                // Fade
                float fadeDuration = _FadeDuration;
                float softness = 0.05;

                float progress = saturate(_Time.y / fadeDuration);
                float edgeY = 1.0 - progress;

                float maskBorder = 1.0 - smoothstep(edgeY - softness, edgeY + softness, uv.y);

                float endFadeStart = 0.9;
                float endProgress = saturate((progress - endFadeStart) / (1.0 - endFadeStart));

                float smoothEndFade = 1.0 - smoothstep(0.0, 1.0, endProgress);

                float finalMask = maskBorder * smoothEndFade;

                return  original + noise * finalMask;
            }

            ENDHLSL
        }
    }
}