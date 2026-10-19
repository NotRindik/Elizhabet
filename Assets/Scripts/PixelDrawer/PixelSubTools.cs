#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PixelToolContext
{
    public PixelLayer layer;
    public Vector2Int pixel;
    public Vector2 pixelF;
    public bool shift;
    public bool ctrl;
}

public abstract class PixelSubTool
{
    public abstract PixelPaintMode Mode { get; }

    public virtual bool ModifiesPixels
    {
        get { return true; }
    }

    public virtual bool DrawsBrushCursor
    {
        get { return false; }
    }

    public virtual void MouseDown(PixelToolContext c) { }

    public virtual void MouseDrag(PixelToolContext c) { }

    public virtual void MouseUp(PixelToolContext c) { }

    public virtual void Cancel() { }

    public virtual void DrawOverlay(PixelLayer layer) { }

    protected static void Refresh()
    {
        PixelCanvasEditing.Apply();
        SceneView.RepaintAll();
    }

    protected static Vector3[] Quad(PixelLayer layer, float x0, float y0, float x1, float y1)
    {
        var ppu = layer.Root.pixelsPerUnit;
        var t = layer.transform;
        return new[]
        {
            t.TransformPoint(new Vector3(x0 / ppu, y0 / ppu)),
            t.TransformPoint(new Vector3(x1 / ppu, y0 / ppu)),
            t.TransformPoint(new Vector3(x1 / ppu, y1 / ppu)),
            t.TransformPoint(new Vector3(x0 / ppu, y1 / ppu))
        };
    }
}

public class PixelBrushTool : PixelSubTool
{
    readonly PixelPaintMode mode;
    Vector2Int last;
    bool erase;
    readonly List<Vector2Int> path = new List<Vector2Int>();
    readonly List<Color32> oldColors = new List<Color32>();
    readonly List<Vector2Int> segment = new List<Vector2Int>();

    public PixelBrushTool(PixelPaintMode mode)
    {
        this.mode = mode;
    }

    public override PixelPaintMode Mode
    {
        get { return mode; }
    }

    public override bool DrawsBrushCursor
    {
        get { return true; }
    }

    bool UsePerfect(PixelLayer layer, PixelBrush brush)
    {
        return PixelPaintState.PixelPerfect && PixelPaintState.Size == 1 && !(brush is PixelPatternBrush)
            && brush.spacing == 1 && !PixelPaintState.SymmetryX && !PixelPaintState.SymmetryY
            && layer.Root.tileMode == PixelCanvas.TileMode.Off;
    }

    Color32 CurrentColor()
    {
        return erase ? new Color32(0, 0, 0, 0) : (Color32)PixelPaintState.Color;
    }

    public override void MouseDown(PixelToolContext c)
    {
        erase = c.shift || mode == PixelPaintMode.Eraser;
        if(!erase) PixelPaintState.PushRecent(PixelPaintState.Color);
        PixelCanvasEditing.LimitHit = false;
        PixelHistory.BeginStroke(c.layer);
        last = c.pixel;
        var brush = PixelPaintState.Brush;
        if(UsePerfect(c.layer, brush))
        {
            path.Clear();
            oldColors.Clear();
            Perfect(c.layer, c.pixel);
        }
        else
        {
            PixelOps.Line(c.layer, c.pixel, c.pixel, brush, PixelPaintState.Size, CurrentColor(), true, false);
        }
        Refresh();
    }

    public override void MouseDrag(PixelToolContext c)
    {
        var brush = PixelPaintState.Brush;
        if(UsePerfect(c.layer, brush))
        {
            segment.Clear();
            PixelOps.LinePoints(last, c.pixel, segment);
            for(var i = 0; i < segment.Count; i++) Perfect(c.layer, segment[i]);
        }
        else
        {
            PixelOps.Line(c.layer, last, c.pixel, brush, PixelPaintState.Size, CurrentColor(), false, false);
        }
        last = c.pixel;
        Refresh();
    }

    public override void MouseUp(PixelToolContext c)
    {
        PixelHistory.EndStroke();
    }

    public override void Cancel()
    {
        PixelHistory.EndStroke();
    }

    void Perfect(PixelLayer layer, Vector2Int p)
    {
        var n = path.Count;
        if(n > 0 && path[n - 1] == p) return;
        if(n >= 2)
        {
            var a = path[n - 2];
            var b = path[n - 1];
            var d1 = b - a;
            var d2 = p - b;
            if(Mathf.Abs(d1.x) + Mathf.Abs(d1.y) == 1 && Mathf.Abs(d2.x) + Mathf.Abs(d2.y) == 1 && d1.x * d2.x + d1.y * d2.y == 0)
            {
                PixelCanvasEditing.SetPixel(layer, b.x, b.y, oldColors[n - 1]);
                path.RemoveAt(n - 1);
                oldColors.RemoveAt(n - 1);
            }
        }
        oldColors.Add(PixelCanvasEditing.GetColor(layer, p.x, p.y));
        path.Add(p);
        PixelOps.Setup(layer);
        PixelOps.Put(p.x, p.y, CurrentColor());
    }
}

