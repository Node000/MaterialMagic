using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ScreenEdgeScribbleRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public LayerMask layerMask;
        public Material compositeMaterial;
    }

    [SerializeField] private Settings settings = new Settings();

    private MaskPass maskPass;
    private CompositePass compositePass;

    public override void Create()
    {
        maskPass = new MaskPass(settings.layerMask)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents
        };
        compositePass = new CompositePass(settings.compositeMaterial)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 1
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.compositeMaterial == null)
            return;

        compositePass.SetMaterial(settings.compositeMaterial);
        renderer.EnqueuePass(maskPass);
        renderer.EnqueuePass(compositePass);
    }

    protected override void Dispose(bool disposing)
    {
        maskPass?.Dispose();
    }

    private sealed class MaskPass : ScriptableRenderPass
    {
        private static readonly ShaderTagId MaskShaderTag = new ShaderTagId("ScreenEdgeScribbleMask");
        private static readonly int MaskTextureId = Shader.PropertyToID("_ScreenEdgeScribbleMask");

        private FilteringSettings filteringSettings;
        private RTHandle maskTexture;

        public MaskPass(LayerMask layerMask)
        {
            filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(ref maskTexture, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_ScreenEdgeScribbleMask");
            ConfigureTarget(maskTexture, renderingData.cameraData.renderer.cameraDepthTargetHandle);
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Screen Edge Scribble Mask");
            using (new ProfilingScope(cmd, new ProfilingSampler("Screen Edge Scribble Mask")))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                SortingCriteria sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
                DrawingSettings drawingSettings = CreateDrawingSettings(MaskShaderTag, ref renderingData, sortingCriteria);
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
                cmd.SetGlobalTexture(MaskTextureId, maskTexture.nameID);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            maskTexture?.Release();
        }
    }

    private sealed class CompositePass : ScriptableRenderPass
    {
        private Material compositeMaterial;

        public CompositePass(Material material)
        {
            compositeMaterial = material;
        }

        public void SetMaterial(Material material)
        {
            compositeMaterial = material;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (compositeMaterial == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("Screen Edge Scribble Composite");
            using (new ProfilingScope(cmd, new ProfilingSampler("Screen Edge Scribble Composite")))
            {
                CoreUtils.SetRenderTarget(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle);
                CoreUtils.DrawFullScreen(cmd, compositeMaterial);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
