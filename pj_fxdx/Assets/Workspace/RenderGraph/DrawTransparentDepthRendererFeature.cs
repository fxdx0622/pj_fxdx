using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class DrawTransparentDepthRendererFeature : ScriptableRendererFeature
{
	[SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
	private DrawTransparentDepthRenderPass pass;

	public override void Create()
	{
		pass = new DrawTransparentDepthRenderPass()
		{
			renderPassEvent = renderPassEvent,
		};
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		if (pass != null) renderer.EnqueuePass(pass);
	}
}