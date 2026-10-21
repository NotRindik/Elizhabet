#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

public static class PixelOps
{
    static PixelLayer layer;
    static PixelMask mask;
    static bool tileX;
    static bool tileY;
    static int tileW = 1;
    static int tileH = 1;
    static bool symX;
    static bool symY;
    static int axisX;
    static int axisY;
    static int sinceDab;
    static readonly System.Action<int, int, Color32> plotAction = Put;
    static readonly List<Vector2Int> pointBuffer = new List<Vector2Int>();

    public static void Setup(PixelLayer target)
    {
        layer = target;
        var canvas = target.Root;
        mask = PixelSelection.For(canvas);
        tileX = canvas.TileX;
        tileY = canvas.TileY;
        tileW = Mathf.Max(1, canvas.tileSize.x);
        tileH = Mathf.Max(1, canvas.tileSize.y);
        symX = PixelPaintState.SymmetryX;
        symY = PixelPaintState.SymmetryY;
        axisX = PixelPaintState.AxisX2;
        axisY = PixelPaintState.AxisY2;
    }

    public static bool Allowed(int x, int y)
    {
        return mask == null || mask.Contains(x, y);
    }

    public static void PutSolo(int x, int y, Color32 c)
    {
        if(tileX) x = Mod(x, tileW);
        if(tileY) y = Mod(y, tileH);
        if(mask != null && !mask.Contains(x, y)) return;
        var mode = PixelPaintState.EmissionMode;
        if(mode == PixelEmissionMode.GlowOnly)
        {
            Color32 current;
            if(!PixelCanvasEditing.TryGetPixel(layer, x, y, out current)) return;
            PixelCanvasEditing.SetPixel(layer, x, y, current, c.a == 0 ? 0 : PixelPaintState.EmissionValue);
            return;
        }
        PixelCanvasEditing.SetPixel(layer, x, y, c, mode == PixelEmissionMode.ColorAndGlow ? PixelPaintState.EmissionValue : 0);
    }

    public static void Put(int x, int y, Color32 c)
    {
        PutSolo(x, y, c);
        var mx = axisX - 1 - x;
        var my = axisY - 1 - y;
        if(symX) PutSolo(mx, y, c);
        if(symY) PutSolo(x, my, c);
        if(symX && symY) PutSolo(mx, my, c);
    }

    public static void Dab(PixelLayer target, int x, int y, PixelBrush brush, int size, Color32 color)
    {
        Setup(target);
        brush.Dab(x, y, size, color, plotAction);
    }

