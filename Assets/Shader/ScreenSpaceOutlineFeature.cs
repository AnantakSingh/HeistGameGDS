using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class ScreenSpaceOutlineFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class OutlineSettings
    {
        public Material outlineMaterial;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        
        [Range(0f, 1f)]
        public float darkenFactor = 0.5f;
        public float thickness = 1f;
        public float depthThreshold = 0.01f;
        public float normalThreshold = 0.5f;
        public float distanceFadeStart = 10f;
        public float distanceFadeEnd = 50f;
    }

    public OutlineSettings settings = new OutlineSettings();
    private ScreenSpaceOutlinePass outlinePass;

    public override void Create()
    {
        if (settings.outlineMaterial == null)
            return;

        outlinePass = new ScreenSpaceOutlinePass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection)
            return;

        if (settings.outlineMaterial == null)
            return;

        if (outlinePass == null)
            outlinePass = new ScreenSpaceOutlinePass(settings);

        outlinePass.Setup(renderer.cameraColorTargetHandle);
        renderer.EnqueuePass(outlinePass);
    }

    class ScreenSpaceOutlinePass : ScriptableRenderPass
    {
        private Material outlineMaterial;
        private OutlineSettings settings;
        private RTHandle cameraColorTarget;
        private RTHandle temporaryColorTexture;

        public ScreenSpaceOutlinePass(OutlineSettings settings)
        {
            this.settings = settings;
            this.outlineMaterial = settings.outlineMaterial;
            this.renderPassEvent = settings.renderPassEvent;
            // Tell URP we need depth and normal for the Render Graph
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Color);
        }

        public void Setup(RTHandle colorTarget)
        {
            this.cameraColorTarget = colorTarget;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Color);

            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0; 

            RenderingUtils.ReAllocateIfNeeded(ref temporaryColorTexture, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_TemporaryOutlineColorTexture");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (outlineMaterial == null)
                return;

            RTHandle colorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
            if (colorTarget == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("Screen Space Outline");

            outlineMaterial.SetFloat("_DarkenFactor", settings.darkenFactor);
            outlineMaterial.SetFloat("_Thickness", settings.thickness);
            outlineMaterial.SetFloat("_DepthThreshold", settings.depthThreshold);
            outlineMaterial.SetFloat("_NormalThreshold", settings.normalThreshold);
            outlineMaterial.SetFloat("_DistanceFadeStart", settings.distanceFadeStart);
            outlineMaterial.SetFloat("_DistanceFadeEnd", settings.distanceFadeEnd);

            Blitter.BlitCameraTexture(cmd, colorTarget, temporaryColorTexture, outlineMaterial, 0);
            Blitter.BlitCameraTexture(cmd, temporaryColorTexture, colorTarget);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private class PassData
        {
            internal TextureHandle src;
            internal Material material;
            internal OutlineSettings settings;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (outlineMaterial == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle src = resourceData.activeColorTexture;
            if (!src.IsValid()) return;

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            
            TextureHandle dst = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_OutlineTemp", false);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Screen Space Outline Pass", out var passData))
            {
                passData.src = src;
                passData.material = outlineMaterial;
                passData.settings = settings;

                builder.UseTexture(src, AccessFlags.Read);
                builder.SetRenderAttachment(dst, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    data.material.SetFloat("_DarkenFactor", data.settings.darkenFactor);
                    data.material.SetFloat("_Thickness", data.settings.thickness);
                    data.material.SetFloat("_DepthThreshold", data.settings.depthThreshold);
                    data.material.SetFloat("_NormalThreshold", data.settings.normalThreshold);
                    data.material.SetFloat("_DistanceFadeStart", data.settings.distanceFadeStart);
                    data.material.SetFloat("_DistanceFadeEnd", data.settings.distanceFadeEnd);
                    
                    Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Outline Copy Back", out var passData))
            {
                passData.src = dst;

                builder.UseTexture(dst, AccessFlags.Read);
                builder.SetRenderAttachment(src, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), 0.0f, false);
                });
            }
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }

        public void Dispose()
        {
            temporaryColorTexture?.Release();
        }
    }

    protected override void Dispose(bool disposing)
    {
        outlinePass?.Dispose();
    }
}
