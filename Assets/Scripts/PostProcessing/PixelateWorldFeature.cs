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
        public Vector2Int resolution = new Vector2Int(320, 180);
        public int PixelsPerUnit = 32;
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

        public override void Configure(
            CommandBuffer cmd,
            RenderTextureDescriptor cameraTextureDescriptor)
        {

            RenderingUtils.ReAllocateHandleIfNeeded(
                ref Temp,
                PixelateWorldFeatureDescriptor);
        }
        private void CalculateCameraSize(ref RenderingData renderingData)
        {

            var cam = renderingData.cameraData.camera;

            float orthoSize = cam.orthographicSize;

            int targetHeight =
                Mathf.RoundToInt(orthoSize * 2f * Settings.PixelsPerUnit);

            float aspect =
                (float)Screen.width / Screen.height;

            int targetWidth =
                Mathf.RoundToInt(targetHeight * aspect);

            PixelateWorldFeatureDescriptor.width =
                targetWidth;

            PixelateWorldFeatureDescriptor.height =
                targetHeight;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Pixelate");

            CalculateCameraSize(ref renderingData);
            
            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            Blitter.BlitCameraTexture(cmd, source, Temp);
            Blitter.BlitCameraTexture(cmd, Temp, source);

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
