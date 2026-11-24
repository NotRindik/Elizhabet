#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.InputSystem;

[EditorTool("Pixel Paint", typeof(PixelLayer))]
public class PixelPaintTool : EditorTool
{
    static PixelBrush cursorBrush;
    static int cursorSize = -1;
    static bool cursorFull;
    static readonly System.Collections.Generic.List<Vector2> cursorEdges = new System.Collections.Generic.List<Vector2>();
    static Vector3[] cursorPoints = new Vector3[0];
    static int cursorVersion = -1;
    static SerializedObject brushObject;

    static readonly PixelSubTool[] tools =
    {
        new PixelBrushTool(PixelPaintMode.Brush),
        new PixelBrushTool(PixelPaintMode.Eraser),
        new PixelFillTool(),
        new PixelShapeTool(PixelPaintMode.Line),
        new PixelShapeTool(PixelPaintMode.Rectangle),
        new PixelShapeTool(PixelPaintMode.Ellipse),
        new PixelSelectTool(PixelPaintMode.SelectRect),
        new PixelSelectTool(PixelPaintMode.SelectLasso),
        new PixelSelectTool(PixelPaintMode.SelectWand),
        new PixelMoveTool()
    };

    PixelSubTool active;
    PixelToolContext activeContext;
    int activeSize;
    bool picking;
    PixelPickSource pickSource;

    const double PickBounceTime = 0.25;

    static bool altHeld;
    static double lastPickRelease = -1;
    static bool ctrlHeld;
    static bool shiftHeld;
    static Color32 previewColor;
    static bool previewFound;
    static PixelPickSource previewSource;
    static bool unityEyeDropper;
    static bool saveQueued;
    static EditorWindow paintView;

    public static void RepaintPaintView()
    {
        if(paintView != null) paintView.Repaint();
        else SceneView.RepaintAll();
    }

    public override GUIContent toolbarIcon
    {
        get { return EditorGUIUtility.IconContent("Grid.PaintTool"); }
    }

    public static PixelSubTool CurrentSubTool
    {
        get { return tools[(int)PixelPaintState.Mode]; }
    }

    public override void OnActivated()
    {
        PixelDrawerAssets.Ensure();
        PixelDesktopPicker.Enable();
    }

    public override void OnWillBeDeactivated()
    {
        if(active != null)
        {
            active.Cancel();
            active = null;
        }
        PixelDesktopPicker.Disable();
        PixelFloating.CommitIfActive();
        PixelTempView.ClearTiles();
        picking = false;
        unityEyeDropper = false;
        if(PixelDrawerAssets.Settings.saveOnToolExit && !saveQueued)
        {
            saveQueued = true;
            EditorApplication.delayCall += SaveAfterExit;
        }
    }

    static void SaveAfterExit()
    {
        saveQueued = false;
        if(PixelHistory.IsOpen) return;
        PixelCanvasEditing.SaveAll();
    }

    public override void OnToolGUI(EditorWindow window)
    {
        var layer = target as PixelLayer;
        if(layer == null || layer.Root == null) return;
        var brush = PixelPaintState.Brush;
        if(brush == null) return;
        var e = Event.current;
        paintView = window;

        if((e.type == EventType.DragUpdated || e.type == EventType.DragPerform) && PixelImageImport.HasImages())
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if(e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                var images = PixelImageImport.FromDrag();
                var center = ToPixelF(layer, e.mousePosition);
                var dropCanvas = layer.Root;
                var dropAbove = layer;
                EditorApplication.delayCall += () => PixelImageImport.PasteAsLayers(dropCanvas, dropAbove, images, center);
            }
            e.Use();
            return;
        }

        if(PixelFloating.Active && PixelFloating.Layer != layer) PixelFloating.Commit();
        PixelDesktopPicker.Enable();

        TrackModifiers(window, e);

        if((altHeld || picking) && !unityEyeDropper && (e.isMouse || e.isKey))
        {
            var src = picking ? pickSource : (PixelPaintHotkeys.LayerPickHeld ? PixelPickSource.Layers : PixelPickSource.Screen);
            UpdatePreview(layer, e.mousePosition, src);
            if(e.type == EventType.MouseMove || e.isKey) window.Repaint();
        }

