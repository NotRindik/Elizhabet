using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Pattern Brush", menuName = "Pixel Canvas/Pattern Brush")]
public class PixelPatternBrush : PixelBrush
{
    public enum PatternKind { Dither, Tile, Stamp }
    public enum DitherType { Bayer2, Bayer4, Bayer8, HorizontalLines, VerticalLines, DiagonalLines, Dots, Noise }
    public enum StampPivot { Center, BottomCenter }

    public class Bitmap
    {
        public int w;
        public int h;
        public Color32[] pixels;
    }

    public PatternKind kind = PatternKind.Dither;

    public DitherType dither = DitherType.Bayer4;
    [Range(0, 1)] public float density = 0.5f;
    public bool fillGaps;
    public Color32 gapColor = new Color32(0, 0, 0, 255);

    public List<Texture2D> images = new List<Texture2D>();
    public bool tintWithBrushColor;
    public Vector2Int tileOffset;

    public StampPivot pivot = StampPivot.BottomCenter;
    public bool randomPick = true;
    public bool randomFlipX = true;
    [Min(0)] public int jitterX;
    [Min(0)] public int jitterY;
    public int seed = 1;

    static readonly Dictionary<Texture2D, Bitmap> cache = new Dictionary<Texture2D, Bitmap>();
    static readonly Dictionary<int, float[]> bayer = new Dictionary<int, float[]>();

    public static void ClearCache()
    {
        cache.Clear();
    }

    public bool IsFieldVisible(string name)
    {
        switch(name)
        {
            case "dither":
            case "density":
            case "fillGaps":
            case "gapColor":
                return kind == PatternKind.Dither;
            case "images":
            case "tintWithBrushColor":
                return kind != PatternKind.Dither;
            case "tileOffset":
                return kind == PatternKind.Tile;
            case "pivot":
            case "randomPick":
            case "randomFlipX":
            case "jitterX":
            case "jitterY":
            case "seed":
                return kind == PatternKind.Stamp;
            case "shape":
            case "fill":
                return kind != PatternKind.Stamp;
        }
        return true;
    }

    public override bool FillPixel(int x, int y, Color32 color, out Color32 result)
    {
        result = color;
        if(kind == PatternKind.Dither)
        {
            if(DitherOn(x, y)) return true;
            if(fillGaps && color.a != 0)
            {
                result = new Color32(gapColor.r, gapColor.g, gapColor.b, gapColor.a);
                return true;
            }
            return false;
        }
        if(kind == PatternKind.Tile)
        {
            var bm = Pick(0);
            if(bm == null) return true;
            var tx = Mod(x - tileOffset.x, bm.w);
            var ty = Mod(y - tileOffset.y, bm.h);
            var p = bm.pixels[ty * bm.w + tx];
            if(color.a == 0) return p.a > 0;
            if(p.a == 0) return false;
            result = Shade(p, color);
            return true;
        }
        return true;
    }

    public override RectInt CursorRect(int x, int y, int size)
    {
        if(kind != PatternKind.Stamp) return base.CursorRect(x, y, size);
        var bm = Pick(0);
        if(bm == null) return base.CursorRect(x, y, size);
        var w = bm.w * size;
        var h = bm.h * size;
        if(pivot == StampPivot.BottomCenter) return new RectInt(x - w / 2, y, w, h);
        return new RectInt(x - w / 2, y - h / 2, w, h);
    }

    public override void Dab(int x, int y, int size, Color32 color, System.Action<int, int, Color32> plot)
    {
        if(kind == PatternKind.Stamp)
        {
            StampDab(x, y, size, color, plot);
            return;
        }
        var ox = x - size / 2;
        var oy = y - size / 2;
        for(var j = 0; j < size; j++)
        {
            for(var i = 0; i < size; i++)
            {
                if(!Covers(i, j, size)) continue;
                var px = ox + i;
                var py = oy + j;
                Color32 c;
                if(FillPixel(px, py, color, out c)) plot(px, py, c);
            }
        }
    }

