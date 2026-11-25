#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PixelLiveBlend
{
    const double RefreshInterval = 0.05;
    const double PollInterval = 0.25;

    static readonly int BackdropId = Shader.PropertyToID("_BackdropTex");
    static readonly int BackdropStId = Shader.PropertyToID("_BackdropST");
    static readonly int OnId = Shader.PropertyToID("_LiveBlendOn");
    static readonly int ModeId = Shader.PropertyToID("_LiveBlendMode");
    static readonly int PremulId = Shader.PropertyToID("_BackdropPremul");

    class Bound
    {
        public RenderTexture texture;
        public Vector4 st;
        public PixelBlendMode mode;
    }

    static readonly Dictionary<PixelLayer, Bound> bound = new Dictionary<PixelLayer, Bound>();
    static readonly HashSet<PixelCanvas> dirty = new HashSet<PixelCanvas>();
    static readonly Dictionary<PixelCanvas, int> signatures = new Dictionary<PixelCanvas, int>();
    static readonly List<PixelLayer> layers = new List<PixelLayer>();
    static readonly List<PixelBackdropGpu.Item> items = new List<PixelBackdropGpu.Item>();
    static List<PixelCanvas> canvases;
    static MaterialPropertyBlock block;
    static double lastRefresh;
    static double lastPoll;

    static readonly Dictionary<PixelCanvas, int> transformSignatures = new Dictionary<PixelCanvas, int>();
    static readonly List<PixelCanvas> boundCanvases = new List<PixelCanvas>();
    static readonly List<PixelLayer> sortedBuffer = new List<PixelLayer>();

    static PixelLiveBlend()
    {
        PixelLayer.AfterBind += Rebind;
        EditorApplication.update += Update;
        UnityEngine.Rendering.RenderPipelineManager.beginContextRendering += OnBeginRendering;
        EditorApplication.hierarchyChanged += () => canvases = null;
        Undo.undoRedoPerformed += MarkBoundDirty;
        AssemblyReloadEvents.beforeAssemblyReload += ReleaseAll;
        EditorApplication.playModeStateChanged += state =>
        {
            if(state == PlayModeStateChange.ExitingEditMode) ReleaseAll();
            if(state == PlayModeStateChange.EnteredEditMode) signatures.Clear();
        };
    }

    public static bool NeedsPreview(PixelBlendMode mode)
    {
        return PixelBackdropGpu.NeedsPreview(mode);
    }

    public static void MarkDirty(PixelCanvas canvas)
    {
        if(canvas != null) dirty.Add(canvas);
    }

    public static void MarkDirty(PixelCanvas canvas, RectInt rect)
    {
        if(canvas != null && HasBound(canvas)) dirty.Add(canvas);
    }

    static bool HasBound(PixelCanvas canvas)
    {
        foreach(var pair in bound)
            if(pair.Key != null && pair.Key.Root == canvas) return true;
        return false;
    }

    static void MarkBoundDirty()
    {
        foreach(var pair in bound)
            if(pair.Key != null) MarkDirty(pair.Key.Root);
    }

    static List<PixelCanvas> Canvases()
    {
        if(canvases == null) canvases = PixelCanvasSync.Canvases();
        return canvases;
    }

    static void Update()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        var now = EditorApplication.timeSinceStartup;
        if(now - lastPoll >= PollInterval)
        {
            lastPoll = now;
            Poll();
        }
        if(dirty.Count == 0 || now - lastRefresh < RefreshInterval) return;
        lastRefresh = now;
        var list = new List<PixelCanvas>(dirty);
        dirty.Clear();
        for(var i = 0; i < list.Count; i++)
            if(list[i] != null) Refresh(list[i]);
        Cleanup();
        SceneView.RepaintAll();
    }

    static void OnBeginRendering(UnityEngine.Rendering.ScriptableRenderContext context, List<Camera> cameras)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || bound.Count == 0) return;
        boundCanvases.Clear();
        foreach(var pair in bound)
        {
            if(pair.Key == null) continue;
            var root = pair.Key.Root;
            if(root != null && !boundCanvases.Contains(root)) boundCanvases.Add(root);
        }
        for(var i = 0; i < boundCanvases.Count; i++)
        {
            var canvas = boundCanvases[i];
            PixelBackdropGpu.Collect(canvas, sortedBuffer);
            var signature = PixelBackdropGpu.TransformSignature(canvas, sortedBuffer);
            int previous;
            if(transformSignatures.TryGetValue(canvas, out previous) && previous == signature) continue;
            transformSignatures[canvas] = signature;
            Refresh(canvas);
        }
    }

    static void Poll()
    {
        var list = Canvases();
        for(var i = 0; i < list.Count; i++)
        {
            var canvas = list[i];
            if(canvas == null) continue;
            var signature = Signature(canvas);
            int previous;
            if(signatures.TryGetValue(canvas, out previous) && previous == signature) continue;
            signatures[canvas] = signature;
            MarkDirty(canvas);
        }
    }

    static int Signature(PixelCanvas canvas)
    {
        unchecked
        {
            var hash = 17;
            var sorted = PixelCorrections.Sorted(canvas);
            var any = false;
            for(var i = 0; i < sorted.Count; i++)
            {
                var l = sorted[i];
                if(!l.IsCorrection && NeedsPreview(l.blendMode)) any = true;
                var sr = l.Surface(false);
                var sprite = sr != null ? sr.sprite : null;
                var lut = l.CorrectionLut;
                hash = hash * 31 + l.GetInstanceID();
                hash = hash * 31 + (l.visible && l.gameObject.activeInHierarchy ? 1 : 0);
                hash = hash * 31 + l.opacity.GetHashCode();
                hash = hash * 31 + (int)l.blendMode;
                hash = hash * 31 + l.sortIndex;
                hash = hash * 31 + (l.clip ? 1 : 0);
                hash = hash * 31 + l.origin.GetHashCode();
                hash = hash * 31 + l.frame;
                hash = hash * 31 + (sprite != null ? sprite.GetInstanceID() : 0);
                hash = hash * 31 + (lut != null ? lut.GetInstanceID() : 0);
                if(l.IsCorrection) hash = hash * 31 + l.correction.Hash();
            }
            return any ? hash : 0;
        }
    }

    public static void Refresh(PixelCanvas canvas)
    {
        if(canvas == null) return;
        PixelBackdropGpu.Collect(canvas, layers);
        PixelBackdropGpu.BuildItems(layers, items);
        var live = new HashSet<PixelLayer>();
        var needed = false;
        for(var i = 1; i < items.Count; i++)
            if(NeedsPreview(items[i].layer.blendMode)) needed = true;

        if(needed)
        {
            for(var i = 0; i < items.Count; i++)
            {
                Vector2Int origin;
                int w, h;
                PixelCanvasEditing.ReadPixels(items[i].layer, out origin, out w, out h);
            }

            for(var i = 1; i < items.Count; i++)
            {
                var layer = items[i].layer;
                if(!NeedsPreview(layer.blendMode)) continue;
                var sr = layer.Surface(false);
                if(sr == null || sr.sprite == null || !PixelDrawerAssets.IsLayerMaterial(sr.sharedMaterial)) continue;
                Bound state;
                if(!bound.TryGetValue(layer, out state))
                {
                    state = new Bound();
                    bound[layer] = state;
                }
                if(!PixelBackdropGpu.RenderInto(items, i, ref state.texture)) continue;
                var rect = sr.sprite.textureRect;
                var tex = sr.sprite.texture;
                state.st = new Vector4(tex.width / rect.width, tex.height / rect.height, -rect.x / rect.width, -rect.y / rect.height);
                state.mode = layer.blendMode;
                live.Add(layer);
                Bind(sr, state);
            }
        }

        var stale = new List<PixelLayer>();
        foreach(var pair in bound)
            if(pair.Key != null && pair.Key.Root == canvas && !live.Contains(pair.Key)) stale.Add(pair.Key);
        for(var i = 0; i < stale.Count; i++) Release(stale[i]);
    }

    static void Bind(SpriteRenderer sr, Bound state)
    {
        if(block == null) block = new MaterialPropertyBlock();
        sr.GetPropertyBlock(block);
        block.SetTexture(BackdropId, state.texture);
        block.SetVector(BackdropStId, state.st);
        block.SetFloat(ModeId, (int)state.mode);
        block.SetFloat(OnId, 1f);
        block.SetFloat(PremulId, 1f);
        sr.SetPropertyBlock(block);
    }

    static void Rebind(PixelLayer layer)
    {
        if(layer == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Bound state;
        if(bound.TryGetValue(layer, out state) && state.texture != null)
        {
            var sr = layer.Surface(false);
            if(sr != null) Bind(sr, state);
            return;
        }
        if(NeedsPreview(layer.blendMode)) MarkDirty(layer.Root);
    }

    static void Release(PixelLayer layer)
    {
        Bound state;
        if(!bound.TryGetValue(layer, out state)) return;
        bound.Remove(layer);
        if(layer != null)
        {
            var sr = layer.Surface(false);
            if(sr != null && sr.HasPropertyBlock())
            {
                if(block == null) block = new MaterialPropertyBlock();
                sr.GetPropertyBlock(block);
                block.SetFloat(OnId, 0f);
                sr.SetPropertyBlock(block);
            }
        }
        if(state.texture != null)
        {
            state.texture.Release();
            Object.DestroyImmediate(state.texture);
        }
    }

    static void Cleanup()
    {
        var stale = new List<PixelLayer>();
        foreach(var pair in bound)
            if(pair.Key == null) stale.Add(pair.Key);
        for(var i = 0; i < stale.Count; i++) Release(stale[i]);
        var gone = new List<PixelCanvas>();
        foreach(var pair in signatures)
            if(pair.Key == null) gone.Add(pair.Key);
        for(var i = 0; i < gone.Count; i++) signatures.Remove(gone[i]);
    }

    static void ReleaseAll()
    {
        var list = new List<PixelLayer>(bound.Keys);
        for(var i = 0; i < list.Count; i++) Release(list[i]);
        bound.Clear();
        signatures.Clear();
        transformSignatures.Clear();
        dirty.Clear();
    }
}
#endif
