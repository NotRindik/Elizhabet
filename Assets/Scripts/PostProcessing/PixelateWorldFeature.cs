using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public class PixelateWorldFeature  : ScriptableRendererFeature
{
    [SerializeField] private Material material;
    RenderPass pass;
    [SerializeField] private Settings settings;
    
    
    [System.Serializable]
    public class Settings
    {
        [Tooltip("ppu = 32")]
        public float ppu = 0.0625f;
    }
    
    class RenderPass : ScriptableRenderPass
    {
        public Material Material;
        public RenderTargetIdentifier Source;
        public RTHandle Temp;
        private RenderTextureDescriptor PixelateWorldFeatureDescriptor;
        public Settings Settings;

        CommandBuffer cmd;

        private static readonly int PixelSizeId   = Shader.PropertyToID("_PPU");
        public RenderPass()
        {
            PixelateWorldFeatureDescriptor = new RenderTextureDescriptor(Screen.width,
                Screen.height, RenderTextureFormat.Default, 0);

            Temp = RTHandles.Alloc(PixelateWorldFeatureDescriptor);
            cmd = CommandBufferPool.Get("PixelateWorldFeature");
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            PixelateWorldFeatureDescriptor.width = cameraTextureDescriptor.width;
            PixelateWorldFeatureDescriptor.height = cameraTextureDescriptor.height;

            RenderingUtils.ReAllocateHandleIfNeeded(ref Temp, PixelateWorldFeatureDescriptor);
        }
        void UpdateMaterial( )
        {
            if (Material == null)
            {
                Debug.Log("No Material");
                return;
            }
            Material.SetFloat(PixelSizeId, Settings.ppu);
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