        var id = GUIUtility.GetControlID(FocusType.Passive);
        var canvasSize = PixelCanvasEditing.Size(layer);

        if(e.type == EventType.Layout) PixelTempView.SyncTiles(layer, true);

        window.wantsMouseMove = true;
        if(e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

        var pixel = ToPixel(layer, e.mousePosition);
        var pixelF = ToPixelF(layer, e.mousePosition);
        var type = e.type;

        if(active != null && activeContext != null && PixelPaintState.Size != activeSize && type != EventType.Layout)
        {
            activeSize = PixelPaintState.Size;
            active.Redraw(activeContext);
        }

        var consumed = PixelFloating.HandleEvent(layer, e, id, false);
        if(consumed)
        {
            if(type == EventType.MouseUp) active = null;
        }
        else if(type == EventType.MouseDown && e.button == 0 && HandleUtility.nearestControl == id)
        {
            unityEyeDropper = false;
            GUIUtility.hotControl = id;
            if(!e.alt) PixelPaintHotkeys.LayerPickHeld = false;
            if(e.alt)
            {
                if(EditorApplication.timeSinceStartup - lastPickRelease >= PickBounceTime)
                {
                    picking = true;
                    pickSource = PixelPaintHotkeys.LayerPickHeld ? PixelPickSource.Layers : PixelPickSource.Screen;
                    PickColorAt(layer, e.mousePosition, pickSource);
                }
            }
            else if(CtrlSelectsLayer(e, layer))
            {
                SelectLayerAt(layer, e.mousePosition);
            }
            else
            {
                var sub = CurrentSubTool;
                if(layer.IsCorrection && sub.ModifiesPixels)
                {
                    window.ShowNotification(new GUIContent("Correction layer: no pixels to paint. Double-click it in Pixel Layers to edit."));
                }
                else if(layer.locked && sub.ModifiesPixels)
                {
                    window.ShowNotification(new GUIContent("Layer is locked"));
                }
                else
                {
                    active = sub;
                    activeContext = MakeContext(layer, e, pixel, pixelF);
                    activeSize = PixelPaintState.Size;
                    active.MouseDown(activeContext);
                }
            }
            e.Use();
        }
        else if(type == EventType.MouseDrag && GUIUtility.hotControl == id && (active != null || picking))
        {
            if(picking) PickColorAt(layer, e.mousePosition, pickSource);
            else
            {
                activeContext = MakeContext(layer, e, pixel, pixelF);
                active.MouseDrag(activeContext);
            }
            e.Use();
        }
        else if(type == EventType.MouseUp && GUIUtility.hotControl == id)
        {
            GUIUtility.hotControl = 0;
            if(active != null)
            {
                active.MouseUp(MakeContext(layer, e, pixel, pixelF));
                active = null;
                PixelCanvasEditing.Apply();
                SceneView.RepaintAll();
            }
            if(picking) lastPickRelease = EditorApplication.timeSinceStartup;
            picking = false;
            e.Use();
        }
        else if(type == EventType.MouseMove) window.Repaint();

        if(e.type == EventType.Repaint && !unityEyeDropper)
        {
            var pickerActive = altHeld || picking;
            var source = picking ? pickSource : (PixelPaintHotkeys.LayerPickHeld ? PixelPickSource.Layers : PixelPickSource.Screen);
            if(!pickerActive)
            {
                DrawBounds(layer, canvasSize);
                DrawTileBounds(layer);
                DrawAxes(layer);
                PixelSelection.Draw(layer);
                PixelFloating.DrawGUI(layer);
                if(!PixelFloating.Active)
                {
                    var sub = CurrentSubTool;
                    sub.DrawOverlay(layer);
                    if(PixelDrawerAssets.Settings.BrushLineHeld(shiftHeld, ctrlHeld) && active == null && sub is PixelBrushTool) ((PixelBrushTool)sub).DrawLinePreview(layer, pixel);
                    if(sub.DrawsBrushCursor) DrawCursor(layer, pixel, brush);
                    else DrawPickerCursor(layer, pixel);
                }
            }
            else
            {
                if(source == PixelPickSource.Layers) DrawPickerCursor(layer, pixel);
                DrawSwatch(e.mousePosition);
            }
        }
    }

