#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

public enum PixelSelectMode { Replace, Add, Subtract, Intersect }

public class PixelMask
{
    public readonly int x;
    public readonly int y;
    public readonly int w;
    public readonly int h;
    public readonly bool[] bits;
    public bool outside;

    public PixelMask(int x, int y, int w, int h, bool outside)
    {
        this.x = x;
        this.y = y;
        this.w = Mathf.Max(0, w);
        this.h = Mathf.Max(0, h);
        this.outside = outside;
        bits = new bool[this.w * this.h];
    }

    public bool Contains(int px, int py)
    {
        var tx = px - x;
        var ty = py - y;
        if(tx < 0 || ty < 0 || tx >= w || ty >= h) return outside;
        return bits[ty * w + tx];
    }

    public bool IsEmpty
    {
        get
        {
            if(outside) return false;
            for(var i = 0; i < bits.Length; i++)
                if(bits[i]) return false;
            return true;
        }
    }

    public RectInt Bounds
    {
        get { return new RectInt(x, y, w, h); }
    }

    public PixelMask Inverted()
    {
        var m = new PixelMask(x, y, w, h, !outside);
        for(var i = 0; i < bits.Length; i++) m.bits[i] = !bits[i];
        return m;
    }

    public static PixelMask Rect(int x0, int y0, int x1, int y1)
    {
        var minX = Mathf.Min(x0, x1);
        var minY = Mathf.Min(y0, y1);
        var m = new PixelMask(minX, minY, Mathf.Abs(x1 - x0) + 1, Mathf.Abs(y1 - y0) + 1, false);
        for(var i = 0; i < m.bits.Length; i++) m.bits[i] = true;
        return m;
    }

    public static PixelMask FromRegion(RectInt r, bool[] region)
    {
        var m = new PixelMask(r.xMin, r.yMin, r.width, r.height, false);
        System.Array.Copy(region, m.bits, Mathf.Min(region.Length, m.bits.Length));
        return m;
    }

    public static PixelMask Polygon(List<Vector2> pts)
    {
        if(pts.Count < 3) return null;
        var fx0 = float.MaxValue;
        var fy0 = float.MaxValue;
        var fx1 = float.MinValue;
        var fy1 = float.MinValue;
        for(var i = 0; i < pts.Count; i++)
        {
            fx0 = Mathf.Min(fx0, pts[i].x);
            fy0 = Mathf.Min(fy0, pts[i].y);
            fx1 = Mathf.Max(fx1, pts[i].x);
            fy1 = Mathf.Max(fy1, pts[i].y);
        }
        var minX = Mathf.FloorToInt(fx0);
        var minY = Mathf.FloorToInt(fy0);
        var w = Mathf.CeilToInt(fx1) - minX;
        var h = Mathf.CeilToInt(fy1) - minY;
        if(w <= 0 || h <= 0) return null;
        var m = new PixelMask(minX, minY, w, h, false);
        var xs = new List<float>();
        for(var row = 0; row < h; row++)
        {
            var yc = minY + row + 0.5f;
            xs.Clear();
            for(var i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                if((a.y <= yc && b.y > yc) || (b.y <= yc && a.y > yc))
                    xs.Add(a.x + (yc - a.y) / (b.y - a.y) * (b.x - a.x));
            }
            xs.Sort();
            for(var k = 0; k + 1 < xs.Count; k += 2)
            {
                var from = Mathf.CeilToInt(xs[k] - 0.5f);
                var to = Mathf.CeilToInt(xs[k + 1] - 0.5f) - 1;
                for(var px = from; px <= to; px++)
                {
                    var tx = px - minX;
                    if(tx >= 0 && tx < w) m.bits[row * w + tx] = true;
                }
            }
        }
        return m;
    }

    public static PixelMask Combine(PixelMask a, PixelMask b, PixelSelectMode mode)
    {
        if(a == null) return mode == PixelSelectMode.Subtract ? null : b;
        if(b == null) return mode == PixelSelectMode.Replace ? null : a;
        if(mode == PixelSelectMode.Replace) return b;
        var x0 = Mathf.Min(a.x, b.x);
        var y0 = Mathf.Min(a.y, b.y);
        var x1 = Mathf.Max(a.x + a.w, b.x + b.w);
        var y1 = Mathf.Max(a.y + a.h, b.y + b.h);
        var m = new PixelMask(x0, y0, x1 - x0, y1 - y0, Op(a.outside, b.outside, mode));
        for(var yy = 0; yy < m.h; yy++)
            for(var xx = 0; xx < m.w; xx++)
                m.bits[yy * m.w + xx] = Op(a.Contains(x0 + xx, y0 + yy), b.Contains(x0 + xx, y0 + yy), mode);
        return m;
    }

