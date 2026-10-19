#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PixelFloating
{
    enum Part { None, Move, Scale, Rotate }

    const float MaxScale = 64f;

    static Color32[] src;
    static bool[] srcMask;
    static int sw;
    static int sh;
    static Vector2 center;
    static float angle;
    static Vector2 scale = Vector2.one;
    static Color32[] outPixels;
    static bool[] outMask;
    static RectInt outRect;
    static bool resultSelection;
    static SpriteRenderer preview;
    static Sprite previewSprite;
    static Texture2D previewTexture;

    static Part dragPart;
    static Vector2 dragStartMouse;
    static Vector2 dragStartCenter;
    static Vector2 dragStartScale;
    static float dragStartAngle;
    static int dragCorner;

    public static bool Active { get; private set; }
    public static PixelLayer Layer { get; private set; }

    public static float Angle
    {
        get { return angle; }
    }

    public static Vector2 Scale
    {
        get { return scale; }
    }

    static PixelFloating()
    {
        PixelHistory.Interrupted += Abort;
    }

    public static bool BeginFromLayer(PixelLayer layer)
    {
        CommitIfActive();
        var canvas = layer.Root;
        var mask = PixelSelection.For(canvas);
        var rect = PixelCanvasEditing.Bounds(layer);
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

        Vector2Int o;
        int pw, ph;
        var pixels = PixelCanvasEditing.ReadPixels(layer, out o, out pw, out ph);
        if(pixels == null) return false;

        sw = rect.width;
        sh = rect.height;
        src = new Color32[sw * sh];
        srcMask = new bool[sw * sh];
        PixelHistory.BeginStroke(layer);
        var clear = new Color32(0, 0, 0, 0);
        for(var y = 0; y < sh; y++)
        {
            for(var x = 0; x < sw; x++)
            {
                var wx = rect.xMin + x;
                var wy = rect.yMin + y;
                if(mask != null && !mask.Contains(wx, wy)) continue;
                var c = pixels[(wy - o.y) * pw + (wx - o.x)];
                src[y * sw + x] = c;
                srcMask[y * sw + x] = true;
                if(c.a != 0) PixelCanvasEditing.SetPixel(layer, wx, wy, clear);
            }
        }
        PixelCanvasEditing.Apply();
        resultSelection = mask != null;
        Init(layer, new Vector2(rect.xMin + sw * 0.5f, rect.yMin + sh * 0.5f));
        return true;
    }

    public static void BeginPaste(PixelLayer layer, Color32[] data, int w, int h)
    {
        CommitIfActive();
        sw = w;
        sh = h;
        src = (Color32[])data.Clone();
        srcMask = new bool[w * h];
        for(var i = 0; i < srcMask.Length; i++) srcMask[i] = true;

        var view = SceneView.lastActiveSceneView;
        var world = view != null ? view.pivot : layer.transform.position;
        var local = layer.transform.InverseTransformPoint(world);
        var ppu = layer.Root.pixelsPerUnit;
        var cx = Mathf.Round(local.x * ppu - w * 0.5f) + w * 0.5f;
        var cy = Mathf.Round(local.y * ppu - h * 0.5f) + h * 0.5f;

        PixelHistory.BeginStroke(layer);
        resultSelection = true;
        Init(layer, new Vector2(cx, cy));
    }

    static void Init(PixelLayer layer, Vector2 c)
    {
        Layer = layer;
        center = c;
        angle = 0;
        scale = Vector2.one;
        dragPart = Part.None;
        Active = true;
        Rebuild();
    }

    public static void BeginMoveDrag(Vector2 mousePixel)
    {
        if(!Active) return;
        dragPart = Part.Move;
        dragStartMouse = mousePixel;
        dragStartCenter = center;
    }

    public static void CommitIfActive()
    {
        if(Active) Commit();
    }

    public static void Commit()
    {
        if(!Active) return;
        var layer = Layer;
        if(layer != null && layer.Root != null)
        {
            for(var y = 0; y < outRect.height; y++)
            {
                for(var x = 0; x < outRect.width; x++)
                {
                    var i = y * outRect.width + x;
                    if(!outMask[i]) continue;
                    var s = outPixels[i];
                    if(s.a == 0) continue;
                    var wx = outRect.xMin + x;
                    var wy = outRect.yMin + y;
                    var result = s;
                    if(s.a != 255) result = Over(s, PixelCanvasEditing.GetColor(layer, wx, wy));
                    PixelCanvasEditing.SetPixel(layer, wx, wy, result);
                }
            }
            if(resultSelection && outRect.width > 0 && outRect.height > 0)
            {
                var m = new PixelMask(outRect.xMin, outRect.yMin, outRect.width, outRect.height, false);
                System.Array.Copy(outMask, m.bits, m.bits.Length);
                PixelSelection.Set(layer.Root, m, true);
            }
            PixelCanvasEditing.Apply();
        }
        PixelHistory.EndStroke();
        Cleanup();
    }

    public static void Cancel()
    {
        if(!Active) return;
        PixelHistory.Rollback();
        PixelHistory.Discard();
        Cleanup();
    }

    static void Abort()
    {
        if(Active) Cleanup();
    }

    static void Cleanup()
    {
        DestroyPreview();
        Active = false;
        Layer = null;
        src = null;
        srcMask = null;
        outPixels = null;
        outMask = null;
        dragPart = Part.None;
        SceneView.RepaintAll();
    }

    static void DestroyPreview()
    {
        if(preview != null) Object.DestroyImmediate(preview.gameObject);
        if(previewSprite != null) Object.DestroyImmediate(previewSprite);
        if(previewTexture != null) Object.DestroyImmediate(previewTexture);
        preview = null;
        previewSprite = null;
        previewTexture = null;
    }

    public static void FlipHorizontal()
    {
        scale.x = -scale.x;
        Rebuild();
    }

    public static void FlipVertical()
    {
        scale.y = -scale.y;
        Rebuild();
    }

    public static void Rotate90(bool clockwise)
    {
        SetAngle(angle + (clockwise ? -90f : 90f));
    }

    public static void SetAngle(float value)
    {
        angle = Mathf.Repeat(value + 180f, 360f) - 180f;
        Rebuild();
    }

    public static void SetScale(Vector2 value)
    {
        scale = new Vector2(Clamp(value.x), Clamp(value.y));
        Rebuild();
    }

    public static void MultiplyScale(float factor)
    {
        SetScale(scale * factor);
    }

    public static void Nudge(int dx, int dy)
    {
        center += new Vector2(dx, dy);
        Rebuild();
    }

    static float Clamp(float v)
    {
        var sign = v < 0 ? -1f : 1f;
        return sign * Mathf.Clamp(Mathf.Abs(v), 0.05f, MaxScale);
    }

    static void Rebuild()
    {
        var rad = angle * Mathf.Deg2Rad;
        var cos = System.Math.Cos(rad);
        var sin = System.Math.Sin(rad);
        double hw = sw * 0.5;
        double hh = sh * 0.5;

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        for(var k = 0; k < 4; k++)
        {
            var cx = ((k & 1) == 0 ? -hw : hw) * scale.x;
            var cy = ((k & 2) == 0 ? -hh : hh) * scale.y;
            var px = cx * cos - cy * sin + center.x;
            var py = cx * sin + cy * cos + center.y;
            minX = System.Math.Min(minX, px);
            maxX = System.Math.Max(maxX, px);
            minY = System.Math.Min(minY, py);
            maxY = System.Math.Max(maxY, py);
        }
        var x0 = (int)System.Math.Floor(minX);
        var y0 = (int)System.Math.Floor(minY);
        var x1 = (int)System.Math.Ceiling(maxX);
        var y1 = (int)System.Math.Ceiling(maxY);
        outRect = new RectInt(x0, y0, Mathf.Max(1, x1 - x0), Mathf.Max(1, y1 - y0));
        outPixels = new Color32[outRect.width * outRect.height];
        outMask = new bool[outRect.width * outRect.height];

        for(var y = 0; y < outRect.height; y++)
        {
            for(var x = 0; x < outRect.width; x++)
            {
                var vx = x0 + x + 0.5 - center.x;
                var vy = y0 + y + 0.5 - center.y;
                var ux = (vx * cos + vy * sin) / scale.x;
                var uy = (-vx * sin + vy * cos) / scale.y;
                var ix = (int)System.Math.Floor(ux + hw + 1e-4);
                var iy = (int)System.Math.Floor(uy + hh + 1e-4);
                if(ix < 0 || iy < 0 || ix >= sw || iy >= sh) continue;
                var si = iy * sw + ix;
                if(!srcMask[si]) continue;
                outPixels[y * outRect.width + x] = src[si];
                outMask[y * outRect.width + x] = true;
            }
        }
        RefreshPreview();
        SceneView.RepaintAll();
    }

    static void RefreshPreview()
    {
        if(Layer == null) return;
        if(previewSprite != null) Object.DestroyImmediate(previewSprite);
        if(previewTexture != null) Object.DestroyImmediate(previewTexture);
        previewTexture = new Texture2D(outRect.width, outRect.height, TextureFormat.RGBA32, false);
        previewTexture.hideFlags = HideFlags.HideAndDontSave;
        previewTexture.filterMode = FilterMode.Point;
        previewTexture.wrapMode = TextureWrapMode.Clamp;
        var shown = new Color32[outPixels.Length];
        for(var i = 0; i < shown.Length; i++) if(outMask[i]) shown[i] = outPixels[i];
        previewTexture.SetPixels32(shown);
        previewTexture.Apply(false);
        var ppu = Layer.Root.pixelsPerUnit;
        previewSprite = Sprite.Create(previewTexture, new Rect(0, 0, outRect.width, outRect.height), Vector2.zero, ppu, 0, SpriteMeshType.FullRect);
        previewSprite.hideFlags = HideFlags.HideAndDontSave;
        if(preview == null) preview = PixelTempView.CreateRenderer(Layer.transform, "PixelFloat", "float", 30000);
        preview.sprite = previewSprite;
        preview.color = new Color(1, 1, 1, Layer.opacity);
        preview.transform.localPosition = new Vector3(outRect.xMin, outRect.yMin, 0) / ppu;
    }

    static Color32 Over(Color32 s, Color32 d)
    {
        var a = s.a / 255f;
        var ba = d.a / 255f;
        var oa = a + ba * (1 - a);
        if(oa <= 0) return new Color32(0, 0, 0, 0);
        var r = (s.r * a + d.r * ba * (1 - a)) / oa;
        var g = (s.g * a + d.g * ba * (1 - a)) / oa;
        var b = (s.b * a + d.b * ba * (1 - a)) / oa;
        return new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(r), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(g), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(b), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(oa * 255f), 0, 255));
    }

    static Vector2 Rot(Vector2 v, float deg)
    {
        var r = deg * Mathf.Deg2Rad;
        var c = Mathf.Cos(r);
        var s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    static Vector3 ToWorld(PixelLayer layer, Vector2 pixel)
    {
        var ppu = layer.Root.pixelsPerUnit;
        return layer.transform.TransformPoint(new Vector3(pixel.x / ppu, pixel.y / ppu, 0));
    }

    static Vector2 CornerPixel(int k)
    {
        var cx = ((k & 1) == 0 ? -0.5f : 0.5f) * sw * scale.x;
        var cy = ((k & 2) == 0 ? -0.5f : 0.5f) * sh * scale.y;
        return center + Rot(new Vector2(cx, cy), angle);
    }

    static Vector2 RotateHandlePixel(PixelLayer layer)
    {
        var top = center + Rot(new Vector2(0, 0.5f * sh * Mathf.Abs(scale.y)), angle);
        var world = ToWorld(layer, top);
        var extra = HandleUtility.GetHandleSize(world) * 0.45f * layer.Root.pixelsPerUnit;
        return center + Rot(new Vector2(0, 0.5f * sh * Mathf.Abs(scale.y) + extra), angle);
    }

    static Vector2 GuiOf(PixelLayer layer, Vector2 pixel)
    {
        return HandleUtility.WorldToGUIPoint(ToWorld(layer, pixel));
    }

    static Vector2 MousePixel(PixelLayer layer, Vector2 gui)
    {
        var ray = HandleUtility.GUIPointToWorldRay(gui);
        var local = layer.transform.InverseTransformPoint(ray.origin);
        var ppu = layer.Root.pixelsPerUnit;
        return new Vector2(local.x * ppu, local.y * ppu);
    }

    static bool Inside(Vector2 pixel)
    {
        var v = Rot(pixel - center, -angle);
        return Mathf.Abs(v.x) <= 0.5f * sw * Mathf.Abs(scale.x) && Mathf.Abs(v.y) <= 0.5f * sh * Mathf.Abs(scale.y);
    }

    public static bool HandleEvent(PixelLayer layer, Event e, int id, bool inPanel)
    {
        if(!Active || Layer != layer) return false;
        switch(e.type)
        {
            case EventType.MouseDown:
                if(e.button != 0 || inPanel || HandleUtility.nearestControl != id) return false;
                GUIUtility.hotControl = id;
                var mp = MousePixel(layer, e.mousePosition);
                dragPart = Part.None;
                if(Vector2.Distance(GuiOf(layer, RotateHandlePixel(layer)), e.mousePosition) < 10)
                {
                    dragPart = Part.Rotate;
                }
                else
                {
                    for(var k = 0; k < 4; k++)
                    {
                        if(Vector2.Distance(GuiOf(layer, CornerPixel(k)), e.mousePosition) >= 10) continue;
                        dragPart = Part.Scale;
                        dragCorner = k;
                        break;
                    }
                }
                if(dragPart == Part.None && Inside(mp)) dragPart = Part.Move;
                dragStartMouse = mp;
                dragStartCenter = center;
                dragStartScale = scale;
                dragStartAngle = angle;
                e.Use();
                return true;
            case EventType.MouseDrag:
                if(GUIUtility.hotControl != id || dragPart == Part.None) return false;
                DragTo(layer, e, MousePixel(layer, e.mousePosition));
                e.Use();
                return true;
            case EventType.MouseUp:
                if(GUIUtility.hotControl != id) return false;
                GUIUtility.hotControl = 0;
                dragPart = Part.None;
                e.Use();
                return true;
        }
        return false;
    }

    static void DragTo(PixelLayer layer, Event e, Vector2 mp)
    {
        switch(dragPart)
        {
            case Part.Move:
                var d = mp - dragStartMouse;
                center = dragStartCenter + new Vector2(Mathf.Round(d.x), Mathf.Round(d.y));
                break;
            case Part.Rotate:
                var a0 = Mathf.Atan2(dragStartMouse.y - dragStartCenter.y, dragStartMouse.x - dragStartCenter.x);
                var a1 = Mathf.Atan2(mp.y - center.y, mp.x - center.x);
                var next = dragStartAngle + (a1 - a0) * Mathf.Rad2Deg;
                if(e.shift) next = Mathf.Round(next / 15f) * 15f;
                angle = Mathf.Repeat(next + 180f, 360f) - 180f;
                break;
            case Part.Scale:
                var s = dragStartScale;
                if(e.control || e.command)
                {
                    var u0 = Rot(dragStartMouse - center, -angle);
                    var u1 = Rot(mp - center, -angle);
                    var rx = Mathf.Abs(u0.x) > 0.01f ? Mathf.Abs(u1.x) / Mathf.Abs(u0.x) : 1f;
                    var ry = Mathf.Abs(u0.y) > 0.01f ? Mathf.Abs(u1.y) / Mathf.Abs(u0.y) : 1f;
                    s = new Vector2(dragStartScale.x * rx, dragStartScale.y * ry);
                }
                else
                {
                    var d0 = (dragStartMouse - center).magnitude;
                    var d1 = (mp - center).magnitude;
                    var ratio = d0 > 0.01f ? d1 / d0 : 1f;
                    if(e.shift)
                    {
                        var snapped = Mathf.Max(0.5f, Mathf.Round(Mathf.Abs(dragStartScale.x) * ratio * 2f) / 2f);
                        ratio = snapped / Mathf.Max(0.0001f, Mathf.Abs(dragStartScale.x));
                    }
                    s = dragStartScale * ratio;
                }
                scale = new Vector2(Clamp(s.x), Clamp(s.y));
                break;
        }
        Rebuild();
    }

    public static void DrawGUI(PixelLayer layer)
    {
        if(!Active || Layer != layer) return;
        var verts = new Vector3[4];
        verts[0] = ToWorld(layer, CornerPixel(0));
        verts[1] = ToWorld(layer, CornerPixel(1));
        verts[2] = ToWorld(layer, CornerPixel(3));
        verts[3] = ToWorld(layer, CornerPixel(2));
        Handles.DrawSolidRectangleWithOutline(verts, Color.clear, Color.white);
        var normal = layer.transform.forward;
        for(var k = 0; k < 4; k++)
        {
            var p = ToWorld(layer, CornerPixel(k));
            Handles.color = Color.black;
            Handles.DrawSolidDisc(p, normal, HandleUtility.GetHandleSize(p) * 0.07f);
            Handles.color = Color.white;
            Handles.DrawSolidDisc(p, normal, HandleUtility.GetHandleSize(p) * 0.05f);
        }
        var top = ToWorld(layer, center + Rot(new Vector2(0, 0.5f * sh * Mathf.Abs(scale.y)), angle));
        var rot = ToWorld(layer, RotateHandlePixel(layer));
        Handles.DrawLine(top, rot);
        Handles.color = new Color(0.3f, 0.8f, 1f);
        Handles.DrawSolidDisc(rot, normal, HandleUtility.GetHandleSize(rot) * 0.06f);
        Handles.color = Color.white;
    }
}
#endif
