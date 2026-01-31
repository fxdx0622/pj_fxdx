using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using static UnityEngine.Rendering.Universal.ShaderInput;

/// <summary>
/// ラディアルブラーのシェーダーで用いるパラメータ。
/// </summary>
[Serializable]
public class RadialBlurParams
{
	[Range(0, 1), Tooltip("ブラーの強さ")] public float Intensity = 0.4f;
	[Min(1), Tooltip("サンプリング回数")] public int SampleCount = 3;
	[Tooltip("エフェクトの中心")] public Vector2 RadialCenter = new Vector2(0.5f, 0.5f);
	[Tooltip("ディザリングを利用する")] public bool UseDither = true;
	[Tooltip("ラディアルブラーのシェーダー")] public Shader Shader;
}

public class RadialBlurFeature : ScriptableRendererFeature
{
	[SerializeField] private RadialBlurParams _parameters;
	[SerializeField] private RenderPassEvent _renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
	private RadialBlurPass _pass;

	public override void Create()
	{
		_pass = new RadialBlurPass(_parameters)
		{
			renderPassEvent = _renderPassEvent,
		};
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		if (_pass != null) renderer.EnqueuePass(_pass);
	}

	public void OnDestroy() => _pass?.Dispose();
}

public class RadialBlurPass : ScriptableRenderPass
{
	private readonly Material _material;
	private readonly RadialBlurParams _parameters;
	private readonly LocalKeyword _keywordUseDither;

	private static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
	private static readonly int IdSampleCountParams = Shader.PropertyToID("_SampleCountParams");
	private static readonly int IdRadialCenter = Shader.PropertyToID("_RadialCenter");

	public RadialBlurPass(RadialBlurParams parameters)
	{
		_parameters = parameters;
		if (!_parameters.Shader)
		{
			Debug.LogError("ラディアルブラーのシェーダーをセットしてください。");
			return;
		}
		//シェーダーの取得、マテリアルとキーワードの生成。
		_material = CoreUtils.CreateEngineMaterial(parameters.Shader);
		_keywordUseDither = new LocalKeyword(parameters.Shader, "USE_DITHER");
	}

	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		if (!_material) return;

		// リソースの情報を取得
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

		//ラディアルブラーのパラメータを設定。
		_material.SetFloat(IdIntensity, _parameters.Intensity);
		_material.SetVector(IdSampleCountParams,
			new Vector3(
				_parameters.SampleCount,
				1f / _parameters.SampleCount,
				2 <= _parameters.SampleCount ? 1f / (_parameters.SampleCount - 1) : 1));
		_material.SetVector(IdRadialCenter, _parameters.RadialCenter);
		_material.SetKeyword(_keywordUseDither, _parameters.UseDither);

		//カメラに映すテクスチャの取得。
		TextureHandle cameraTexture = resourceData.activeColorTexture;

		//一時的なテクスチャの性質を決めるDescriptorを取得。
		TextureDesc tempDesc = renderGraph.GetTextureDesc(cameraTexture);
		tempDesc.name = "_RadialBlurTempTexture";
		//一時的なテクスチャの取得。
		TextureHandle tempTexture = renderGraph.CreateTexture(tempDesc);

		//Blitに必要なデータの用意。
		RenderGraphUtils.BlitMaterialParameters blitParameters = new(cameraTexture, tempTexture, _material, 0);

		//カメラバッファから一時テクスチャへのBlit。ラディアルブラーのエフェクトがかかる。 
		renderGraph.AddBlitPass(blitParameters, "Radial Blur Effect");

		//一時テクスチャからカメラテクスチャへのBlit。ただのコピー。
		renderGraph.AddCopyPass(tempTexture, cameraTexture, "Radial Blur Copy");
	}

	public void Dispose()
	{
		CoreUtils.Destroy(_material);
	}
}
