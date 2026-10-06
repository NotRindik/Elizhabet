using System.Collections.Generic;
using UnityEngine;

public static class SpriteColorLocator
{
    private static readonly Dictionary<Sprite, Dictionary<uint, Vector2Int>> Cache = new();

    public static uint Pack(Color32 c) => (uint)(c.r | (c.g << 8) | (c.b << 16) | (c.a << 24));

    public static Dictionary<uint, Vector2Int> Get(Sprite sprite, HashSet<uint> targets)
    {
        if (Cache.TryGetValue(sprite, out var cached)) return cached;

        var result = new Dictionary<uint, Vector2Int>(targets.Count);
        Cache[sprite] = result;

        var tex = sprite.texture;
        if (!tex.isReadable)
        {
            Debug.LogWarning($"[SpriteColorLocator] У текстуры '{tex.name}' выключен Read/Write", tex);
            return result;
        }

        Rect r = sprite.textureRect;
        int x0 = (int)r.x, y0 = (int)r.y, w = (int)r.width, h = (int)r.height, texW = tex.width;

        Color32[] fallback = null;
        Unity.Collections.NativeArray<Color32> raw = default;
        bool useRaw = tex.format == TextureFormat.RGBA32;
        if (useRaw) raw = tex.GetRawTextureData<Color32>();
        else fallback = tex.GetPixels32();
        
        int need = targets.Count;
        for (int y = 0; y < h && result.Count < need; y++)
        {
            int row = (y0 + y) * texW + x0;
            for (int x = 0; x < w; x++)
            {
                Color32 c = useRaw ? raw[row + x] : fallback[row + x];
                uint key = Pack(c);
                if (targets.Contains(key) && !result.ContainsKey(key))
                {
                    result[key] = new Vector2Int(x, y);
                    if (result.Count == need) break;
                }
            }
        }
        return result;
    }
    

    public static void Clear() => Cache.Clear();
}