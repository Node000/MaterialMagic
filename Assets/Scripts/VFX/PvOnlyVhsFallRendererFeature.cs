using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PvOnlyVhsFallRendererFeature : ScriptableRendererFeature
{
    public static Material RuntimeMaterial { get; set; }

    [System.Serializable]
    public class Settings
    {
        public Material material;
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingTransparents;
    }

    [SerializeField] private Settings settings = new Settings();

    private VhsPass pass;

    public override void Create()
    {
        pass = new VhsPass(settings.material)
        {
            renderPassEvent = settings.injectionPoint
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Material material = RuntimeMaterial != null ? RuntimeMaterial : settings.material;
        if (material == null || renderingData.cameraData.cameraType != CameraType.Game ||
            !material.HasProperty("_Intensity") || material.GetFloat("_Intensity") <= 0f)
            return;

        pass.SetMaterial(material);
        renderer.EnqueuePass(pass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        pass.SetTarget(renderer.cameraColorTargetHandle);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
    }

    private sealed class VhsPass : ScriptableRenderPass
    {
        private Material material;
        private RTHandle source;
        private RTHandle temporaryColor;

        public VhsPass(Material material)
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
            RenderingUtils.ReAllocateIfNeeded(ref temporaryColor, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_PvOnlyVhsFallColor");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || source == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("PvOnly VHS Fall");
            using (new ProfilingScope(cmd, new ProfilingSampler("PvOnly VHS Fall")))
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