    public static void LinePoints(Vector2Int a, Vector2Int b, List<Vector2Int> into)
    {
        var x0 = a.x;
        var y0 = a.y;
        var dx = Mathf.Abs(b.x - x0);
        var dy = Mathf.Abs(b.y - y0);
        var sx = x0 < b.x ? 1 : -1;
        var sy = y0 < b.y ? 1 : -1;
        var err = dx - dy;
        while(x0 != b.x || y0 != b.y)
        {
            var e2 = err * 2;
            if(e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if(e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
            into.Add(new Vector2Int(x0, y0));
        }
    }

    public static void Line(PixelLayer target, Vector2Int a, Vector2Int b, PixelBrush brush, int size, Color32 color, bool includeStart, bool ignoreSpacing)
    {
        Setup(target);
        var spacing = ignoreSpacing ? 1 : Mathf.Max(1, brush.spacing);
        if(includeStart)
        {
            brush.Dab(a.x, a.y, size, color, plotAction);
            sinceDab = 0;
        }
        pointBuffer.Clear();
        LinePoints(a, b, pointBuffer);
        for(var i = 0; i < pointBuffer.Count; i++)
        {
            sinceDab++;
            if(sinceDab < spacing) continue;
            sinceDab = 0;
            brush.Dab(pointBuffer[i].x, pointBuffer[i].y, size, color, plotAction);
        }
    }

    public static void Rect(PixelLayer target, Vector2Int a, Vector2Int b, PixelBrush brush, int size, Color32 color, bool filled)
    {
        Setup(target);
        var x0 = Mathf.Min(a.x, b.x);
        var x1 = Mathf.Max(a.x, b.x);
        var y0 = Mathf.Min(a.y, b.y);
        var y1 = Mathf.Max(a.y, b.y);
        if(filled)
        {
            for(var y = y0; y <= y1; y++)
                for(var x = x0; x <= x1; x++)
                    FillPix(x, y, brush, color);
            return;
        }
        DabLine(new Vector2Int(x0, y0), new Vector2Int(x1, y0), brush, size, color);
        DabLine(new Vector2Int(x1, y0), new Vector2Int(x1, y1), brush, size, color);
        DabLine(new Vector2Int(x1, y1), new Vector2Int(x0, y1), brush, size, color);
        DabLine(new Vector2Int(x0, y1), new Vector2Int(x0, y0), brush, size, color);
    }

    public static void Ellipse(PixelLayer target, Vector2Int a, Vector2Int b, PixelBrush brush, int size, Color32 color, bool filled)
    {
        Setup(target);
        var x0 = Mathf.Min(a.x, b.x);
        var y0 = Mathf.Min(a.y, b.y);
        var w = Mathf.Abs(b.x - a.x) + 1;
        var h = Mathf.Abs(b.y - a.y) + 1;
        var inside = new bool[w * h];
        for(var j = 0; j < h; j++)
        {
            var dy = (j + 0.5f - h * 0.5f) / (h * 0.5f);
            var sq = 1f - dy * dy;
            if(sq < 0) continue;
            var hw = Mathf.Sqrt(sq) * w * 0.5f;
            for(var i = 0; i < w; i++)
                if(Mathf.Abs(i + 0.5f - w * 0.5f) <= hw) inside[j * w + i] = true;
        }
        for(var j = 0; j < h; j++)
        {
            for(var i = 0; i < w; i++)
            {
                if(!inside[j * w + i]) continue;
                if(filled)
                {
                    FillPix(x0 + i, y0 + j, brush, color);
                    continue;
                }
                var edge = i == 0 || j == 0 || i == w - 1 || j == h - 1
                    || !inside[j * w + i - 1] || !inside[j * w + i + 1] || !inside[(j - 1) * w + i] || !inside[(j + 1) * w + i];
                if(edge) brush.Dab(x0 + i, y0 + j, size, color, plotAction);
            }
        }
    }

    static void DabLine(Vector2Int a, Vector2Int b, PixelBrush brush, int size, Color32 color)
    {
        brush.Dab(a.x, a.y, size, color, plotAction);
        pointBuffer.Clear();
        LinePoints(a, b, pointBuffer);
        for(var i = 0; i < pointBuffer.Count; i++)
            brush.Dab(pointBuffer[i].x, pointBuffer[i].y, size, color, plotAction);
    }

    static void FillPix(int x, int y, PixelBrush brush, Color32 color)
    {
        Color32 c;
        if(brush.FillPixel(x, y, color, out c)) Put(x, y, c);
    }

    public static RectInt FillBounds(PixelLayer target, Vector2Int seed, bool ignoreMask)
    {
        var canvas = target.Root;
        RectInt b;
        if(!ignoreMask && mask != null && !mask.outside)
        {
            b = mask.Bounds;
        }
        else
        {
            b = PixelCanvasEditing.CanvasBounds(canvas);
            if(b.width <= 0 || !b.Contains(seed))
            {
                var step = Mathf.Max(1, canvas.growStep);
                b = new RectInt(FloorDiv(seed.x, step) * step, FloorDiv(seed.y, step) * step, step, step);
            }
        }
        var x0 = b.xMin;
        var x1 = b.xMax;
        var y0 = b.yMin;
        var y1 = b.yMax;
        if(tileX)
        {
            x0 = Mathf.Max(x0, 0);
            x1 = Mathf.Min(x1, tileW);
        }
        if(tileY)
        {
            y0 = Mathf.Max(y0, 0);
            y1 = Mathf.Min(y1, tileH);
        }
        return new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
    }

    public static bool FloodRegion(PixelLayer target, Vector2Int seed, int tolerance, bool contiguous, bool ignoreMask, out bool[] region, out RectInt bounds)
    {
        Setup(target);
        region = null;
        bounds = FillBounds(target, seed, ignoreMask);
        if(bounds.width <= 0 || bounds.height <= 0 || !bounds.Contains(seed)) return false;
        var useMask = !ignoreMask && mask != null;
        var w = bounds.width;
        var h = bounds.height;
        var reg = new bool[w * h];
        region = reg;

        Vector2Int origin;
        int pw, ph;
        var pixels = PixelCanvasEditing.ReadPixels(target, out origin, out pw, out ph);
        var targetColor = At(pixels, origin, pw, ph, seed.x, seed.y);
        var xMin = bounds.xMin;
        var yMin = bounds.yMin;
        var xMax = bounds.xMax;
        var yMax = bounds.yMax;

        System.Func<int, int, bool> ok = (x, y) =>
        {
            if(reg[(y - yMin) * w + (x - xMin)]) return false;
            if(useMask && !mask.Contains(x, y)) return false;
            return Dist(At(pixels, origin, pw, ph, x, y), targetColor) <= tolerance;
        };

        if(!contiguous)
        {
            for(var y = yMin; y < yMax; y++)
                for(var x = xMin; x < xMax; x++)
                    if(ok(x, y)) reg[(y - yMin) * w + (x - xMin)] = true;
            return true;
        }

        var stack = new Stack<Vector2Int>();
        stack.Push(seed);
        while(stack.Count > 0)
        {
            var p = stack.Pop();
            if(!ok(p.x, p.y)) continue;
            var xl = p.x;
            while(xl - 1 >= xMin && ok(xl - 1, p.y)) xl--;
            var xr = p.x;
            while(xr + 1 < xMax && ok(xr + 1, p.y)) xr++;
            for(var i = xl; i <= xr; i++) reg[(p.y - yMin) * w + (i - xMin)] = true;
            for(var d = -1; d <= 1; d += 2)
            {
                var ny = p.y + d;
                if(ny < yMin || ny >= yMax) continue;
                var inRun = false;
                for(var nx = xl; nx <= xr; nx++)
                {
                    var good = ok(nx, ny);
                    if(good && !inRun)
                    {
                        stack.Push(new Vector2Int(nx, ny));
                        inRun = true;
                    }
                    else if(!good) inRun = false;
                }
            }
        }
        return true;
    }

    public static void Fill(PixelLayer target, Vector2Int seed, Color32 color, int tolerance, bool contiguous, PixelBrush brush)
    {
        bool[] region;
        RectInt b;
        if(!FloodRegion(target, seed, tolerance, contiguous, false, out region, out b)) return;
        for(var y = 0; y < b.height; y++)
        {
            for(var x = 0; x < b.width; x++)
            {
                if(!region[y * b.width + x]) continue;
                var px = b.xMin + x;
                var py = b.yMin + y;
                Color32 c = color;
                if(brush != null && !brush.FillPixel(px, py, color, out c)) continue;
                PutSolo(px, py, c);
            }
        }
    }

    public static void ClearSelection(PixelLayer target)
    {
        Setup(target);
        var rect = PixelCanvasEditing.Bounds(target);
        if(mask != null && !mask.outside)
        {
            var m = mask.Bounds;
            var x0 = Mathf.Max(rect.xMin, m.xMin);
            var y0 = Mathf.Max(rect.yMin, m.yMin);
            var x1 = Mathf.Min(rect.xMax, m.xMax);
            var y1 = Mathf.Min(rect.yMax, m.yMax);
            rect = new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
        }
        var clear = new Color32(0, 0, 0, 0);
        for(var y = rect.yMin; y < rect.yMax; y++)
            for(var x = rect.xMin; x < rect.xMax; x++)
                if(mask == null || mask.Contains(x, y)) PixelCanvasEditing.SetPixel(target, x, y, clear);
    }

    static Color32 At(Color32[] px, Vector2Int o, int pw, int ph, int x, int y)
    {
        var tx = x - o.x;
        var ty = y - o.y;
        if(px == null || tx < 0 || ty < 0 || tx >= pw || ty >= ph) return new Color32(0, 0, 0, 0);
        return px[ty * pw + tx];
    }

    static int Dist(Color32 a, Color32 b)
    {
        if(a.a == 0 && b.a == 0) return 0;
        var d = Mathf.Abs(a.a - b.a);
        d = Mathf.Max(d, Mathf.Abs(a.r - b.r));
        d = Mathf.Max(d, Mathf.Abs(a.g - b.g));
        d = Mathf.Max(d, Mathf.Abs(a.b - b.b));
        return d;
    }

    static int Mod(int v, int m)
    {
        var r = v % m;
        return r < 0 ? r + m : r;
    }

    static int FloorDiv(int value, int size)
    {
        var result = value / size;
        if(value % size < 0) result--;
        return result;
    }
}
#endif
