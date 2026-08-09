using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class CrtScanLineRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingTransparents;
    }

    [SerializeField] private Settings settings = new Settings();
    private CrtScanLinePass pass;

    public override void Create()
    {
        pass = new CrtScanLinePass(settings.material)
        {
            renderPassEvent = settings.injectionPoint
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material == null || renderingData.cameraData.cameraType != CameraType.Game ||
            !settings.material.HasProperty("_Intensity") || settings.material.GetFloat("_Intensity") <= 0.001f)
            return;

        pass.SetMaterial(settings.material);
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

    private sealed class CrtScanLinePass : ScriptableRenderPass
    {
        private Material material;
        private RTHandle source;
        private RTHandle temporaryColor;

        public CrtScanLinePass(Material material)
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
            RenderingUtils.ReAllocateIfNeeded(ref temporaryColor, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_CrtScanLineColor");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || source == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("CRT Scan Line");
            using (new ProfilingScope(cmd, new ProfilingSampler("CRT Scan Line")))
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