    static PixelToolContext MakeContext( PixelLayer layer, Event e, Vector2Int pixel, Vector2 pixelF)
    {
        var pen = Pen.current;
        var penPressure = pen != null ? pen.pressure.ReadValue() : 0f;
        var isPen = e.pointerType == PointerType.Pen || (pen != null && (pen.tip.isPressed || pen.inRange.isPressed || penPressure > 0f));
        var pressure = isPen ? penPressure : 1f;

        return new PixelToolContext
        {
            layer = layer,
            pixel = pixel,
            pixelF = pixelF,
            shift = e.shift,
            ctrl = e.control || e.command,
            line = PixelDrawerAssets.Settings.BrushLineHeld(e.shift, e.control || e.command),
            pressure = pressure,
        };
    }
    static void TrackModifiers(EditorWindow window, Event e)
    {
        if(e.isMouse || e.isKey)
        {
            altHeld = e.alt;
            ctrlHeld = e.control || e.command;
            shiftHeld = e.shift;
        }
        if(e.isKey)
        {
            if(e.keyCode == KeyCode.LeftAlt || e.keyCode == KeyCode.RightAlt) altHeld = e.type == EventType.KeyDown;
            if(e.keyCode == KeyCode.LeftShift || e.keyCode == KeyCode.RightShift) shiftHeld = e.type == EventType.KeyDown;
            if(e.keyCode == KeyCode.LeftControl || e.keyCode == KeyCode.RightControl || e.keyCode == KeyCode.LeftCommand || e.keyCode == KeyCode.RightCommand) ctrlHeld = e.type == EventType.KeyDown;
            window.Repaint();
        }
        if(e.type == EventType.ExecuteCommand)
        {
            var name = e.commandName;
            if(name == "EyeDropperUpdate")
            {
                unityEyeDropper = true;
                window.Repaint();
            }
            else if(name == "EyeDropperClicked" || name == "EyeDropperCancelled")
            {
                unityEyeDropper = false;
                window.Repaint();
            }
        }
    }

    static bool BrushLineReady(PixelLayer layer)
    {
        var mode = PixelPaintState.Mode;
        return (mode == PixelPaintMode.Brush || mode == PixelPaintMode.Eraser) && PixelBrushTool.HasAnchor(layer);
    }

    static bool CtrlSelectsLayer(Event e, PixelLayer layer)
    {
        if(!(e.control || e.command) || !PixelDrawerAssets.Settings.ctrlClickSelectsLayer) return false;
        if(PixelDrawerAssets.Settings.brushLineModifier == PixelLineModifier.Ctrl && BrushLineReady(layer)) return false;
        var mode = PixelPaintState.Mode;
        return mode != PixelPaintMode.SelectRect && mode != PixelPaintMode.SelectLasso && mode != PixelPaintMode.SelectWand;
    }

    static void SelectLayerAt(PixelLayer layer, Vector2 guiPosition)
    {
        var layers = layer.Root.GetComponentsInChildren<PixelLayer>(false);
        System.Array.Sort(layers, (a, b) => b.sortIndex.CompareTo(a.sortIndex));
        for(var i = 0; i < layers.Length; i++)
        {
            var other = layers[i];
            if(!other.visible || other.opacity <= 0) continue;
            var p = ToPixel(other, guiPosition);
            Color32 c;
            if(!PixelCanvasEditing.TryGetPixel(other, p.x, p.y, out c) || c.a == 0) continue;
            if(other.clipped && !ClipBaseCovers(layers, i, guiPosition)) continue;
            if(other != layer) Selection.activeGameObject = other.gameObject;
            return;
        }
    }