    static bool Op(bool p, bool q, PixelSelectMode mode)
    {
        switch(mode)
        {
            case PixelSelectMode.Add: return p || q;
            case PixelSelectMode.Subtract: return p && !q;
            case PixelSelectMode.Intersect: return p && q;
            default: return q;
        }
    }
}

[InitializeOnLoad]
public static class PixelSelection
{
    static readonly List<Vector2Int> edges = new List<Vector2Int>();
    static bool edgesDirty;
    static double nextTick;

    public static PixelMask Mask { get; private set; }
    public static PixelCanvas Owner { get; private set; }

    static PixelSelection()
    {
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if(Mask == null) return;
        if(EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + 0.25;
        if(ToolManager.activeToolType == typeof(PixelPaintTool)) SceneView.RepaintAll();
    }

    public static PixelMask For(PixelCanvas canvas)
    {
        if(Owner != canvas || Owner == null) return null;
        return Mask;
    }

    public static bool Has(PixelCanvas canvas)
    {
        return For(canvas) != null;
    }

    public static void Set(PixelCanvas canvas, PixelMask mask, bool record)
    {
        if(mask != null && mask.IsEmpty) mask = null;
        if(record) PixelHistory.RecordSelection(canvas, For(canvas), mask);
        Mask = mask;
        Owner = mask == null ? null : canvas;
        edgesDirty = true;
        SceneView.RepaintAll();
    }

    public static void Apply(PixelCanvas canvas, PixelMask shape, PixelSelectMode mode)
    {
        var result = PixelMask.Combine(For(canvas), shape, mode);
        Set(canvas, result, true);
    }

    public static void Clear(PixelCanvas canvas)
    {
        if(For(canvas) == null) return;
        Set(canvas, null, true);
    }

    public static void SelectAll(PixelCanvas canvas)
    {
        var r = PixelCanvasEditing.CanvasBounds(canvas);
        if(r.width <= 0 || r.height <= 0)
        {
            var step = Mathf.Max(1, canvas.growStep);
            r = new RectInt(0, 0, step, step);
        }
        Set(canvas, PixelMask.Rect(r.xMin, r.yMin, r.xMax - 1, r.yMax - 1), true);
    }

    public static void Invert(PixelCanvas canvas)
    {
        var current = For(canvas);
        if(current == null) return;
        Set(canvas, current.Inverted(), true);
    }

    public static void Draw(PixelLayer layer)
    {
        var mask = For(layer.Root);
        if(mask == null) return;
        if(edgesDirty) Rebuild(mask);
        if(edges.Count == 0) return;
        var ppu = layer.Root.pixelsPerUnit;
        var pts = new Vector3[edges.Count];
        var t = layer.transform;
        for(var i = 0; i < pts.Length; i++)
            pts[i] = t.TransformPoint(new Vector3(edges[i].x / ppu, edges[i].y / ppu, 0));
        var phase = ((int)(EditorApplication.timeSinceStartup * 4)) & 1;
        Handles.color = phase == 0 ? Color.black : Color.white;
        Handles.DrawLines(pts);
        Handles.color = phase == 0 ? Color.white : Color.black;
        Handles.DrawDottedLines(pts, 4f);
        Handles.color = Color.white;
    }

    static void Rebuild(PixelMask m)
    {
        edgesDirty = false;
        edges.Clear();
        for(var yy = m.y; yy <= m.y + m.h; yy++)
        {
            var start = int.MinValue;
            for(var xx = m.x; xx <= m.x + m.w; xx++)
            {
                var edge = xx < m.x + m.w && m.Contains(xx, yy - 1) != m.Contains(xx, yy);
                if(edge && start == int.MinValue) start = xx;
                if(!edge && start != int.MinValue)
                {
                    edges.Add(new Vector2Int(start, yy));
                    edges.Add(new Vector2Int(xx, yy));
                    start = int.MinValue;
                }
            }
        }
        for(var xx = m.x; xx <= m.x + m.w; xx++)
        {
            var start = int.MinValue;
            for(var yy = m.y; yy <= m.y + m.h; yy++)
            {
                var edge = yy < m.y + m.h && m.Contains(xx - 1, yy) != m.Contains(xx, yy);
                if(edge && start == int.MinValue) start = yy;
                if(!edge && start != int.MinValue)
                {
                    edges.Add(new Vector2Int(xx, start));
                    edges.Add(new Vector2Int(xx, yy));
                    start = int.MinValue;
                }
            }
        }
    }
}
#endif