public class PixelFillTool : PixelSubTool
{
    public override PixelPaintMode Mode
    {
        get { return PixelPaintMode.Fill; }
    }

    public override void MouseDown(PixelToolContext c)
    {
        var erase = c.shift;
        if(!erase) PixelPaintState.PushRecent(PixelPaintState.Color);
        var color = erase ? new Color32(0, 0, 0, 0) : (Color32)PixelPaintState.Color;
        PixelHistory.BeginStroke(c.layer);
        PixelOps.Fill(c.layer, c.pixel, color, PixelPaintState.Tolerance, PixelPaintState.Contiguous, PixelPaintState.Brush);
        Refresh();
        PixelHistory.EndStroke();
    }
}

public class PixelShapeTool : PixelSubTool
{
    readonly PixelPaintMode mode;
    Vector2Int start;
    bool open;

    public PixelShapeTool(PixelPaintMode mode)
    {
        this.mode = mode;
    }

    public override PixelPaintMode Mode
    {
        get { return mode; }
    }

    public override bool DrawsBrushCursor
    {
        get { return mode == PixelPaintMode.Line; }
    }

    public override void MouseDown(PixelToolContext c)
    {
        PixelPaintState.PushRecent(PixelPaintState.Color);
        PixelCanvasEditing.LimitHit = false;
        start = c.pixel;
        open = true;
        PixelHistory.BeginStroke(c.layer);
        Draw(c);
    }

    public override void MouseDrag(PixelToolContext c)
    {
        if(!open) return;
        PixelHistory.Rollback();
        Draw(c);
    }

    public override void MouseUp(PixelToolContext c)
    {
        if(!open) return;
        open = false;
        PixelHistory.Rollback();
        Draw(c);
        PixelHistory.EndStroke();
    }

    public override void Cancel()
    {
        if(!open) return;
        open = false;
        PixelHistory.Rollback();
        PixelHistory.Discard();
        Refresh();
    }

    void Draw(PixelToolContext c)
    {
        var end = c.pixel;
        var dx = end.x - start.x;
        var dy = end.y - start.y;
        if(c.shift)
        {
            if(mode == PixelPaintMode.Line)
            {
                var ax = Mathf.Abs(dx);
                var ay = Mathf.Abs(dy);
                if(ax > ay * 2) end.y = start.y;
                else if(ay > ax * 2) end.x = start.x;
                else
                {
                    var m = Mathf.Max(ax, ay);
                    end = new Vector2Int(start.x + (dx < 0 ? -m : m), start.y + (dy < 0 ? -m : m));
                }
            }
            else
            {
                var m = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                end = new Vector2Int(start.x + (dx < 0 ? -m : m), start.y + (dy < 0 ? -m : m));
            }
        }
        var brush = PixelPaintState.Brush;
        var size = PixelPaintState.Size;
        Color32 color = PixelPaintState.Color;
        var filled = PixelPaintState.ShapeFilled;
        switch(mode)
        {
            case PixelPaintMode.Line:
                PixelOps.Line(c.layer, start, end, brush, size, color, true, true);
                break;
            case PixelPaintMode.Rectangle:
                PixelOps.Rect(c.layer, start, end, brush, size, color, filled);
                break;
            default:
                PixelOps.Ellipse(c.layer, start, end, brush, size, color, filled);
                break;
        }
        Refresh();
    }
}

public class PixelSelectTool : PixelSubTool
{
    readonly PixelPaintMode mode;
    Vector2Int start;
    Vector2Int current;
    bool dragging;
    bool moved;
    readonly List<Vector2> lasso = new List<Vector2>();

    public PixelSelectTool(PixelPaintMode mode)
    {
        this.mode = mode;
    }

    public override PixelPaintMode Mode
    {
        get { return mode; }
    }

    public override bool ModifiesPixels
    {
        get { return false; }
    }

    static PixelSelectMode ModeFor(PixelToolContext c)
    {
        if(c.shift && c.ctrl) return PixelSelectMode.Intersect;
        if(c.shift) return PixelSelectMode.Add;
        if(c.ctrl) return PixelSelectMode.Subtract;
        return PixelPaintState.SelectMode;
    }

