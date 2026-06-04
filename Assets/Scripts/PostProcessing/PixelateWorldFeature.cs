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
        [SerializeField]
        public float perspectiveReferenceDistance = 10f;
        public Vector2Int resolution = new Vector2Int(320, 180);
        public int PixelsPerUnit = 32;
    }
    
    class RenderPass : ScriptableRenderPass
    {
        public RenderTargetIdentifier Source;
        public RTHandle Temp;
        private RenderTextureDescriptor PixelateWorldFeatureDescriptor;
        public Settings Settings;
        

        private static readonly int PixelSizeId   = Shader.PropertyToID("_PPU");
        public RenderPass()
        {
            PixelateWorldFeatureDescriptor =
                new RenderTextureDescriptor(
                    1,
                    1,
                    RenderTextureFormat.Default,
                    0);
        }

        public override void Configure(
            CommandBuffer cmd,
            RenderTextureDescriptor cameraTextureDescriptor)
        {

            RenderingUtils.ReAllocateHandleIfNeeded(
                ref Temp,
                PixelateWorldFeatureDescriptor);
        }
        private void CalculateCameraSize(
            ref RenderingData renderingData)
        {
            var cam =
                renderingData.cameraData.camera;

            int targetHeight;

            if (cam.orthographic)
            {
                float orthoHeight =
                    cam.orthographicSize * 2f;

                targetHeight =
                    Mathf.RoundToInt(
                        orthoHeight *
                        Settings.PixelsPerUnit);
            }
            else
            {
                float frustumHeight =
                    2f *
                    Settings.perspectiveReferenceDistance *
                    Mathf.Tan(
                        cam.fieldOfView *
                        0.5f *
                        Mathf.Deg2Rad);

                targetHeight =
                    Mathf.RoundToInt(
                        frustumHeight *
                        Settings.PixelsPerUnit);
            }

            float aspect =
                (float)Screen.width /
                Screen.height;

            int targetWidth =
                Mathf.RoundToInt(
                    targetHeight *
                    aspect);

            // GPU limit
            int maxTextureSize =
                SystemInfo.maxTextureSize;

            // Clamp preserving aspect ratio
            if (targetWidth > maxTextureSize ||
                targetHeight > maxTextureSize)
            {
                float scale =
                    Mathf.Min(
                        (float)maxTextureSize / targetWidth,
                        (float)maxTextureSize / targetHeight);

                targetWidth =
                    Mathf.Max(
                        1,
                        Mathf.RoundToInt(
                            targetWidth * scale));

                targetHeight =
                    Mathf.Max(
                        1,
                        Mathf.RoundToInt(
                            targetHeight * scale));
            }

            PixelateWorldFeatureDescriptor.width =
                targetWidth;

            PixelateWorldFeatureDescriptor.height =
                targetHeight;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // CommandBuffer cmd = CommandBufferPool.Get("Pixelate");
            //
            // //CalculateCameraSize(ref renderingData);
            //
            // RenderingUtils.ReAllocateHandleIfNeeded(ref Temp, PixelateWorldFeatureDescriptor); 
            //
            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            // Blitter.BlitCameraTexture(cmd, source, Temp);
            // Blitter.BlitCameraTexture(cmd, Temp, source);
            //
            // context.ExecuteCommandBuffer(cmd);
            // CommandBufferPool.Release(cmd);
            
            Debug.Log(source.name);
        }
    }

    public override void Create()
    {
        pass = new RenderPass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
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
