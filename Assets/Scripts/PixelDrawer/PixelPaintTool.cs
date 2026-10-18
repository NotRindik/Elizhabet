using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditorInternal;
using UnityEngine;

[EditorTool("Pixel Paint", typeof(PixelLayer))]
public class PixelPaintTool : EditorTool
{
    const float PanelWidth = 230;

    static float panelHeight = 160;
    static SerializedObject brushObject;

    Vector2Int last;
    bool painting;
    bool picking;
    PixelPickSource pickSource;

    static bool altHeld;
    // Preview color is sampled on mouse/key events (same coordinate context as an actual pick), never in Repaint.
    static Color32 previewColor;
    static bool previewFound;
    static PixelPickSource previewSource;
    static bool unityEyeDropper; // Unity's own screen eyedropper (color field) is running

    public override GUIContent toolbarIcon
    {
        get { return EditorGUIUtility.IconContent("Grid.PaintTool"); }
    }

    public override void OnActivated()
    {
        PixelDrawerAssets.Ensure();
    }

    public override void OnWillBeDeactivated()
    {
        if(painting)
        {
            painting = false;
            PixelHistory.EndStroke();
        }
        picking = false;
        unityEyeDropper = false;
    }

    public override void OnToolGUI(EditorWindow window)
    {
        var layer = target as PixelLayer;
        if(layer == null || layer.Root == null) return;
        var brush = PixelPaintState.Brush;
        if(brush == null) return;
        var e = Event.current;

        TrackModifiers(window, e);

        if((altHeld || picking) && !unityEyeDropper && (e.isMouse || e.isKey))
        {
            var src = picking ? pickSource : (PixelPaintHotkeys.LayerPickHeld ? PixelPickSource.Layers : PixelPickSource.Screen);
            UpdatePreview(layer, e.mousePosition, src);
            if(e.type == EventType.MouseMove || e.isKey) window.Repaint();
        }

        var id = GUIUtility.GetControlID(FocusType.Passive);
        var canvasSize = PixelCanvasEditing.Size(layer);

        Handles.BeginGUI();
        DrawPanel(window, layer, brush, canvasSize);
        Handles.EndGUI();

        window.wantsMouseMove = true;
        if(e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

        var panelRect = new Rect(PixelPaintState.PanelPosition, new Vector2(PanelWidth, panelHeight));
        var inPanel = panelRect.Contains(e.mousePosition);
        var pixel = ToPixel(layer, e.mousePosition);

        if(e.type == EventType.MouseDown && e.button == 0 && !inPanel && HandleUtility.nearestControl == id)
        {
            unityEyeDropper = false;
            GUIUtility.hotControl = id;
            last = pixel;
            if(!e.alt) PixelPaintHotkeys.LayerPickHeld = false;
            if(e.alt)
            {
                // Alt = eyedropper from screen. Alt + X (held) = eyedropper from layers.
                picking = true;
                pickSource = PixelPaintHotkeys.LayerPickHeld ? PixelPickSource.Layers : PixelPickSource.Screen;
                PickColorAt(layer, e.mousePosition, pickSource);
            }
            else
            {
                painting = true;
                PixelCanvasEditing.LimitHit = false;
                PixelHistory.BeginStroke(layer);
                Paint(layer, pixel, pixel, e.shift, true);
            }
            e.Use();
        }
        else if(e.type == EventType.MouseDrag && GUIUtility.hotControl == id && (painting || picking))
        {
            if(picking)
            {
                PickColorAt(layer, e.mousePosition, pickSource);
            }
            else
            {
                Paint(layer, last, pixel, e.shift, false);
                last = pixel;
            }
            e.Use();
        }
        else if(e.type == EventType.MouseUp && GUIUtility.hotControl == id)
        {
            GUIUtility.hotControl = 0;
            if(painting) PixelHistory.EndStroke();
            painting = false;
            picking = false;
            e.Use();
        }
        else if(e.type == EventType.MouseMove) window.Repaint();

        if(e.type == EventType.Repaint && !unityEyeDropper && !inPanel)
        {
            // Overlays are outline-only and are skipped while an eyedropper is running,
            // so they never tint the pixels you are sampling.
            var pickerActive = altHeld || picking;
            var source = picking ? pickSource : (PixelPaintHotkeys.LayerPickHeld ? PixelPickSource.Layers : PixelPickSource.Screen);
            if(!pickerActive)
            {
                DrawBounds(layer, canvasSize);
                DrawCursor(layer, pixel, brush);
            }
            else
            {
                // screen picking: draw nothing near the cursor so the sampled pixel stays clean
                if(source == PixelPickSource.Layers) DrawPickerCursor(layer, pixel);
                DrawSwatch(e.mousePosition);
            }
        }
    }

    // Alt state + Unity's screen eyedropper (color field) state.
    static void TrackModifiers(EditorWindow window, Event e)
    {
        if(e.isMouse || e.isKey) altHeld = e.alt;
        if(e.isKey)
        {
            if(e.keyCode == KeyCode.LeftAlt || e.keyCode == KeyCode.RightAlt) altHeld = e.type == EventType.KeyDown;
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

    // ---------- Eyedropper ----------

    // Screen: the color exactly as it is on screen right now (includes scene effects, lighting, post-processing).
    // Layers: the exact color from all visible layers of the canvas, composited bottom to top; scene effects don't affect it.
    public static void PickColorAt(PixelLayer layer, Vector2 guiPosition, PixelPickSource source)
    {
        if(layer == null || layer.Root == null) return;
        UpdatePreview(layer, guiPosition, source);
        if(!previewFound) return;
        var c = previewColor;
        PixelPaintState.Color = c;
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
        return source == PixelPickSource.Screen
            ? SampleScreen(guiPosition, out color)
            : SampleLayers(layer, guiPosition, out color);
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

    // Composite of every visible layer of the canvas at this point (sorting layer/order, opacity, alpha-over),
    // blended in the project's color space so it matches what the renderer produces.
    static bool SampleLayers(PixelLayer layer, Vector2 guiPosition, out Color32 color)
    {
        color = default(Color32);
        var layers = layer.Root.GetComponentsInChildren<PixelLayer>(false);
        System.Array.Sort(layers, CompareLayers);
        var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

        float outA = 0, outR = 0, outG = 0, outB = 0; // premultiplied
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

    static int CompareLayers(PixelLayer a, PixelLayer b)
    {
        var la = SortingLayer.GetLayerValueFromName(a.sortingLayerName);
        var lb = SortingLayer.GetLayerValueFromName(b.sortingLayerName);
        if(la != lb) return la.CompareTo(lb);
        if(a.sortingOrder != b.sortingOrder) return a.sortingOrder.CompareTo(b.sortingOrder);
        return a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex());
    }

    static byte ToByte(float v)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    }

    // ---------- Panel ----------

    void DrawPanel(EditorWindow window, PixelLayer layer, PixelBrush brush, Vector2Int canvasSize)
    {
        var e = Event.current;
        var settings = PixelDrawerAssets.Settings;
        var brushes = PixelDrawerAssets.Brushes;

        ClampPanel(window);
        var oldLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 70;

        var pos = PixelPaintState.PanelPosition;
        GUILayout.BeginArea(new Rect(pos.x, pos.y, PanelWidth, 900));
        var box = EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // header: drag handle + settings button
        var header = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
        var dragRect = new Rect(header.x, header.y, header.width - 40, header.height);
        HandleDrag(dragRect, e);
        EditorGUIUtility.AddCursorRect(dragRect, MouseCursor.Pan);
        GUI.Label(dragRect, "::  Pixel Paint", EditorStyles.boldLabel);
        if(GUI.Button(new Rect(header.xMax - 38, header.y, 38, header.height), new GUIContent("Cfg", "Open Pixel Drawer settings (hotkeys)"), EditorStyles.miniButton))
            PixelCanvasMenu.OpenSettings();

        // mode
        var labels = new[]
        {
            new GUIContent("Brush (" + settings.brushTool + ")"),
            new GUIContent("Eraser (" + settings.eraserTool + ")")
        };
        var mode = GUILayout.Toolbar((int)PixelPaintState.Mode, labels);
        if(mode != (int)PixelPaintState.Mode) PixelPaintState.Mode = (PixelPaintMode)mode;

        // brush selector
        EditorGUILayout.BeginHorizontal();
        var names = new string[brushes.Count];
        var index = -1;
        for(var i = 0; i < brushes.Count; i++)
        {
            names[i] = brushes[i].name;
            if(brushes[i] == brush) index = i;
        }
        var next = EditorGUILayout.Popup(index, names);
        if(next != index && next >= 0) PixelPaintState.Brush = brushes[next];
        if(GUILayout.Button(new GUIContent("+", "Create a new brush (copy of the current one)"), EditorStyles.miniButton, GUILayout.Width(22)))
        {
            var created = PixelDrawerAssets.CreateBrush("New Brush", brush);
            PixelPaintState.Brush = created;
            EditorGUIUtility.PingObject(created);
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();

        // color + size
        using(new EditorGUI.DisabledScope(PixelPaintState.Mode == PixelPaintMode.Eraser))
        {
            EditorGUI.BeginChangeCheck();
            var color = EditorGUILayout.ColorField(new GUIContent("Color", "Alt + click: pick color from screen. Alt + " + settings.pickLayers.key + " + click: pick from all layers. Keys: " + settings.pickScreen + " / " + settings.pickLayers), PixelPaintState.Color);
            if(EditorGUI.EndChangeCheck()) PixelPaintState.Color = color;
        }
        EditorGUI.BeginChangeCheck();
        var size = EditorGUILayout.IntSlider(new GUIContent("Size", "Hotkeys: " + settings.sizeDown + " / " + settings.sizeUp), PixelPaintState.Size, brush.MinSize, brush.MaxSize);
        if(EditorGUI.EndChangeCheck()) PixelPaintState.Size = size;

        // brush settings
        EditorGUI.BeginChangeCheck();
        var open = EditorGUILayout.Foldout(PixelPaintState.BrushSettingsOpen, "Brush settings", true);
        if(EditorGUI.EndChangeCheck())
        {
            PixelPaintState.BrushSettingsOpen = open;
            PixelPaintState.Save();
        }
        if(open) DrawBrushSettings(brush);

        // canvas info
        var oldColor = GUI.contentColor;
        if(PixelCanvasEditing.LimitHit) GUI.contentColor = new Color(1, 0.4f, 0.4f);
        GUILayout.Label("Canvas " + canvasSize.x + "x" + canvasSize.y + " / limit " + layer.Root.EffectiveMaxSize, EditorStyles.miniLabel);
        GUI.contentColor = oldColor;

        EditorGUILayout.EndVertical();
        if(e.type == EventType.Repaint) panelHeight = box.height;
        GUILayout.EndArea();

        EditorGUIUtility.labelWidth = oldLabelWidth;
    }

    static void DrawBrushSettings(PixelBrush brush)
    {
        if(brushObject == null || brushObject.targetObject != brush) brushObject = new SerializedObject(brush);
        brushObject.Update();
        EditorGUI.BeginChangeCheck();
        var it = brushObject.GetIterator();
        var enter = true;
        while(it.NextVisible(enter))
        {
            enter = false;
            if(it.propertyPath == "m_Script") continue;
            EditorGUILayout.PropertyField(it, true);
        }
        if(EditorGUI.EndChangeCheck())
        {
            brushObject.ApplyModifiedProperties();
            PixelPaintState.Size = PixelPaintState.Size; // re-clamp to the edited min/max
        }
    }

    void HandleDrag(Rect handle, Event e)
    {
        var dragId = GUIUtility.GetControlID(FocusType.Passive);
        switch(e.GetTypeForControl(dragId))
        {
            case EventType.MouseDown:
                if(e.button == 0 && handle.Contains(e.mousePosition))
                {
                    GUIUtility.hotControl = dragId;
                    e.Use();
                }
                break;
            case EventType.MouseDrag:
                if(GUIUtility.hotControl == dragId)
                {
                    PixelPaintState.PanelPosition += e.delta;
                    e.Use();
                }
                break;
            case EventType.MouseUp:
                if(GUIUtility.hotControl == dragId)
                {
                    GUIUtility.hotControl = 0;
                    PixelPaintState.Save();
                    e.Use();
                }
                break;
        }
    }

    static void ClampPanel(EditorWindow window)
    {
        var view = window as SceneView;
        var area = view != null && view.camera != null
            ? new Vector2(view.camera.pixelWidth, view.camera.pixelHeight) / EditorGUIUtility.pixelsPerPoint
            : window.position.size;
        var p = PixelPaintState.PanelPosition;
        p.x = Mathf.Clamp(p.x, 0, Mathf.Max(0, area.x - PanelWidth));
        p.y = Mathf.Clamp(p.y, 0, Mathf.Max(0, area.y - panelHeight));
        PixelPaintState.PanelPosition = p;
    }

    // ---------- Painting ----------

    static void Paint(PixelLayer layer, Vector2Int from, Vector2Int to, bool shiftErase, bool includeStart)
    {
        var erase = shiftErase || PixelPaintState.Mode == PixelPaintMode.Eraser;
        var c = erase ? new Color32(0, 0, 0, 0) : (Color32)PixelPaintState.Color;
        PixelCanvasEditing.Line(layer, from, to, PixelPaintState.Brush, PixelPaintState.Size, c, includeStart);
        PixelCanvasEditing.Apply();
        SceneView.RepaintAll();
    }

    static Vector2Int ToPixel(PixelLayer layer, Vector2 guiPosition)
    {
        var ray = HandleUtility.GUIPointToWorldRay(guiPosition);
        var local = layer.transform.InverseTransformPoint(ray.origin);
        var ppu = layer.Root.pixelsPerUnit;
        return new Vector2Int(Mathf.FloorToInt(local.x * ppu), Mathf.FloorToInt(local.y * ppu));
    }

    // ---------- Overlays ----------

    static void DrawBounds(PixelLayer layer, Vector2Int size)
    {
        if(size.x == 0 || size.y == 0) return;
        var ppu = layer.Root.pixelsPerUnit;
        var x0 = layer.origin.x / ppu;
        var y0 = layer.origin.y / ppu;
        var x1 = x0 + size.x / ppu;
        var y1 = y0 + size.y / ppu;
        var verts = Quad(layer.transform, x0, y0, x1, y1);
        var limit = PixelCanvasEditing.LimitHit;
        Handles.DrawSolidRectangleWithOutline(verts, Color.clear, limit ? Color.red : new Color(0.3f, 0.8f, 1f, 0.6f));
    }

    // Brush cursor: outlines only, no fill.
    static void DrawCursor(PixelLayer layer, Vector2Int pixel, PixelBrush brush)
    {
        var ppu = layer.Root.pixelsPerUnit;
        var size = PixelPaintState.Size;
        var t = layer.transform;
        var eraser = PixelPaintState.Mode == PixelPaintMode.Eraser;
        var outline = eraser ? new Color(1, 0.4f, 0.4f) : Color.white;
        var faint = new Color(outline.r, outline.g, outline.b, 0.35f);

        var ox = pixel.x - size / 2;
        var oy = pixel.y - size / 2;

        if(size <= 16)
        {
            // for non-square footprints (circle, custom) show the covered pixels as thin outlines
            var full = true;
            for(var j = 0; j < size && full; j++)
                for(var i = 0; i < size; i++)
                    if(!brush.Covers(i, j, size))
                    {
                        full = false;
                        break;
                    }
            if(!full)
            {
                for(var j = 0; j < size; j++)
                {
                    for(var i = 0; i < size; i++)
                    {
                        if(!brush.Covers(i, j, size)) continue;
                        var x0 = (ox + i) / ppu;
                        var y0 = (oy + j) / ppu;
                        Handles.DrawSolidRectangleWithOutline(Quad(t, x0, y0, x0 + 1 / ppu, y0 + 1 / ppu), Color.clear, faint);
                    }
                }
            }
        }
        var bx = ox / ppu;
        var by = oy / ppu;
        Handles.DrawSolidRectangleWithOutline(Quad(t, bx, by, bx + size / ppu, by + size / ppu), Color.clear, outline);
    }

    // Eyedropper cursor: a single pixel outline.
    static void DrawPickerCursor(PixelLayer layer, Vector2Int pixel)
    {
        var ppu = layer.Root.pixelsPerUnit;
        var x0 = pixel.x / ppu;
        var y0 = pixel.y / ppu;
        Handles.DrawSolidRectangleWithOutline(Quad(layer.transform, x0, y0, x0 + 1 / ppu, y0 + 1 / ppu), Color.clear, Color.white);
    }

    // Small preview of the last sampled color, next to the mouse. Draws the cache only; sampling happens on events.
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

    static Vector3[] Quad(Transform t, float x0, float y0, float x1, float y1)
    {
        return new[]
        {
            t.TransformPoint(new Vector3(x0, y0)),
            t.TransformPoint(new Vector3(x1, y0)),
            t.TransformPoint(new Vector3(x1, y1)),
            t.TransformPoint(new Vector3(x0, y1))
        };
    }
}
