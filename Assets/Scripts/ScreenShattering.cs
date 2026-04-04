using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Unity.VisualScripting.Member;

public class ScreenShattering : ScriptableRendererFeature
{
    [SerializeField] private Material material;
    RenderPass pass;
    [SerializeField] private DitheringData settings;

    [Serializable]
    public class DitheringData
    {
        public float ColorResMult = 4;
        public float ColorResDiv = 0.25f;
        public float DithFactor = 0.0900000036f;
        public float PixelPerUnit = 32;
    }
    class RenderPass : ScriptableRenderPass
    {
        public Material Material;
        public RenderTargetIdentifier Source;
        public RTHandle Temp;
        private int id;
        private RenderTextureDescriptor ScreenShatterinRendererDescriptor;
        public DitheringData Settings;

        CommandBuffer cmd;

        public RenderPass()
        {
            ScreenShatterinRendererDescriptor = new RenderTextureDescriptor(Screen.width,
                Screen.height, RenderTextureFormat.Default, 0);

            Temp = RTHandles.Alloc(ScreenShatterinRendererDescriptor);
            cmd = CommandBufferPool.Get("Screen Shattering");
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ScreenShatterinRendererDescriptor.width = cameraTextureDescriptor.width;
            ScreenShatterinRendererDescriptor.height = cameraTextureDescriptor.height;

            RenderingUtils.ReAllocateHandleIfNeeded(ref Temp, ScreenShatterinRendererDescriptor);
            id = Shader.PropertyToID(Temp.name);
            cmd.GetTemporaryRT(id, cameraTextureDescriptor, FilterMode.Point);
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

            cmd.Clear();
            Source = renderingData.cameraData.renderer.cameraColorTargetHandle;
            UpdateMaterial();
            cmd.Blit(Source, Temp.nameID);
            cmd.SetRenderTarget(Source);
            cmd.ClearRenderTarget(true, true, default);
            cmd.Blit(Temp.nameID, Source, Material);

            context.ExecuteCommandBuffer(cmd);
        }



        public override void FrameCleanup(CommandBuffer cmd){
            
            cmd.ReleaseTemporaryRT(id);
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
