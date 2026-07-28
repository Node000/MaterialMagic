using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CrayonScreenFilterRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingTransparents;
    }

    [SerializeField] private Settings settings = new Settings();

    private CrayonPass pass;

    public override void Create()
    {
        pass = new CrayonPass(settings.material)
        {
            renderPassEvent = settings.injectionPoint
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material == null || renderingData.cameraData.cameraType != CameraType.Game)
            return;

        pass.SetMaterial(settings.material);
        pass.SetTarget(renderer.cameraColorTargetHandle);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
    }

    private sealed class CrayonPass : ScriptableRenderPass
    {
        private static readonly int TemporaryColorId = Shader.PropertyToID("_CrayonScreenFilterColor");

        private Material material;
        private RTHandle source;
        private RTHandle temporaryColor;

        public CrayonPass(Material material)
        {
            this.material = material;
        }

        public void SetMaterial(Material value)
        {
            material = value;
        }

        public void SetTarget(RTHandle value)
        {
            source = value;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(ref temporaryColor, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_CrayonScreenFilterColor");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || source == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("Crayon Screen Filter");
            using (new ProfilingScope(cmd, new ProfilingSampler("Crayon Screen Filter")))
            {
                Blitter.BlitCameraTexture(cmd, source, temporaryColor);
                Blitter.BlitCameraTexture(cmd, temporaryColor, source, material, 0);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            temporaryColor?.Release();
        }
    }
}
