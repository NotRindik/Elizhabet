#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PixelLiveBlend
{
    const double RefreshInterval = 0.1;
    const double PollInterval = 0.25;

    static readonly int BackdropId = Shader.PropertyToID("_BackdropTex");
    static readonly int BackdropStId = Shader.PropertyToID("_BackdropST");
    static readonly int OnId = Shader.PropertyToID("_LiveBlendOn");
    static readonly int ModeId = Shader.PropertyToID("_LiveBlendMode");

    class Bound
    {
        public Texture2D texture;
        public Vector4 st;
        public PixelBlendMode mode;
        public RectInt rect;
    }

    static readonly Dictionary<PixelLayer, Bound> bound = new Dictionary<PixelLayer, Bound>();
    static readonly HashSet<PixelCanvas> dirtyFull = new HashSet<PixelCanvas>();
    static readonly Dictionary<PixelCanvas, RectInt> dirtyRects = new Dictionary<PixelCanvas, RectInt>();
    static readonly Dictionary<PixelCanvas, int> signatures = new Dictionary<PixelCanvas, int>();
    static List<PixelCanvas> canvases;
    static MaterialPropertyBlock block;
    static double lastRefresh;
    static double lastPoll;

    static PixelLiveBlend()
    {
        PixelLayer.AfterBind += Rebind;
        EditorApplication.update += Update;
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
        return !PixelBlend.IsLive(mode);
    }

    public static void MarkDirty(PixelCanvas canvas)
    {
        if(canvas == null) return;
        dirtyFull.Add(canvas);
        dirtyRects.Remove(canvas);
    }

    public static void MarkDirty(PixelCanvas canvas, RectInt rect)
    {
        if(canvas == null || rect.width <= 0 || rect.height <= 0 || dirtyFull.Contains(canvas) || !HasBound(canvas)) return;
        RectInt current;
        if(dirtyRects.TryGetValue(canvas, out current))
        {
            var x0 = Mathf.Min(current.xMin, rect.xMin);
            var y0 = Mathf.Min(current.yMin, rect.yMin);
            var x1 = Mathf.Max(current.xMax, rect.xMax);
            var y1 = Mathf.Max(current.yMax, rect.yMax);
            rect = new RectInt(x0, y0, x1 - x0, y1 - y0);
        }
        dirtyRects[canvas] = rect;
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
        if((dirtyFull.Count == 0 && dirtyRects.Count == 0) || now - lastRefresh < RefreshInterval) return;
        lastRefresh = now;

        var full = new List<PixelCanvas>(dirtyFull);
        var partial = new List<KeyValuePair<PixelCanvas, RectInt>>(dirtyRects);
        dirtyFull.Clear();
        dirtyRects.Clear();
        for(var i = 0; i < full.Count; i++)
            if(full[i] != null) Refresh(full[i]);
        for(var i = 0; i < partial.Count; i++)
            if(partial[i].Key != null) RefreshRect(partial[i].Key, partial[i].Value);
        Cleanup();
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
            var layers = PixelCorrections.Sorted(canvas);
            var any = false;
            for(var i = 0; i < layers.Count; i++)
            {
                var l = layers[i];
                if(!l.IsCorrection && NeedsPreview(l.blendMode)) any = true;
                hash = hash * 31 + l.GetInstanceID();
                hash = hash * 31 + (l.visible && l.gameObject.activeInHierarchy ? 1 : 0);
                hash = hash * 31 + l.opacity.GetHashCode();
                hash = hash * 31 + (int)l.blendMode;
                hash = hash * 31 + l.sortIndex;
                hash = hash * 31 + (l.clip ? 1 : 0);
                hash = hash * 31 + l.origin.GetHashCode();
                hash = hash * 31 + l.frame;
                if(l.IsCorrection) hash = hash * 31 + l.correction.Hash();
            }
            return any ? hash : 0;
        }
    }

    public static void Refresh(PixelCanvas canvas)
    {
        if(canvas == null) return;
        var items = PixelCompositor.ForCanvas(canvas, true);
        var live = new HashSet<PixelLayer>();
        for(var i = 1; i < items.Count; i++)
        {
            if(!NeedsPreview(items[i].blend)) continue;
            var layer = items[i].layer;
            Vector2Int origin;
            int w, h;
            var pixels = PixelCanvasEditing.ReadPixels(layer, out origin, out w, out h);
            if(pixels == null || w <= 0 || h <= 0) continue;
            var sr = layer.Surface(false);
            if(sr == null || sr.sprite == null || !PixelDrawerAssets.IsLayerMaterial(sr.sharedMaterial)) continue;
            var rect = sr.sprite.rect;
            if((int)rect.width != w || (int)rect.height != h) continue;
            var area = new RectInt(origin.x, origin.y, w, h);
            var backdrop = PixelCompositor.Composite(items.GetRange(0, i), area);
            var state = Ensure(layer, w, h);
            state.texture.SetPixels32(backdrop);
            state.texture.Apply(false);
            var tex = sr.sprite.texture;
            state.st = new Vector4(tex.width / rect.width, tex.height / rect.height, -rect.x / rect.width, -rect.y / rect.height);
            state.mode = items[i].blend;
            state.rect = area;
            live.Add(layer);
            Bind(sr, state);
        }
        var stale = new List<PixelLayer>();
        foreach(var pair in bound)
            if(pair.Key != null && pair.Key.Root == canvas && !live.Contains(pair.Key)) stale.Add(pair.Key);
        for(var i = 0; i < stale.Count; i++) Release(stale[i]);
    }

    static void RefreshRect(PixelCanvas canvas, RectInt dirtyRect)
    {
        var items = PixelCompositor.ForCanvas(canvas, true);
        for(var i = 1; i < items.Count; i++)
        {
            Bound state;
            var layer = items[i].layer;
            if(!bound.TryGetValue(layer, out state) || state.texture == null) continue;
            Vector2Int origin;
            int w, h;
            if(PixelCanvasEditing.ReadPixels(layer, out origin, out w, out h) == null || origin != state.rect.position || w != state.rect.width || h != state.rect.height)
            {
                MarkDirty(canvas);
                return;
            }
            var x0 = Mathf.Max(dirtyRect.xMin, state.rect.xMin);
            var y0 = Mathf.Max(dirtyRect.yMin, state.rect.yMin);
            var x1 = Mathf.Min(dirtyRect.xMax, state.rect.xMax);
            var y1 = Mathf.Min(dirtyRect.yMax, state.rect.yMax);
            if(x1 <= x0 || y1 <= y0) continue;
            var area = new RectInt(x0, y0, x1 - x0, y1 - y0);
            var backdrop = PixelCompositor.Composite(items.GetRange(0, i), area);
            state.texture.SetPixels32(x0 - state.rect.xMin, y0 - state.rect.yMin, area.width, area.height, backdrop);
            state.texture.Apply(false);
        }
    }

    static Bound Ensure(PixelLayer layer, int w, int h)
    {
        Bound state;
        if(!bound.TryGetValue(layer, out state))
        {
            state = new Bound();
            bound[layer] = state;
        }
        if(state.texture == null || state.texture.width != w || state.texture.height != h)
        {
            if(state.texture != null) Object.DestroyImmediate(state.texture);
            state.texture = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            state.texture.hideFlags = HideFlags.HideAndDontSave;
            state.texture.filterMode = FilterMode.Point;
            state.texture.wrapMode = TextureWrapMode.Clamp;
        }
        return state;
    }

    static void Bind(SpriteRenderer sr, Bound state)
    {
        if(block == null) block = new MaterialPropertyBlock();
        sr.GetPropertyBlock(block);
        block.SetTexture(BackdropId, state.texture);
        block.SetVector(BackdropStId, state.st);
        block.SetFloat(ModeId, (int)state.mode);
        block.SetFloat(OnId, 1f);
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
        if(state.texture != null) Object.DestroyImmediate(state.texture);
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
        var layers = new List<PixelLayer>(bound.Keys);
        for(var i = 0; i < layers.Count; i++) Release(layers[i]);
        bound.Clear();
        signatures.Clear();
        dirtyFull.Clear();
        dirtyRects.Clear();
    }
}
#endif