    void StampDab(int x, int y, int size, Color32 color, System.Action<int, int, Color32> plot)
    {
        if(images == null || images.Count == 0) return;
        var h0 = Hash(x, y, seed);
        var index = randomPick ? (int)(h0 % (uint)images.Count) : 0;
        var bm = Pick(index);
        if(bm == null) return;
        var flip = randomFlipX && ((Hash(x, y, seed + 17) & 1) != 0);
        var jx = jitterX > 0 ? (int)(Hash(x, y, seed + 31) % (uint)(jitterX * 2 + 1)) - jitterX : 0;
        var jy = jitterY > 0 ? (int)(Hash(x, y, seed + 47) % (uint)(jitterY * 2 + 1)) - jitterY : 0;
        var scale = Mathf.Max(1, size);
        var left = x - (bm.w * scale) / 2 + jx;
        var bottom = pivot == StampPivot.BottomCenter ? y + jy : y - (bm.h * scale) / 2 + jy;
        var erase = color.a == 0;
        for(var j = 0; j < bm.h; j++)
        {
            for(var i = 0; i < bm.w; i++)
            {
                var p = bm.pixels[j * bm.w + (flip ? bm.w - 1 - i : i)];
                if(p.a == 0) continue;
                var c = erase ? new Color32(0, 0, 0, 0) : Shade(p, color);
                for(var sy = 0; sy < scale; sy++)
                    for(var sx = 0; sx < scale; sx++)
                        plot(left + i * scale + sx, bottom + j * scale + sy, c);
            }
        }
    }

    Color32 Shade(Color32 p, Color32 color)
    {
        if(!tintWithBrushColor) return p;
        return new Color32(color.r, color.g, color.b, (byte)(color.a * p.a / 255));
    }

    Bitmap Pick(int index)
    {
        if(images == null || images.Count == 0) return null;
        var tex = images[Mathf.Clamp(index, 0, images.Count - 1)];
        if(tex == null) return null;
        Bitmap bm;
        if(cache.TryGetValue(tex, out bm) && bm != null) return bm;
        bm = Read(tex);
        cache[tex] = bm;
        return bm;
    }

    static Bitmap Read(Texture2D tex)
    {
#if UNITY_EDITOR
        var path = UnityEditor.AssetDatabase.GetAssetPath(tex);
        if(!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
        {
            var temp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            temp.LoadImage(System.IO.File.ReadAllBytes(path));
            var result = new Bitmap { w = temp.width, h = temp.height, pixels = temp.GetPixels32() };
            DestroyImmediate(temp);
            return result;
        }
#endif
        if(!tex.isReadable) return null;
        return new Bitmap { w = tex.width, h = tex.height, pixels = tex.GetPixels32() };
    }

    bool DitherOn(int x, int y)
    {
        switch(dither)
        {
            case DitherType.Bayer2: return Threshold(2, x, y) < density;
            case DitherType.Bayer4: return Threshold(4, x, y) < density;
            case DitherType.Bayer8: return Threshold(8, x, y) < density;
            case DitherType.HorizontalLines: return Mod(y, 4) < Mathf.RoundToInt(density * 4);
            case DitherType.VerticalLines: return Mod(x, 4) < Mathf.RoundToInt(density * 4);
            case DitherType.DiagonalLines: return Mod(x + y, 4) < Mathf.RoundToInt(density * 4);
            case DitherType.Dots:
                if(density <= 0) return false;
                var p = Mathf.Max(2, Mathf.RoundToInt(1f / Mathf.Sqrt(density + 0.001f)));
                return Mod(x, p) == 0 && Mod(y, p) == 0;
            default:
                return (Hash(x, y, seed) % 1000) / 1000f < density;
        }
    }

    static float Threshold(int n, int x, int y)
    {
        float[] m;
        if(!bayer.TryGetValue(n, out m))
        {
            m = BuildBayer(n);
            bayer[n] = m;
        }
        return m[Mod(y, n) * n + Mod(x, n)];
    }

    static float[] BuildBayer(int n)
    {
        var size = 1;
        var cur = new int[] { 0 };
        while(size < n)
        {
            var next = size * 2;
            var arr = new int[next * next];
            for(var y = 0; y < next; y++)
            {
                for(var x = 0; x < next; x++)
                {
                    var b = cur[(y % size) * size + (x % size)] * 4;
                    var qx = x / size;
                    var qy = y / size;
                    var add = qy == 0 ? (qx == 0 ? 0 : 2) : (qx == 0 ? 3 : 1);
                    arr[y * next + x] = b + add;
                }
            }
            cur = arr;
            size = next;
        }
        var result = new float[n * n];
        for(var i = 0; i < result.Length; i++) result[i] = (cur[i] + 0.5f) / (n * n);
        return result;
    }

    static int Mod(int v, int m)
    {
        var r = v % m;
        return r < 0 ? r + m : r;
    }

    static uint Hash(int x, int y, int s)
    {
        unchecked
        {
            var h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(s * 83492791);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return h;
        }
    }
}