    static bool ClipBaseCovers(PixelLayer[] layers, int index, Vector2 guiPosition)
    {
        for(var j = index + 1; j < layers.Length; j++)
        {
            var below = layers[j];
            if(below.clipped) continue;
            if(!below.visible) return false;
            var p = ToPixel(below, guiPosition);
            Color32 c;
            return PixelCanvasEditing.TryGetPixel(below, p.x, p.y, out c) && c.a > 2;
        }
        return true;
    }

    public static void PickColorAt(PixelLayer layer, Vector2 guiPosition, PixelPickSource source)
    {
        if(layer == null || layer.Root == null) return;
        UpdatePreview(layer, guiPosition, source);
        if(!previewFound) return;
        ApplyPickedColor(previewColor);
    }

    public static void ApplyPickedColor(Color32 color)
    {
        PixelPaintState.Color = color;
        PixelPaintState.PushRecent(color);
        if(PixelPaintState.Mode == PixelPaintMode.Eraser || PixelPaintState.Mode == PixelPaintMode.Move || PixelPaintState.Mode >= PixelPaintMode.SelectRect)
            PixelPaintState.Mode = PixelPaintMode.Brush;
        SceneView.RepaintAll();
    }

    public static void UpdatePreview(PixelLayer layer, Vector2 guiPosition, PixelPickSource source)
    {
        previewSource = source;
        previewFound = Sample(layer, guiPosition, source, out previewColor);
    }

    static bool Sample(PixelLayer layer, Vector2 guiPosition, PixelPickSource source, out Color32 color)
    {
        return source == PixelPickSource.Screen ? SampleScreen(guiPosition, out color) : SampleLayers(layer, guiPosition, out color);
    }

    static bool SampleScreen(Vector2 guiPosition, out Color32 color)
    {
        color = default(Color32);
        var screen = GUIUtility.GUIToScreenPoint(guiPosition) * EditorGUIUtility.pixelsPerPoint;
        var pixels = InternalEditorUtility.ReadScreenPixel(screen, 1, 1);
        if(pixels == null || pixels.Length == 0) return false;
        var c = (Color32)pixels[0];
        color = new Color32(c.r, c.g, c.b, 255);
        return true;
    }

    static bool SampleLayers(PixelLayer layer, Vector2 guiPosition, out Color32 color)
    {
        color = default(Color32);
        var layers = layer.Root.GetComponentsInChildren<PixelLayer>(false);
        System.Array.Sort(layers, (a, b) => a.sortIndex.CompareTo(b.sortIndex));
        var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

        float outA = 0, outR = 0, outG = 0, outB = 0;
        for(var i = 0; i < layers.Length; i++)
        {
            var other = layers[i];
            if(!other.visible) continue;
            var p = ToPixel(other, guiPosition);
            Color32 c;
            if(!PixelCanvasEditing.TryGetPixel(other, p.x, p.y, out c)) continue;
            var a = c.a / 255f * other.opacity;
            if(a <= 0) continue;
            float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
            if(linear)
            {
                r = Mathf.GammaToLinearSpace(r);
                g = Mathf.GammaToLinearSpace(g);
                b = Mathf.GammaToLinearSpace(b);
            }
            outR = r * a + outR * (1 - a);
            outG = g * a + outG * (1 - a);
            outB = b * a + outB * (1 - a);
            outA = a + outA * (1 - a);
        }
        if(outA <= 0) return false;

        float fr = outR / outA, fg = outG / outA, fb = outB / outA;
        if(linear)
        {
            fr = Mathf.LinearToGammaSpace(fr);
            fg = Mathf.LinearToGammaSpace(fg);
            fb = Mathf.LinearToGammaSpace(fb);
        }
        color = new Color32(ToByte(fr), ToByte(fg), ToByte(fb), ToByte(outA));
        return true;
    }

