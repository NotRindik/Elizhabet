#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

public static class PixelClipboard
{
    const string Header = "PIXELDRAWER1";

    static Color32[] pixels;
    static int width;
    static int height;

    public static bool Capture(PixelLayer layer, bool merged, out Color32[] result, out int w, out int h)
    {
        result = null;
        w = 0;
        h = 0;
        var canvas = layer.Root;
        var mask = PixelSelection.For(canvas);
        List<PixelCompositor.Item> items = null;
        RectInt rect;
        if(merged)
        {
            items = PixelCompositor.ForCanvas(canvas, true);
            rect = PixelCompositor.UnionBounds(items);
        }
        else
        {
            rect = PixelCanvasEditing.Bounds(layer);
        }
        if(mask != null && !mask.outside)
        {
            var m = mask.Bounds;
            var x0 = Mathf.Max(rect.xMin, m.xMin);
            var y0 = Mathf.Max(rect.yMin, m.yMin);
            var x1 = Mathf.Min(rect.xMax, m.xMax);
            var y1 = Mathf.Min(rect.yMax, m.yMax);
            rect = new RectInt(x0, y0, x1 - x0, y1 - y0);
        }
        if(rect.width <= 0 || rect.height <= 0) return false;

        if(merged)
        {
            result = PixelCompositor.Composite(items, rect);
        }
        else
        {
            Vector2Int o;
            int pw, ph;
            var src = PixelCanvasEditing.ReadPixels(layer, out o, out pw, out ph);
            if(src == null) return false;
            result = new Color32[rect.width * rect.height];
            for(var y = 0; y < rect.height; y++)
                System.Array.Copy(src, (rect.yMin + y - o.y) * pw + (rect.xMin - o.x), result, y * rect.width, rect.width);
        }
        if(mask != null)
        {
            for(var y = 0; y < rect.height; y++)
                for(var x = 0; x < rect.width; x++)
                    if(!mask.Contains(rect.xMin + x, rect.yMin + y)) result[y * rect.width + x] = new Color32(0, 0, 0, 0);
        }
        w = rect.width;
        h = rect.height;
        return true;
    }

    public static void Copy(PixelLayer layer, bool cut, bool merged)
    {
        PixelFloating.CommitIfActive();
        Color32[] data;
        int w, h;
        if(!Capture(layer, merged, out data, out w, out h)) return;
        pixels = data;
        width = w;
        height = h;
        GUIUtility.systemCopyBuffer = ToText(data, w, h);
        if(cut && !merged && !layer.locked)
        {
            PixelHistory.BeginStroke(layer);
            PixelOps.ClearSelection(layer);
            PixelCanvasEditing.Apply();
            PixelHistory.EndStroke();
            SceneView.RepaintAll();
        }
    }

    public static bool Paste(PixelLayer layer, bool asNewLayer)
    {
        PixelFloating.CommitIfActive();
        Color32[] data;
        int w, h;
        if(!TryFromText(GUIUtility.systemCopyBuffer, out data, out w, out h))
        {
            if(pixels == null) return false;
            data = pixels;
            w = width;
            h = height;
        }
        var target = layer;
        if(asNewLayer) target = PixelLayerOps.CreateLayer(layer.Root, layer, true);
        else if(layer.locked) return false;
        PixelFloating.BeginPaste(target, data, w, h);
        return true;
    }

    public static string ToText(Color32[] data, int w, int h)
    {
        var bytes = new byte[data.Length * 4];
        for(var i = 0; i < data.Length; i++)
        {
            bytes[i * 4] = data[i].r;
            bytes[i * 4 + 1] = data[i].g;
            bytes[i * 4 + 2] = data[i].b;
            bytes[i * 4 + 3] = data[i].a;
        }
        using(var ms = new MemoryStream())
        {
            using(var gz = new GZipStream(ms, CompressionMode.Compress))
                gz.Write(bytes, 0, bytes.Length);
            return Header + ";" + w + ";" + h + ";" + System.Convert.ToBase64String(ms.ToArray());
        }
    }

    public static bool TryFromText(string text, out Color32[] data, out int w, out int h)
    {
        data = null;
        w = 0;
        h = 0;
        if(string.IsNullOrEmpty(text) || !text.StartsWith(Header + ";")) return false;
        var parts = text.Split(new[] { ';' }, 4);
        if(parts.Length != 4 || !int.TryParse(parts[1], out w) || !int.TryParse(parts[2], out h) || w <= 0 || h <= 0) return false;
        try
        {
            var compressed = System.Convert.FromBase64String(parts[3]);
            var bytes = new byte[w * h * 4];
            using(var ms = new MemoryStream(compressed))
            using(var gz = new GZipStream(ms, CompressionMode.Decompress))
            {
                var read = 0;
                while(read < bytes.Length)
                {
                    var n = gz.Read(bytes, read, bytes.Length - read);
                    if(n <= 0) break;
                    read += n;
                }
                if(read != bytes.Length) return false;
            }
            data = new Color32[w * h];
            for(var i = 0; i < data.Length; i++)
                data[i] = new Color32(bytes[i * 4], bytes[i * 4 + 1], bytes[i * 4 + 2], bytes[i * 4 + 3]);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
#endif
