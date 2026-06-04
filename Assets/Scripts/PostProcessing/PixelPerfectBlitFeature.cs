// using UnityEngine;
// using UnityEngine.Rendering;
// using UnityEngine.Rendering.Universal;
// using UnityEngine.Rendering.RenderGraphModule;
// using UnityEngine.Rendering.RenderGraphModule.Util;
//
// public class PixelPerfectBlitFeature : ScriptableRendererFeature
// {
//     class BlitPass : ScriptableRenderPass
//     {
//         private RTHandle rtHandle;
//
//         public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
//         {
//             
//             var tex = PixelPerfectRenderer.LowResTexture;
//             if (tex == null || !tex.IsCreated()) return;
//
//             var resourceData = frameData.Get<UniversalResourceData>();
//
//             // переиспользуем RTHandle
//             if (rtHandle == null || rtHandle.rt != tex)
//             {
//                 rtHandle?.Release();
//                 rtHandle = RTHandles.Alloc(tex);
//             }
//
//             var sourceHandle = renderGraph.ImportTexture(rtHandle);
//             var destHandle = resourceData.backBufferColor;
//
//             // AddBlitPass без материала — ClampNearest = Point фильтр
//             renderGraph.AddBlitPass(
//                 sourceHandle,
//                 destHandle,
//                 Vector2.one,
//                 Vector2.zero,
//                 filterMode: RenderGraphUtils.BlitFilterMode.ClampNearest,
//                 passName: "PixelPerfectBlit");
//         }
//
//         public void Dispose()
//         {
//             rtHandle?.Release();
//             rtHandle = null;
//         }
//     }
//
//     BlitPass pass;
//
//     public override void Create()
//     {
//         pass = new BlitPass
//         {
//             renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
//         };
//     }
//
//     public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
//     {
//         renderer.EnqueuePass(pass);
//     }
//
//     protected override void Dispose(bool disposing)
//     {
//         pass?.Dispose();
//     }
// }