    public override void MouseDown(PixelToolContext c)
    {
        start = c.pixel;
        current = c.pixel;
        moved = false;
        dragging = true;
        if(mode == PixelPaintMode.SelectLasso)
        {
            lasso.Clear();
            lasso.Add(c.pixelF);
        }
        else if(mode == PixelPaintMode.SelectWand)
        {
            dragging = false;
            bool[] region;
            RectInt bounds;
            if(PixelOps.FloodRegion(c.layer, c.pixel, PixelPaintState.Tolerance, PixelPaintState.Contiguous, true, out region, out bounds))
                PixelSelection.Apply(c.layer.Root, PixelMask.FromRegion(bounds, region), ModeFor(c));
        }
    }

    public override void MouseDrag(PixelToolContext c)
    {
        if(!dragging) return;
        current = c.pixel;
        if(current != start) moved = true;
        if(mode == PixelPaintMode.SelectLasso)
        {
            if(lasso.Count == 0 || (lasso[lasso.Count - 1] - c.pixelF).sqrMagnitude > 0.25f) lasso.Add(c.pixelF);
        }
        SceneView.RepaintAll();
    }

    public override void MouseUp(PixelToolContext c)
    {
        if(!dragging) return;
        dragging = false;
        var canvas = c.layer.Root;
        var selectMode = ModeFor(c);
        if(mode == PixelPaintMode.SelectRect)
        {
            if(!moved)
            {
                if(selectMode == PixelSelectMode.Replace) PixelSelection.Clear(canvas);
                return;
            }
            PixelSelection.Apply(canvas, PixelMask.Rect(start.x, start.y, current.x, current.y), selectMode);
        }
        else if(mode == PixelPaintMode.SelectLasso)
        {
            if(lasso.Count < 3)
            {
                if(selectMode == PixelSelectMode.Replace) PixelSelection.Clear(canvas);
                return;
            }
            PixelSelection.Apply(canvas, PixelMask.Polygon(lasso), selectMode);
        }
    }

    public override void Cancel()
    {
        dragging = false;
    }

    public override void DrawOverlay(PixelLayer layer)
    {
        if(!dragging) return;
        if(mode == PixelPaintMode.SelectRect)
        {
            var x0 = Mathf.Min(start.x, current.x);
            var y0 = Mathf.Min(start.y, current.y);
            var x1 = Mathf.Max(start.x, current.x) + 1;
            var y1 = Mathf.Max(start.y, current.y) + 1;
            Handles.DrawSolidRectangleWithOutline(Quad(layer, x0, y0, x1, y1), new Color(1, 1, 1, 0.08f), Color.white);
        }
        else if(mode == PixelPaintMode.SelectLasso && lasso.Count > 1)
        {
            var ppu = layer.Root.pixelsPerUnit;
            var pts = new Vector3[lasso.Count + 1];
            for(var i = 0; i < lasso.Count; i++)
                pts[i] = layer.transform.TransformPoint(new Vector3(lasso[i].x / ppu, lasso[i].y / ppu, 0));
            pts[lasso.Count] = pts[0];
            Handles.color = Color.white;
            Handles.DrawAAPolyLine(2f, pts);
        }
    }
}

public class PixelMoveTool : PixelSubTool
{
    bool shifting;
    Vector2Int startPixel;
    Vector2Int total;
    PixelLayer layer;

    public override PixelPaintMode Mode
    {
        get { return PixelPaintMode.Move; }
    }

    public override void MouseDown(PixelToolContext c)
    {
        layer = c.layer;
        var mask = PixelSelection.For(layer.Root);
        if(mask == null)
        {
            var b = PixelCanvasEditing.Bounds(layer);
            if(b.width <= 0 || b.height <= 0) return;
            shifting = true;
            startPixel = c.pixel;
            total = Vector2Int.zero;
            return;
        }
        if(PixelFloating.BeginFromLayer(layer)) PixelFloating.BeginMoveDrag(c.pixelF);
    }

    public override void MouseDrag(PixelToolContext c)
    {
        if(!shifting) return;
        var d = c.pixel - startPixel;
        var step = d - total;
        if(step == Vector2Int.zero) return;
        PixelCanvasEditing.ShiftOrigin(layer, step.x, step.y);
        total = d;
        SceneView.RepaintAll();
    }

    public override void MouseUp(PixelToolContext c)
    {
        if(!shifting) return;
        shifting = false;
        PixelHistory.RecordShift(layer, total.x, total.y);
    }

    public override void Cancel()
    {
        if(!shifting) return;
        shifting = false;
        PixelHistory.RecordShift(layer, total.x, total.y);
    }
}
#endif
