using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// グリッチエフェクトのシェーダーで用いるパラメータ。
/// </summary>
[Serializable]
public class GridEffectParams
{
	public Color BaseColor;
	public Color GridColor;

	public Texture NoiseTex;
	public float GridCount = 3;
	public float FadeDuration = 1.5f;

	[Tooltip("グリッチエフェクトのシェーダー")] public Shader Shader;
}

public class GridEffectFeature : ScriptableRendererFeature
{
	[SerializeField] private GridEffectParams _parameters;
	[SerializeField] private RenderPassEvent _renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
	private GridEffectPass _pass;

	public override void Create()
	{
		_pass = new GridEffectPass(_parameters)
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

public class GridEffectPass : ScriptableRenderPass
{
	private readonly Material _material;
	private readonly GridEffectParams _parameters;

	private static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");
	private static readonly int IdGridColor = Shader.PropertyToID("_GridColor");

	private static readonly int IdNoiseTex = Shader.PropertyToID("_NoiseTex");
	private static readonly int IdGridCount = Shader.PropertyToID("_GridCount");
	private static readonly int IdFadeDuration = Shader.PropertyToID("_FadeDuration");

	public GridEffectPass(GridEffectParams parameters)
	{
		_parameters = parameters;
		if (!_parameters.Shader)
		{
			Debug.LogError("グリッチのシェーダーをセットしてください。");
			return;
		}
		//シェーダーの取得、マテリアルとキーワードの生成。
		_material = CoreUtils.CreateEngineMaterial(parameters.Shader);
	}

	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		if (!_material) return;

		//リソース関係のデータ（カメラのテクスチャなど）を取得する。
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

		//グリッチのパラメータを設定。
		_material.SetColor(IdBaseColor, _parameters.BaseColor);
		_material.SetColor(IdGridColor, _parameters.GridColor);

		_material.SetTexture(IdNoiseTex, _parameters.NoiseTex);
		_material.SetFloat(IdGridCount, _parameters.GridCount);
		_material.SetFloat(IdFadeDuration, _parameters.FadeDuration);

		//カメラに映すテクスチャの取得。
		TextureHandle cameraTexture = resourceData.activeColorTexture;

		//一時的なテクスチャの性質を決めるDescriptorを取得。
		TextureDesc tempDesc = renderGraph.GetTextureDesc(cameraTexture);
		tempDesc.name = "_GridEffectTempTexture";
		//一時的なテクスチャの取得。
		TextureHandle tempTexture = renderGraph.CreateTexture(tempDesc);

		//Blitに必要なデータの用意。
		RenderGraphUtils.BlitMaterialParameters blitParameters = new(cameraTexture, tempTexture, _material, 0);

		//カメラバッファから一時テクスチャへのBlit。ラディアルブラーのエフェクトがかかる。 
		renderGraph.AddBlitPass(blitParameters, "Grid Effect");

		//一時テクスチャからカメラテクスチャへのBlit。ただのコピー。
		renderGraph.AddCopyPass(tempTexture, cameraTexture, "Grid Effect Copy");
	}

	public void Dispose()
	{
		CoreUtils.Destroy(_material);
	}
}