    static byte ToByte(float v)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    }

    public static void DrawBrushSettings(PixelBrush brush)
    {
        if(brushObject == null || brushObject.targetObject != brush) brushObject = new SerializedObject(brush);
        brushObject.Update();
        var pattern = brush as PixelPatternBrush;
        EditorGUI.BeginChangeCheck();
        var it = brushObject.GetIterator();
        var enter = true;
        while(it.NextVisible(enter))
        {
            enter = false;
            if(it.propertyPath == "m_Script") continue;
            if(pattern != null && !pattern.IsFieldVisible(it.propertyPath)) continue;
            EditorGUILayout.PropertyField(it, true);
        }
        if(EditorGUI.EndChangeCheck())
        {
            brushObject.ApplyModifiedProperties();
            PixelPatternBrush.ClearCache();
            PixelPaintState.Size = PixelPaintState.Size;
        }
    }

    public static Vector2Int ToPixel(PixelLayer layer, Vector2 guiPosition)
    {
        var f = ToPixelF(layer, guiPosition);
        return new Vector2Int(Mathf.FloorToInt(f.x), Mathf.FloorToInt(f.y));
    }

    public static Vector2 ToPixelF(PixelLayer layer, Vector2 guiPosition)
    {
        var ray = HandleUtility.GUIPointToWorldRay(guiPosition);
        var local = layer.transform.InverseTransformPoint(ray.origin);
        var ppu = layer.Root.pixelsPerUnit;
        return new Vector2(local.x * ppu, local.y * ppu);
    }

    static void DrawBounds(PixelLayer layer, Vector2Int size)
    {
        if(size.x == 0 || size.y == 0) return;
        var b = PixelCanvasEditing.Bounds(layer);
        var verts = Quad(layer.transform, layer.Root.pixelsPerUnit, b.xMin, b.yMin, b.xMax, b.yMax);
        var limit = PixelCanvasEditing.LimitHit;
        Handles.DrawSolidRectangleWithOutline(verts, Color.clear, limit ? Color.red : new Color(0.3f, 0.8f, 1f, 0.6f));
    }

    static void DrawTileBounds(PixelLayer layer)
    {
        var canvas = layer.Root;
        if(canvas.tileMode == PixelCanvas.TileMode.Off) return;
        var w = canvas.tileSize.x;
        var h = canvas.tileSize.y;
        var verts = Quad(layer.transform, canvas.pixelsPerUnit, 0, 0, w, h);
        Handles.DrawSolidRectangleWithOutline(verts, Color.clear, new Color(1f, 0.4f, 1f, 0.9f));
    }

    static void DrawAxes(PixelLayer layer)
    {
        if(!PixelPaintState.SymmetryX && !PixelPaintState.SymmetryY) return;
        var ppu = layer.Root.pixelsPerUnit;
        var b = PixelCanvasEditing.CanvasBounds(layer.Root);
        var pad = 24;
        var x0 = b.width > 0 ? b.xMin - pad : -pad;
        var x1 = b.width > 0 ? b.xMax + pad : pad;
        var y0 = b.height > 0 ? b.yMin - pad : -pad;
        var y1 = b.height > 0 ? b.yMax + pad : pad;
        var t = layer.transform;
        Handles.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        if(PixelPaintState.SymmetryX)
        {
            var ax = PixelPaintState.AxisX2 / 2f;
            Handles.DrawDottedLine(t.TransformPoint(new Vector3(ax / ppu, y0 / ppu)), t.TransformPoint(new Vector3(ax / ppu, y1 / ppu)), 4f);
        }
        if(PixelPaintState.SymmetryY)
        {
            var ay = PixelPaintState.AxisY2 / 2f;
            Handles.DrawDottedLine(t.TransformPoint(new Vector3(x0 / ppu, ay / ppu)), t.TransformPoint(new Vector3(x1 / ppu, ay / ppu)), 4f);
        }
        Handles.color = Color.white;
    }

    static void DrawCursor(PixelLayer layer, Vector2Int pixel, PixelBrush brush)
    {
        var ppu = layer.Root.pixelsPerUnit;
        var size = PixelPaintState.Size;
        var t = layer.transform;
        var eraser = PixelPaintState.Mode == PixelPaintMode.Eraser;
        var outline = eraser ? new Color(1, 0.4f, 0.4f) : Color.white;
        var faint = new Color(outline.r, outline.g, outline.b, 0.35f);

        var rect = brush.CursorRect(pixel.x, pixel.y, size);
        var ox = pixel.x - size / 2;
        var oy = pixel.y - size / 2;

        if(size <= 32 && !(brush is PixelPatternBrush && ((PixelPatternBrush)brush).kind == PixelPatternBrush.PatternKind.Stamp))
        {
            if(cursorBrush != brush || cursorSize != size || cursorVersion != PixelPaintState.Version) BuildCursorShape(brush, size);
            if(!cursorFull && cursorEdges.Count > 0)
            {
                if(cursorPoints.Length != cursorEdges.Count) cursorPoints = new Vector3[cursorEdges.Count];
                for(var i = 0; i < cursorEdges.Count; i++)
                    cursorPoints[i] = t.TransformPoint(new Vector3((ox + cursorEdges[i].x) / ppu, (oy + cursorEdges[i].y) / ppu));
                var oldColor = Handles.color;
                Handles.color = faint;
                Handles.DrawLines(cursorPoints);
                Handles.color = oldColor;
            }
        }
        Handles.DrawSolidRectangleWithOutline(Quad(t, ppu, rect.xMin, rect.yMin, rect.xMax, rect.yMax), Color.clear, outline);
    }

    static void BuildCursorShape(PixelBrush brush, int size)
    {
        cursorBrush = brush;
        cursorSize = size;
        cursorVersion = PixelPaintState.Version;
        cursorEdges.Clear();
        cursorFull = true;
        var covered = new bool[size * size];
        for(var j = 0; j < size; j++)
            for(var i = 0; i < size; i++)
            {
                covered[j * size + i] = brush.Covers(i, j, size);
                if(!covered[j * size + i]) cursorFull = false;
            }
        if(cursorFull) return;
        System.Func<int, int, bool> at = (i, j) => i >= 0 && j >= 0 && i < size && j < size && covered[j * size + i];
        for(var j = 0; j < size; j++)
        {
            for(var i = 0; i < size; i++)
            {
                if(!at(i, j)) continue;
                if(!at(i - 1, j)) AddEdge(i, j, i, j + 1);
                if(!at(i + 1, j)) AddEdge(i + 1, j, i + 1, j + 1);
                if(!at(i, j - 1)) AddEdge(i, j, i + 1, j);
                if(!at(i, j + 1)) AddEdge(i, j + 1, i + 1, j + 1);
            }
        }
    }

    static void AddEdge(int x0, int y0, int x1, int y1)
    {
        cursorEdges.Add(new Vector2(x0, y0));
        cursorEdges.Add(new Vector2(x1, y1));
    }

    static void DrawPickerCursor(PixelLayer layer, Vector2Int pixel)
    {
        var ppu = layer.Root.pixelsPerUnit;
        Handles.DrawSolidRectangleWithOutline(Quad(layer.transform, ppu, pixel.x, pixel.y, pixel.x + 1, pixel.y + 1), Color.clear, Color.white);
    }

    static void DrawSwatch(Vector2 mouse)
    {
        var c = previewColor;
        var rect = new Rect(mouse.x + 16, mouse.y + 16, 20, 20);
        Handles.BeginGUI();
        EditorGUI.DrawRect(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), Color.black);
        EditorGUI.DrawRect(rect, previewFound ? (Color)new Color32(c.r, c.g, c.b, 255) : new Color(0.35f, 0.35f, 0.35f));
        EditorGUI.DropShadowLabel(new Rect(rect.xMax + 4, rect.y, 70, rect.height), previewSource == PixelPickSource.Screen ? "screen" : "layers");
        Handles.EndGUI();
    }

    static Vector3[] Quad(Transform t, float ppu, float x0, float y0, float x1, float y1)
    {
        return new[]
        {
            t.TransformPoint(new Vector3(x0 / ppu, y0 / ppu)),
            t.TransformPoint(new Vector3(x1 / ppu, y0 / ppu)),
            t.TransformPoint(new Vector3(x1 / ppu, y1 / ppu)),
            t.TransformPoint(new Vector3(x0 / ppu, y1 / ppu))
        };
    }
}
#endif
