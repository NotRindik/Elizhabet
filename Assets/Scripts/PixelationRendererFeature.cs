using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PixelationRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Material material;
    RenderPass pass;

    [SerializeField] private PixelationSettings settings;

    [Serializable]
    public class PixelationSettings
    {
        public float PixelPerUnit = 32f; // главный параметр
        public float Aspect = 1f;
    }

    class RenderPass : ScriptableRenderPass
    {
        public Material Material;
        public PixelationSettings Settings;

        public RenderTargetIdentifier Source;
        public RTHandle Temp;

        private RenderTextureDescriptor descriptor;
        CommandBuffer cmd;

        public RenderPass()
        {
            descriptor = new RenderTextureDescriptor(Screen.width, Screen.height,
                RenderTextureFormat.Default, 0);

            Temp = RTHandles.Alloc(descriptor);
            cmd = CommandBufferPool.Get("Pixelation Pass");
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            descriptor = cameraTextureDescriptor;

            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;

            RenderingUtils.ReAllocateHandleIfNeeded(ref Temp, descriptor);

            Temp.rt.filterMode = FilterMode.Point;
        }

        void UpdateMaterial(ref RenderingData renderingData)
        {
            if (Material == null) return;

            var cam = renderingData.cameraData.camera;

            float screenHeight = cam.pixelHeight;

            float unitsToPixels;

            if (cam.orthographic)
            {
                float worldHeight = cam.orthographicSize * 2f;
                unitsToPixels = screenHeight / worldHeight;
            }
            else
            {
                float distance = 10f; // можно потом улучшить через depth
                float fovRad = cam.fieldOfView * Mathf.Deg2Rad;

                float worldHeight = 2f * distance * Mathf.Tan(fovRad * 0.5f);
                unitsToPixels = screenHeight / worldHeight;
            }

            // 🔥 ВАЖНО: теперь это РАЗМЕР ПИКСЕЛЯ В ЭКРАННЫХ ПИКСЕЛЯХ
            float pixelSize = Mathf.Max(1f, unitsToPixels / Settings.PixelPerUnit);

            Material.SetVector("_PixelParams", new Vector4(
                pixelSize,
                Settings.Aspect,
                cam.pixelWidth,
                cam.pixelHeight
            ));
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            //cmd.Clear();

            //Source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            /*UpdateMaterial(ref renderingData);

            cmd.Blit(Source, Temp.nameID);
            cmd.Blit(Temp.nameID, Source, Material);*/

            //context.ExecuteCommandBuffer(cmd);
        }
    }

    public override void Create()
    {
        pass = new RenderPass
        {
            Material = Instantiate(material),
            Settings = settings,
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (!data.postProcessingEnabled)
            return;

        renderer.EnqueuePass(pass);
    }
}
