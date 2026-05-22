using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ScreenShattering : ScriptableRendererFeature
{
    [SerializeField] private Material material;
    RenderPass pass;
    [SerializeField] private DitheringData settings;
    class RenderPass : ScriptableRenderPass
    {
        public Material Material;
        public RenderTargetIdentifier Source;
        public RTHandle Temp;
        private RenderTextureDescriptor ScreenShatterinRendererDescriptor;
        public DitheringData Settings;
        public RenderPass()
        {
            ScreenShatterinRendererDescriptor = new RenderTextureDescriptor(Screen.width,
                Screen.height, RenderTextureFormat.Default, 0);

            Temp = RTHandles.Alloc(ScreenShatterinRendererDescriptor);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ScreenShatterinRendererDescriptor.width = cameraTextureDescriptor.width;
            ScreenShatterinRendererDescriptor.height = cameraTextureDescriptor.height;

            RenderingUtils.ReAllocateHandleIfNeeded(ref Temp, ScreenShatterinRendererDescriptor);
        }
        private void UpdateMaterial()
        {
            if (Material == null) return;

            Material.SetVector("_Params", new Vector4(
                Settings.ColorResMult,
                Settings.ColorResDiv,
                Settings.DithFactor,
                Settings.PixelPerUnit
            ));
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {

            var cmd = CommandBufferPool.Get("Screen Shattering");
            Source = renderingData.cameraData.renderer.cameraColorTargetHandle;
            UpdateMaterial();
            cmd.Blit(Source, Temp.nameID);
            cmd.SetRenderTarget(Source);
            cmd.ClearRenderTarget(true, true, default);
            cmd.Blit(Temp.nameID, Source, Material);

            context.ExecuteCommandBuffer(cmd);
            
            CommandBufferPool.Release(cmd);
        }
    }

    public override void Create()
    {
        pass = new RenderPass
        {
            Material = Instantiate(material),
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing,
            Settings = settings
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (!data.postProcessingEnabled)
            return;
        renderer.EnqueuePass(pass);
    }
}
