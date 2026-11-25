using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Serialization;

[PreferBinarySerialization]
public class PixelArtAsset : ScriptableObject
{
    public enum LoopMode { Loop, Once, PingPong }

    [System.Serializable]
    public class Frame
    {
        public int dataIndex = -1;
        public Vector2Int origin;
        [Min(0.01f)] public float duration = 0.1f;

        [FormerlySerializedAs("color"), HideInInspector] public Texture2D legacyColor;
        [FormerlySerializedAs("emission"), HideInInspector] public Texture2D legacyEmission;
        [FormerlySerializedAs("sprite"), HideInInspector] public Sprite legacySprite;

        public bool IsLegacy
        {
            get { return legacyColor != null || legacyEmission != null || legacySprite != null; }
        }
    }

    [System.Serializable]
    public class FrameData
    {
        public int width;
        public int height;
        public byte[] png;
        public byte[] glow;

        [System.NonSerialized] public Texture2D color;
        [System.NonSerialized] public Texture2D emission;
        [System.NonSerialized] public Sprite sprite;
    }

    public List<Frame> frames = new List<Frame>();
    [HideInInspector] public List<FrameData> pixelData = new List<FrameData>();
    public float pixelsPerUnit = 32;
    public LoopMode loop;
    public Material material;

    public int Count
    {
        get { return frames.Count; }
    }

    public bool HasEmission
    {
        get
        {
            for(var i = 0; i < frames.Count; i++)
                if(HasGlow(frames[i])) return true;
            return false;
        }
    }

    public float TotalDuration
    {
        get
        {
            var total = 0f;
            for(var i = 0; i < frames.Count; i++) total += Mathf.Max(0.001f, frames[i].duration);
            return total;
        }
    }

    public int FrameAt(float time)
    {
        var n = frames.Count;
        if(n <= 1) return 0;
        var total = TotalDuration;
        float t;
        if(loop == LoopMode.Once) t = Mathf.Min(time, total - 0.0001f);
        else if(loop == LoopMode.Loop) t = Mathf.Repeat(time, total);
        else
        {
            var m = Mathf.Repeat(time, total * 2);
            t = Mathf.Min(m < total ? m : total * 2 - m, total - 0.0001f);
        }
        if(t < 0) t = 0;
        var acc = 0f;
        for(var i = 0; i < n; i++)
        {
            acc += Mathf.Max(0.001f, frames[i].duration);
            if(t < acc) return i;
        }
        return n - 1;
    }

    public FrameData DataOf(Frame f)
    {
        if(f == null || f.dataIndex < 0 || f.dataIndex >= pixelData.Count) return null;
        return pixelData[f.dataIndex];
    }

    public bool HasPixels(Frame f)
    {
        return DataOf(f) != null || (f != null && f.legacyColor != null);
    }

    public bool HasGlow(Frame f)
    {
        var d = DataOf(f);
        if(d != null) return d.glow != null;
        return f != null && f.legacyEmission != null;
    }

    public Vector2Int SizeOf(Frame f)
    {
        var d = DataOf(f);
        if(d != null) return new Vector2Int(d.width, d.height);
        if(f != null && f.legacyColor != null) return new Vector2Int(f.legacyColor.width, f.legacyColor.height);
        return Vector2Int.zero;
    }

    public Texture2D ColorOf(Frame f)
    {
        var d = DataOf(f);
        if(d == null) return f != null ? f.legacyColor : null;
        if(d.color == null) d.color = DecodeColor(d, f.dataIndex);
        return d.color;
    }

    public Texture2D EmissionOf(Frame f)
    {
        var d = DataOf(f);
        if(d == null) return f != null ? f.legacyEmission : null;
        if(d.glow == null) return null;
        if(d.emission == null) d.emission = DecodeGlow(d, f.dataIndex);
        return d.emission;
    }

    public Sprite SpriteOf(Frame f)
    {
        var d = DataOf(f);
        if(d == null) return f != null ? f.legacySprite : null;
        var tex = ColorOf(f);
        if(tex == null) return null;
        if(d.sprite == null || d.sprite.texture != tex || !Mathf.Approximately(d.sprite.pixelsPerUnit, pixelsPerUnit))
        {
            DestroyCached(d.sprite);
            d.sprite = Sprite.Create(tex, new Rect(0, 0, d.width, d.height), Vector2.zero, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            d.sprite.name = tex.name;
            d.sprite.hideFlags = HideFlags.DontSave;
        }
        return d.sprite;
    }

    public void Prewarm()
    {
        for(var i = 0; i < frames.Count; i++)
        {
            SpriteOf(frames[i]);
            EmissionOf(frames[i]);
        }
    }

    public void ReleaseCache(FrameData d)
    {
        if(d == null) return;
        DestroyCached(d.sprite);
        DestroyCached(d.color);
        DestroyCached(d.emission);
        d.sprite = null;
        d.color = null;
        d.emission = null;
    }

    public void ReleaseCaches()
    {
        for(var i = 0; i < pixelData.Count; i++) ReleaseCache(pixelData[i]);
    }

    void OnDisable()
    {
        ReleaseCaches();
    }

    Texture2D DecodeColor(FrameData d, int index)
    {
        if(d.png == null || d.png.Length == 0) return null;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(d.png, !Application.isEditor);
        Setup(tex, name + " " + index);
        return tex;
    }

    Texture2D DecodeGlow(FrameData d, int index)
    {
        var raw = Inflate(d.glow, d.width * d.height);
        var tex = new Texture2D(d.width, d.height, TextureFormat.R8, false, true);
        tex.LoadRawTextureData(raw);
        tex.Apply(false, !Application.isEditor);
        Setup(tex, name + " " + index + " emission");
        return tex;
    }

    static void Setup(Texture2D tex, string texName)
    {
        tex.name = texName;
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.DontSave;
    }

    static void DestroyCached(Object o)
    {
        if(o == null) return;
        if(Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    public static byte[] Deflate(byte[] raw)
    {
        using var ms = new MemoryStream();
        using(var ds = new DeflateStream(ms, System.IO.Compression.CompressionLevel.Optimal, true))
            ds.Write(raw, 0, raw.Length);
        return ms.ToArray();
    }

    public static byte[] Inflate(byte[] packed, int length)
    {
        var raw = new byte[length];
        using var ds = new DeflateStream(new MemoryStream(packed), CompressionMode.Decompress);
        var read = 0;
        while(read < length)
        {
            var n = ds.Read(raw, read, length - read);
            if(n <= 0) break;
            read += n;
        }
        return raw;
    }
}
