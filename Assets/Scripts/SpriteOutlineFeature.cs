using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 1) Рисует отфильтрованные спрайты камерой (те же матрицы, что и у основной картинки)
///    в прозрачную RT размером с экран -> позиция/масштаб/поворот/PPU совпадают автоматически.
/// 2) Fullscreen-шейдер находит границу по альфе этой RT и накладывает контур на кадр.
/// Можно добавить несколько экземпляров фичера на один Renderer с разными фильтрами.
/// </summary>
public class SpriteOutlineFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;

        [Header("Filters")]
        public LayerMask layerMask = ~0;

        [Tooltip("Битовая маска Rendering Layers (Renderer.renderingLayerMask). -1 = все.")]
        public int renderingLayerMask = -1;

        public bool useSortingLayerRange;
        public string minSortingLayer = "Default";
        public string maxSortingLayer = "Default";

        [Header("Outline")]
        public Color color = Color.red;
        [Min(0f)] public float thicknessPixels = 1f;   // теперь: в пикселях спрайта (texels)
        [Min(1f)] public float pixelsPerUnit = 16f;    // PPU твоих спрайтов
        public bool scaleWithCamera = true;            // false = старое поведение, константа в экранных px
        
        [Header("Occlusion")]
        public bool useOcclusion = true;
        public string outlinedSortingLayer = "Default"; // слой, где лежат подсвечиваемые объекты
    }
    

    [SerializeField] Shader shader; // Hidden/SpriteOutlineComposite
    [SerializeField] Settings settings = new Settings();

    Material material;
    OutlinePass pass;

    public override void Create()
    {
        if (shader == null) shader = Shader.Find("Hidden/SpriteOutlineComposite");
        if (shader == null) { pass = null; return; }

        CoreUtils.Destroy(material);
        material = CoreUtils.CreateEngineMaterial(shader); // свой материал на каждый фичер
        material.SetColor("_OutlineColor", settings.color);
        material.SetFloat("_Thickness", settings.thicknessPixels);

        pass = new OutlinePass
        {
            renderPassEvent = settings.passEvent,
            settings = settings,
            material = material
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null) return;
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
    }

    class OutlinePass : ScriptableRenderPass
    {
        public Settings settings;
        public Material material;

        static readonly List<ShaderTagId> ShaderTags = new List<ShaderTagId>
        {
            new ShaderTagId("Universal2D"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("SRPDefaultUnlit"),
        };

        class SilhouetteData { public RendererListHandle rendererList; }
        class CompositeData { public TextureHandle source; public TextureHandle occluder; public Material material; public bool useOcc; }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData  = frameData.Get<UniversalResourceData>();
            var cameraData    = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData     = frameData.Get<UniversalLightData>();

            if (cameraData.cameraType == CameraType.Preview ||
                cameraData.cameraType == CameraType.Reflection)
                return;

            // ── Прозрачная RT для силуэта ────────────────────────────────
            var desc = cameraData.cameraTargetDescriptor;
            var texDesc = new TextureDesc(desc.width, desc.height)
            {
                name = "_SpriteOutlineSilhouette",
                colorFormat = GraphicsFormat.R8G8B8A8_UNorm,
                clearBuffer = true,
                clearColor = Color.clear,   // именно прозрачный, а не Color.black
                filterMode = FilterMode.Point,
                msaaSamples = MSAASamples.None,
            };
            TextureHandle silhouette = renderGraph.CreateTexture(texDesc);

            // ── Фильтры ──────────────────────────────────────────────────
            var drawing = RenderingUtils.CreateDrawingSettings(
                ShaderTags, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);

            
            TextureHandle occluder = TextureHandle.nullHandle;
            if (settings.useOcclusion)
            {
                texDesc.name = "_SpriteOutlineOccluder";
                occluder = renderGraph.CreateTexture(texDesc);

                short lo = (short)(SortingLayer.GetLayerValueFromName(settings.outlinedSortingLayer) + 1);
                var occFiltering = new FilteringSettings(RenderQueueRange.transparent, ~0, uint.MaxValue)
                {
                    sortingLayerRange = new SortingLayerRange(lo, short.MaxValue)
                };
                var occParams = new RendererListParams(renderingData.cullResults, drawing, occFiltering);

                using (var builder = renderGraph.AddRasterRenderPass<SilhouetteData>("SpriteOutline Occluders", out var data))
                {
                    data.rendererList = renderGraph.CreateRendererList(occParams);
                    builder.UseRendererList(data.rendererList);
                    builder.SetRenderAttachment(occluder, 0);
                    builder.SetRenderFunc(static (SilhouetteData d, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.DrawRendererList(d.rendererList);
                    });
                }
            }
            
            var filtering = new FilteringSettings(
                RenderQueueRange.transparent,
                settings.layerMask,
                (uint)settings.renderingLayerMask);

            if (settings.useSortingLayerRange)
            {
                short lo = (short)SortingLayer.GetLayerValueFromName(settings.minSortingLayer);
                short hi = (short)SortingLayer.GetLayerValueFromName(settings.maxSortingLayer);
                filtering.sortingLayerRange = new SortingLayerRange(lo, hi);
            }

            var listParams = new RendererListParams(renderingData.cullResults, drawing, filtering);
            
            float thickness = settings.thicknessPixels;

            if (settings.scaleWithCamera && cameraData.camera.orthographic)
            {
                float screenPxPerUnit = desc.height / (2f * cameraData.camera.orthographicSize);
                thickness = settings.thicknessPixels / settings.pixelsPerUnit * screenPxPerUnit;
                
                thickness = Mathf.Max(1f, Mathf.Round(thickness));
            }

            material.SetColor("_OutlineColor", settings.color);
            material.SetFloat("_Thickness", thickness);
            
            // ── Pass 1: рисуем спрайты в силуэт ──────────────────────────
            using (var builder = renderGraph.AddRasterRenderPass<SilhouetteData>("SpriteOutline Silhouette", out var data))
            {
                data.rendererList = renderGraph.CreateRendererList(listParams);
                builder.UseRendererList(data.rendererList);
                builder.SetRenderAttachment(silhouette, 0);

                builder.SetRenderFunc(static (SilhouetteData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawRendererList(d.rendererList);
                });
            }

            // ── Pass 2: контур поверх кадра (blend делает шейдер) ────────
            using (var builder = renderGraph.AddRasterRenderPass<CompositeData>("SpriteOutline Composite", out var data))
            {
                data.source = silhouette;
                data.material = material;
                data.useOcc = settings.useOcclusion;
                builder.UseTexture(silhouette);
                if (data.useOcc) { data.occluder = occluder; builder.UseTexture(occluder); }
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);

                builder.SetRenderFunc(static (CompositeData d, RasterGraphContext ctx) =>
                {
                    d.material.SetFloat("_UseOcclusion", d.useOcc ? 1f : 0f);
                    if (d.useOcc) d.material.SetTexture("_OccluderTex", d.occluder);
                    Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, 0);
                });
            }
        }
    }
}
