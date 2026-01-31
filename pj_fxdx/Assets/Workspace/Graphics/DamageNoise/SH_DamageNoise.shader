Shader "Custom/DamageNoise"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Color ("Color", Color) = (1, 1, 1, 1)
		_Bias ("Bias", float) = 1
		_ScanningFrequency ("Scanning Frequency", float) = 400
		_ScanningSpeed ("Scanning Speed", float) = 10
		_GlitchFrameRate("GlitchFrameRate", Range(0,60)) = 15
		_GlitchFrequency("GlitchFrequency", Range(0,1)) = 1
		_GlitchScale("GlitchScale", Range(0,10)) = 1
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
			Cull Off
			ZWrite On
			BlendOp Add
			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 objVertex : TEXCOORD1;
			};

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			float4 _Color;
			float _Bias;
			float _ScanningFrequency;
			float _ScanningSpeed;
			float _GlitchFrameRate;
			float _GlitchFrequency;
			float _GlitchScale;

			float rand(float2 co)
			{
				return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
			}

			float perlinNoise(float2 st)
			{
				float2 p = floor(st);
				float2 f = frac(st);
				float2 u = f * f * (3.0 - 2.0 * f);

				float v00 = rand(p + float2(0, 0));
				float v10 = rand(p + float2(1, 0));
				float v01 = rand(p + float2(0, 1));
				float v11 = rand(p + float2(1, 1));

				return lerp(lerp(dot(v00, f - float2(0, 0)), dot(v10, f - float2(1, 0)), u.x),
					lerp(dot(v01, f - float2(0, 1)), dot(v11, f - float2(1, 1)), u.x),
					u.y) + 0.5f;
			}

			float random(float2 seeds)
			{
				return frac(sin(dot(seeds, float2(12.9898, 78.233))) * 43758.5453);
			}

			float blockNoise(float2 seeds)
			{
				return random(floor(seeds));
			}

			float noiserandom(float2 seeds)
			{
				return -1.0 + 2.0 * blockNoise(seeds);
			}

			float2 glitch_noise_calculate(float2 uv)
			{
				float posterize = floor(frac(perlinNoise(frac(_Time)) * 10) / (1 / _GlitchFrameRate)) * (1 / _GlitchFrameRate);

				float noiseY = 2.0 * rand(posterize) - 0.5;

				float glitchLine1 = step(uv.y - noiseY, rand(uv));
				float glitchLine2 = step(uv.y - noiseY, 0);
				noiseY = saturate(glitchLine1 - glitchLine2);

				float noiseX = (2.0 * rand(posterize) - 0.5) * 0.1;
				float frequency = step(abs(noiseX), _GlitchFrequency);
				noiseX *= frequency / 10.0;
				return float2(noiseX, noiseY);
			}

			v2f vert(appdata v)
			{
				v2f o;

				o.objVertex = mul(unity_ObjectToWorld, v.vertex);
				o.vertex = TransformObjectToHClip(v.vertex);
				o.uv = v.uv;

				float2 noise = glitch_noise_calculate(o.uv);
				o.vertex.x = lerp(o.vertex.x, o.vertex.x + noise.x * _GlitchScale, noise.y);

				return o;
			}

			half4 frag(v2f input) : SV_Target
			{
				float2 uv = input.uv;

				float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

				float scan1 = saturate(cos(input.vertex.x * _ScanningFrequency) * 0.5 + 0.5);
				float scan2 = saturate(cos(input.objVertex.y * _ScanningFrequency + (_Time.y * _ScanningSpeed)) * 0.5 + 0.5);
				float scan3 = saturate(cos(input.objVertex.y * 29 + (_Time.y * 0.7)) * 0.5 + 0.5);

				float scan = scan1 * scan2 * scan3;

				float4 effect = _Color * scan * _Bias * 5;

				return tex + effect * tex.a;
			}

			ENDHLSL
		}
	}
}
