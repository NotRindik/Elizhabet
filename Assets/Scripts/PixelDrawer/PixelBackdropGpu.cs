using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class PixelBackdropGpu
{
    const float RecollectInterval = 1f;

    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int SrcTexId = Shader.PropertyToID("_SrcTex");
    static readonly int SrcSizeId = Shader.PropertyToID("_SrcSize");
    static readonly int SrcUvId = Shader.PropertyToID("_SrcUV");
    static readonly int DstToSrcId = Shader.PropertyToID("_DstToSrc");
    static readonly int ClipTexId = Shader.PropertyToID("_ClipTex");
    static readonly int ClipSizeId = Shader.PropertyToID("_ClipSize");
    static readonly int ClipUvId = Shader.PropertyToID("_ClipUV");
    static readonly int DstToClipId = Shader.PropertyToID("_DstToClip");
    static readonly int ClipOnId = Shader.PropertyToID("_ClipOn");
    static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    static readonly int ModeId = Shader.PropertyToID("_Mode");
    static readonly int DstSizeId = Shader.PropertyToID("_DstSize");
    static readonly int LutId = Shader.PropertyToID("_CorrectionLut");
    static readonly int LutOnId = Shader.PropertyToID("_CorrectionOn");
    static readonly int LutSizeId = Shader.PropertyToID("_CorrectionLutSize");

    static readonly int BackdropId = Shader.PropertyToID("_BackdropTex");
    static readonly int BackdropStId = Shader.PropertyToID("_BackdropST");
    static readonly int LiveOnId = Shader.PropertyToID("_LiveBlendOn");
    static readonly int LiveModeId = Shader.PropertyToID("_LiveBlendMode");
    static readonly int PremulId = Shader.PropertyToID("_BackdropPremul");

    public struct Item
    {
        public PixelLayer layer;
        public PixelLayer clipBase;
    }

    class Target
    {
        public RenderTexture texture;
        public Vector4 st;
        public PixelBlendMode mode;
    }

    class CanvasState
    {
        public readonly List<PixelLayer> layers = new List<PixelLayer>();
        public readonly Dictionary<PixelLayer, Target> targets = new Dictionary<PixelLayer, Target>();
        public int childCount = -1;
        public float collectedAt = -1;
        public int signature;
        public bool dirty = true;
    }

    static readonly Dictionary<PixelCanvas, CanvasState> states = new Dictionary<PixelCanvas, CanvasState>();
    static readonly List<PixelCanvas> pending = new List<PixelCanvas>();
    static readonly List<Item> items = new List<Item>();
    static Material material;
    static MaterialPropertyBlock block;
    static bool hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        ReleaseAll();
        PixelLayer.AfterBind -= OnBind;
        PixelLayer.AfterBind += OnBind;
        RenderPipelineManager.beginContextRendering -= OnBeginRendering;
        hooked = false;
    }

    public static bool NeedsPreview(PixelBlendMode mode)
    {
        return !PixelBlend.IsLive(mode);
    }

    public static void MarkDirty(PixelCanvas canvas)
    {
        CanvasState state;
        if(canvas != null && states.TryGetValue(canvas, out state)) state.dirty = true;
    }

    public static void Tick(PixelCanvas canvas)
    {
        if(canvas == null) return;
        if(!hooked)
        {
            RenderPipelineManager.beginContextRendering += OnBeginRendering;
            hooked = true;
        }
        if(!states.ContainsKey(canvas)) states[canvas] = new CanvasState();
    }

    static void OnBeginRendering(ScriptableRenderContext context, List<Camera> cameras)
    {
        if(!Application.isPlaying) return;
        pending.Clear();
        foreach(var pair in states) pending.Add(pair.Key);
        for(var i = 0; i < pending.Count; i++)
        {
            var canvas = pending[i];
            if(canvas == null)
            {
                Release(canvas);
                continue;
            }
            if(!canvas.isActiveAndEnabled) continue;
            Update(canvas, states[canvas]);
        }
    }

    static void Update(PixelCanvas canvas, CanvasState state)
    {
        var now = Time.realtimeSinceStartup;
        if(state.childCount != canvas.transform.childCount || now - state.collectedAt > RecollectInterval)
        {
            Collect(canvas, state.layers);
            state.childCount = canvas.transform.childCount;
            state.collectedAt = now;
        }

        bool hasPreview;
        var signature = Signature(canvas, state.layers, true, out hasPreview);
        if(!hasPreview)
        {
            if(state.targets.Count > 0) ReleaseTargets(state);
            state.signature = signature;
            return;
        }
        if(!state.dirty && signature == state.signature) return;
        state.signature = signature;
        state.dirty = false;
        Render(state);
    }

    public static void Release(PixelCanvas canvas)
    {
        CanvasState state;
        if(ReferenceEquals(canvas, null) || !states.TryGetValue(canvas, out state)) return;
        ReleaseTargets(state);
        states.Remove(canvas);
    }

    public static void ReleaseAll()
    {
        foreach(var pair in states) ReleaseTargets(pair.Value);
        states.Clear();
    }

    public static void Collect(PixelCanvas canvas, List<PixelLayer> layers)
    {
        layers.Clear();
        layers.AddRange(canvas.GetComponentsInChildren<PixelLayer>(true));
        layers.Sort((a, b) => a.sortIndex.CompareTo(b.sortIndex));
    }

    public static void BuildItems(List<PixelLayer> layers, List<Item> result)
    {
        result.Clear();
        var hasBase = false;
        var baseVisible = true;
        PixelLayer baseLayer = null;
        for(var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            if(layer == null || layer.IsCorrection) continue;
            var visible = layer.visible && layer.gameObject.activeInHierarchy;
            var clip = layer.clip && hasBase;
            if(!clip)
            {
                hasBase = true;
                baseVisible = visible;
                baseLayer = layer;
            }
            if(!visible || (clip && !baseVisible)) continue;
            result.Add(new Item { layer = layer, clipBase = clip ? baseLayer : null });
        }
    }

    public static int TransformSignature(PixelCanvas canvas, List<PixelLayer> layers)
    {
        unchecked
        {
            var hash = 17;
            var toCanvas = canvas.transform.worldToLocalMatrix;
            for(var i = 0; i < layers.Count; i++)
            {
                var l = layers[i];
                if(l == null || l.IsCorrection) continue;
                var sr = l.Surface(false);
                if(sr == null) continue;
                var m = toCanvas * sr.transform.localToWorldMatrix;
                hash = hash * 31 + m.m00.GetHashCode();
                hash = hash * 31 + m.m01.GetHashCode();
                hash = hash * 31 + m.m10.GetHashCode();
                hash = hash * 31 + m.m11.GetHashCode();
                hash = hash * 31 + m.m03.GetHashCode();
                hash = hash * 31 + m.m13.GetHashCode();
            }
            return hash;
        }
    }

    public static int Signature(PixelCanvas canvas, List<PixelLayer> layers, bool withTransforms, out bool hasPreview)
    {
        hasPreview = false;
        unchecked
        {
            var hash = 17;
            for(var i = 0; i < layers.Count; i++)
            {
                var l = layers[i];
                if(l == null)
                {
                    hash = hash * 31 + 1;
                    continue;
                }
                if(!l.IsCorrection && NeedsPreview(l.blendMode)) hasPreview = true;
                var sr = l.Surface(false);
                var sprite = sr != null ? sr.sprite : null;
                var lut = l.CorrectionLut;
                hash = hash * 31 + l.GetInstanceID();
                hash = hash * 31 + (l.visible && l.gameObject.activeInHierarchy ? 1 : 0);
                hash = hash * 31 + l.opacity.GetHashCode();
                hash = hash * 31 + (int)l.blendMode;
                hash = hash * 31 + l.sortIndex;
                hash = hash * 31 + (l.clip ? 1 : 0);
                hash = hash * 31 + l.frame;
                hash = hash * 31 + (sprite != null ? sprite.GetInstanceID() : 0);
                hash = hash * 31 + (lut != null ? lut.GetInstanceID() : 0);
            }
            if(withTransforms && hasPreview) hash = hash * 31 + TransformSignature(canvas, layers);
            return hash;
        }
    }

    static void Render(CanvasState state)
    {
        BuildItems(state.layers, items);
        var live = new HashSet<PixelLayer>();
        for(var i = 1; i < items.Count; i++)
        {
            var layer = items[i].layer;
            if(!NeedsPreview(layer.blendMode)) continue;
            var sr = layer.Surface(false);
            if(sr == null || sr.sprite == null) continue;

            Target target;
            if(!state.targets.TryGetValue(layer, out target))
            {
                target = new Target();
                state.targets[layer] = target;
            }
            if(!RenderInto(items, i, ref target.texture)) continue;

            target.st = BackdropST(sr.sprite);
            target.mode = layer.blendMode;
            Bind(sr, target);
            live.Add(layer);
        }

        var stale = new List<PixelLayer>();
        foreach(var pair in state.targets)
            if(pair.Key == null || !live.Contains(pair.Key)) stale.Add(pair.Key);
        for(var i = 0; i < stale.Count; i++) ReleaseTarget(state, stale[i]);
    }

    public static Vector4 BackdropST(Sprite sprite)
    {
        var rect = sprite.textureRect;
        var tex = sprite.texture;
        return new Vector4(tex.width / rect.width, tex.height / rect.height, -rect.x / rect.width, -rect.y / rect.height);
    }

    public static bool RenderInto(List<Item> list, int index, ref RenderTexture texture)
    {
        var mat = Material();
        if(mat == null) return false;
        var layer = list[index].layer;
        var dst = layer.Surface(false);
        if(dst == null || dst.sprite == null) return false;
        var w = Mathf.RoundToInt(dst.sprite.textureRect.width);
        var h = Mathf.RoundToInt(dst.sprite.textureRect.height);
        if(w <= 0 || h <= 0) return false;

        if(texture == null || texture.width != w || texture.height != h)
        {
            Destroy(texture);
            texture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.name = "PixelBackdrop " + layer.name;
            texture.Create();
        }

        var previous = RenderTexture.active;
        var a = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        var b = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        a.filterMode = FilterMode.Point;
        b.filterMode = FilterMode.Point;
        RenderTexture.active = a;
        GL.Clear(false, true, Color.clear);
        RenderTexture.active = previous;

        var dstToWorld = dst.transform.localToWorldMatrix * PixelToLocal(dst.sprite);
        mat.SetVector(DstSizeId, new Vector4(w, h, 0, 0));
        for(var j = 0; j < index; j++)
        {
            var item = list[j];
            Texture tex;
            Vector4 size, uv;
            Matrix4x4 m;
            if(!Describe(item.layer, dstToWorld, out tex, out m, out size, out uv)) continue;
            mat.SetTexture(SrcTexId, tex);
            mat.SetMatrix(DstToSrcId, m);
            mat.SetVector(SrcSizeId, size);
            mat.SetVector(SrcUvId, uv);

            Texture clipTex = null;
            Matrix4x4 clipM = Matrix4x4.identity;
            Vector4 clipSize = default, clipUv = default;
            var clipOn = item.clipBase != null && Describe(item.clipBase, dstToWorld, out clipTex, out clipM, out clipSize, out clipUv);
            if(clipOn)
            {
                mat.SetTexture(ClipTexId, clipTex);
                mat.SetMatrix(DstToClipId, clipM);
                mat.SetVector(ClipSizeId, clipSize);
                mat.SetVector(ClipUvId, clipUv);
            }
            mat.SetFloat(ClipOnId, clipOn ? 1f : 0f);

            mat.SetFloat(OpacityId, item.layer.opacity);
            mat.SetFloat(ModeId, (int)item.layer.blendMode);
            var lut = item.layer.CorrectionLut;
            mat.SetFloat(LutOnId, lut != null ? 1f : 0f);
            if(lut != null)
            {
                mat.SetTexture(LutId, lut);
                mat.SetFloat(LutSizeId, lut.width);
            }

            mat.SetTexture(MainTexId, a);
            Graphics.Blit(a, b, mat, 0);
            var swap = a;
            a = b;
            b = swap;
        }

        Graphics.Blit(a, texture);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(a);
        RenderTexture.ReleaseTemporary(b);
        return true;
    }

    static Matrix4x4 PixelToLocal(Sprite sprite)
    {
        return Matrix4x4.Scale(Vector3.one / sprite.pixelsPerUnit) * Matrix4x4.Translate(-(Vector3)sprite.pivot);
    }

    static Matrix4x4 LocalToPixel(Sprite sprite)
    {
        return Matrix4x4.Translate(sprite.pivot) * Matrix4x4.Scale(Vector3.one * sprite.pixelsPerUnit);
    }

    static bool Describe(PixelLayer layer, Matrix4x4 dstToWorld, out Texture texture, out Matrix4x4 dstToPixel, out Vector4 size, out Vector4 uv)
    {
        texture = null;
        dstToPixel = Matrix4x4.identity;
        size = uv = default;
        if(layer == null) return false;
        var sr = layer.Surface(false);
        if(sr == null || sr.sprite == null || sr.sprite.texture == null) return false;
        var sprite = sr.sprite;
        var rect = sprite.textureRect;
        var tex = sprite.texture;
        texture = tex;
        dstToPixel = LocalToPixel(sprite) * sr.transform.worldToLocalMatrix * dstToWorld;
        size = new Vector4(rect.width, rect.height, 0, 0);
        uv = new Vector4(rect.x / tex.width, rect.y / tex.height, 1f / tex.width, 1f / tex.height);
        return true;
    }

    static Material Material()
    {
        if(material != null) return material;
        var shader = Resources.Load<Shader>("PixelBackdropComposite");
        if(shader == null)
        {
            Debug.LogError("PixelBackdropGpu: shader 'Resources/PixelBackdropComposite' not found.");
            return null;
        }
        material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        return material;
    }

    static void Bind(SpriteRenderer sr, Target target)
    {
        if(block == null) block = new MaterialPropertyBlock();
        sr.GetPropertyBlock(block);
        block.SetTexture(BackdropId, target.texture);
        block.SetVector(BackdropStId, target.st);
        block.SetFloat(LiveModeId, (int)target.mode);
        block.SetFloat(LiveOnId, 1f);
        block.SetFloat(PremulId, 1f);
        sr.SetPropertyBlock(block);
    }

    static void OnBind(PixelLayer layer)
    {
        if(layer == null || !Application.isPlaying) return;
        foreach(var pair in states)
        {
            Target target;
            if(!pair.Value.targets.TryGetValue(layer, out target) || target.texture == null) continue;
            var sr = layer.Surface(false);
            if(sr != null) Bind(sr, target);
            return;
        }
    }

    static void ReleaseTargets(CanvasState state)
    {
        var layers = new List<PixelLayer>(state.targets.Keys);
        for(var i = 0; i < layers.Count; i++) ReleaseTarget(state, layers[i]);
        state.targets.Clear();
    }

    static void ReleaseTarget(CanvasState state, PixelLayer layer)
    {
        Target target;
        if(!state.targets.TryGetValue(layer, out target)) return;
        state.targets.Remove(layer);
        if(layer != null)
        {
            var sr = layer.Surface(false);
            if(sr != null && sr.HasPropertyBlock())
            {
                if(block == null) block = new MaterialPropertyBlock();
                sr.GetPropertyBlock(block);
                block.SetFloat(LiveOnId, 0f);
                sr.SetPropertyBlock(block);
            }
        }
        Destroy(target.texture);
    }

    static void Destroy(RenderTexture texture)
    {
        if(texture == null) return;
        texture.Release();
        if(Application.isPlaying) Object.Destroy(texture);
        else Object.DestroyImmediate(texture);
    }
}
