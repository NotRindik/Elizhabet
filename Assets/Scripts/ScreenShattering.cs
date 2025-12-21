using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ScreenShattering : ScriptableRendererFeature
{
    [SerializeField] private Material material;
    RenderPass pass;

    class RenderPass : ScriptableRenderPass
    {
        Material material;
        RTHandle source;
        RTHandle temp;

        public RenderPass(Material mat)
        {
            material = mat;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        public void Setup(RTHandle src)
        {
            source = src;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
        {
            var desc = data.cameraData.cameraTargetDescriptor;

            // RTHandle НЕ поддерживает это
            desc.msaaSamples = 1;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            desc.depthBufferBits = 0;

            RenderingUtils.ReAllocateHandleIfNeeded(
                ref temp,
                desc,
                FilterMode.Point,
                TextureWrapMode.Clamp,
                name: "_TempShatter"
            );

        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            if (source == null || temp == null || material == null)
                return;

            var cmd = CommandBufferPool.Get("Screen Shattering");

            Blitter.BlitCameraTexture(cmd, source, temp, material, 0);
            Blitter.BlitCameraTexture(cmd, temp, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }


        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // НЕ Release здесь — RTHandle живёт между кадрами
        }

        public void Dispose()
        {
            temp?.Release();
        }
    }

    public override void Create()
    {
        pass = new RenderPass(Instantiate(material));
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (!data.postProcessingEnabled || material == null)
            return;

        renderer.EnqueuePass(pass);
    }



    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
    }
}
