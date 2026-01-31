using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class DrawTransparentDepthRenderPass : ScriptableRenderPass
{
	private readonly ShaderTagId shaderTagId = new ShaderTagId("TransparentDepthOnly");

	private class PassData
	{
		public RendererListHandle list;
	}

	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frame)
	{
		UniversalCameraData cameraData = frame.Get<UniversalCameraData>();
		UniversalResourceData resourceData = frame.Get<UniversalResourceData>();
		UniversalRenderingData renderingData = frame.Get<UniversalRenderingData>();

		RendererListDesc rlDesc = new RendererListDesc(shaderTagId, renderingData.cullResults, cameraData.camera);
		rlDesc.renderQueueRange = RenderQueueRange.transparent;
		rlDesc.sortingCriteria = SortingCriteria.CommonTransparent;
		rlDesc.layerMask = cameraData.camera.cullingMask;
		using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("Draw Transparent Depth", out PassData passData);

		passData.list = renderGraph.CreateRendererList(rlDesc);
		builder.UseRendererList(passData.list);

		builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);
		builder.SetRenderFunc(static (PassData d, RasterGraphContext ctx) => { ctx.cmd.DrawRendererList(d.list); });
	}
}
