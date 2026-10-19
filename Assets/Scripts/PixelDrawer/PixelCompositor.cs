#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

public static class PixelCompositor
{
    public struct Item
    {
        public PixelLayer layer;
        public float opacity;
    }

    public static List<Item> ForCanvas(PixelCanvas canvas, bool visibleOnly)
    {
        var layers = new List<PixelLayer>(canvas.GetComponentsInChildren<PixelLayer>(true));
        layers.Sort((a, b) => a.sortIndex.CompareTo(b.sortIndex));
        var items = new List<Item>();
        for(var i = 0; i < layers.Count; i++)
        {
            if(visibleOnly && (!layers[i].visible || !layers[i].gameObject.activeInHierarchy)) continue;
            items.Add(new Item { layer = layers[i], opacity = layers[i].opacity });
        }
        return items;
    }

    public static RectInt UnionBounds(List<Item> items)
    {
        var has = false;
        int x0 = 0, y0 = 0, x1 = 0, y1 = 0;
        for(var i = 0; i < items.Count; i++)
        {
            var b = PixelCanvasEditing.Bounds(items[i].layer);
            if(b.width <= 0 || b.height <= 0) continue;
            if(!has)
            {
                has = true;
                x0 = b.xMin;
                y0 = b.yMin;
                x1 = b.xMax;
                y1 = b.yMax;
            }
            else
            {
                x0 = Mathf.Min(x0, b.xMin);
                y0 = Mathf.Min(y0, b.yMin);
                x1 = Mathf.Max(x1, b.xMax);
                y1 = Mathf.Max(y1, b.yMax);
            }
        }
        return has ? new RectInt(x0, y0, x1 - x0, y1 - y0) : new RectInt(0, 0, 0, 0);
    }

    public static Color32[] Composite(List<Item> items, RectInt rect)
    {
        var result = new Color32[Mathf.Max(0, rect.width * rect.height)];
        if(result.Length == 0) return result;
        var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        var lut = new float[256];
        for(var i = 0; i < 256; i++) lut[i] = linear ? Mathf.GammaToLinearSpace(i / 255f) : i / 255f;

        var count = items.Count;
        var pix = new Color32[count][];
        var org = new Vector2Int[count];
        var pw = new int[count];
        var ph = new int[count];
        var op = new float[count];
        for(var i = 0; i < count; i++)
        {
            pix[i] = PixelCanvasEditing.ReadPixels(items[i].layer, out org[i], out pw[i], out ph[i]);
            op[i] = items[i].opacity;
        }

        for(var y = 0; y < rect.height; y++)
        {
            for(var x = 0; x < rect.width; x++)
            {
                float outA = 0, outR = 0, outG = 0, outB = 0;
                var wx = rect.xMin + x;
                var wy = rect.yMin + y;
                for(var i = 0; i < count; i++)
                {
                    if(pix[i] == null) continue;
                    var tx = wx - org[i].x;
                    var ty = wy - org[i].y;
                    if(tx < 0 || ty < 0 || tx >= pw[i] || ty >= ph[i]) continue;
                    var c = pix[i][ty * pw[i] + tx];
                    var a = c.a / 255f * op[i];
                    if(a <= 0) continue;
                    outR = lut[c.r] * a + outR * (1 - a);
                    outG = lut[c.g] * a + outG * (1 - a);
                    outB = lut[c.b] * a + outB * (1 - a);
                    outA = a + outA * (1 - a);
                }
                if(outA <= 0) continue;
                float fr = outR / outA, fg = outG / outA, fb = outB / outA;
                if(linear)
                {
                    fr = Mathf.LinearToGammaSpace(fr);
                    fg = Mathf.LinearToGammaSpace(fg);
                    fb = Mathf.LinearToGammaSpace(fb);
                }
                result[y * rect.width + x] = new Color32(ToByte(fr), ToByte(fg), ToByte(fb), ToByte(outA));
            }
        }
        return result;
    }

    static byte ToByte(float v)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    }
}
#endif
