using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class ScreenShattering : ScriptableRendererFeature
{
    [SerializeField] private Material material;
    [SerializeField] private DitheringData settings;

    RenderPass pass;

    class RenderPass : ScriptableRenderPass
    {
        public Material Material;
        public DitheringData Settings;
        
        private class PassData
        {
            public Material Material;
            public TextureHandle Source;
            public TextureHandle Temp;
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
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();

            // Не трогаем backbuffer напрямую
            if (resourceData.isActiveTargetBackBuffer)
                return;

            UpdateMaterial();

            var source = resourceData.activeColorTexture;
            var desc = renderGraph.GetTextureDesc(source);
            desc.name = "ScreenShattering_Temp";
            desc.clearBuffer = false;

            TextureHandle temp = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddUnsafePass<PassData>("ScreenShattering", out var passData))
            {
                passData.Material = Material;
                passData.Source   = source;
                passData.Temp     = temp;

                builder.UseTexture(source, AccessFlags.ReadWrite);
                builder.UseTexture(temp,   AccessFlags.ReadWrite);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                    
                    Blitter.BlitCameraTexture(cmd, data.Source, data.Temp);
                    
                    Blitter.BlitCameraTexture(cmd, data.Temp, data.Source, data.Material, 0);
                });
            }
        }

        // // ── Старый API (оставляем для Compatibility Mode) ─────────────────
        // [System.Obsolete("Compatible with Compatibility Mode only", false)]
        // public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        // {
        //     UpdateMaterial();
        //     var cmd = CommandBufferPool.Get("Screen Shattering");
        //     var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
        //
        //     cmd.GetTemporaryRT(Shader.PropertyToID("_Temp"), renderingData.cameraData.cameraTargetDescriptor);
        //     cmd.Blit(source, Shader.PropertyToID("_Temp"));
        //     cmd.Blit(Shader.PropertyToID("_Temp"), source, Material);
        //
        //     context.ExecuteCommandBuffer(cmd);
        //     CommandBufferPool.Release(cmd);
        // }
    }

    public override void Create()
    {
        pass = new RenderPass
        {
            Material = material,
            renderPassEvent =
                RenderPassEvent.BeforeRenderingPostProcessing,
            Settings = settings
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (!data.postProcessingEnabled) return;
        renderer.EnqueuePass(pass);
    }
}