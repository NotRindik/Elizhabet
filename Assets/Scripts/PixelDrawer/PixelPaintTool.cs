#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.InputSystem;

[EditorTool("Pixel Paint", typeof(PixelLayer))]
public class PixelPaintTool : EditorTool
{
    const float PanelWidth = 260;

    static float panelHeight = 200;
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
    bool picking;
    PixelPickSource pickSource;

    static bool altHeld;
    static Color32 previewColor;
    static bool previewFound;
    static PixelPickSource previewSource;
    static bool unityEyeDropper;

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
    }

    public override void OnWillBeDeactivated()
    {
        if(active != null)
        {
            active.Cancel();
            active = null;
        }
        PixelFloating.CommitIfActive();
        PixelTempView.ClearTiles();
        picking = false;
        unityEyeDropper = false;
        if(PixelDrawerAssets.Settings.saveOnToolExit) PixelCanvasEditing.SaveAll();
    }

    public override void OnToolGUI(EditorWindow window)
    {
        var layer = target as PixelLayer;
        if(layer == null || layer.Root == null) return;
        var brush = PixelPaintState.Brush;
        if(brush == null) return;
        var e = Event.current;

        if(PixelFloating.Active && PixelFloating.Layer != layer) PixelFloating.Commit();

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

        Handles.BeginGUI();
        DrawPanel(window, layer, brush, canvasSize);
        Handles.EndGUI();

        window.wantsMouseMove = true;
        if(e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

        var panelRect = new Rect(PixelPaintState.PanelPosition, new Vector2(PanelWidth, panelHeight));
        var inPanel = panelRect.Contains(e.mousePosition);
        var pixel = ToPixel(layer, e.mousePosition);
        var pixelF = ToPixelF(layer, e.mousePosition);
        var type = e.type;

        var consumed = PixelFloating.HandleEvent(layer, e, id, inPanel);
        if(consumed)
        {
            if(type == EventType.MouseUp) active = null;
        }
        else if(type == EventType.MouseDown && e.button == 0 && !inPanel && HandleUtility.nearestControl == id)
        {
            unityEyeDropper = false;
            GUIUtility.hotControl = id;
            if(!e.alt) PixelPaintHotkeys.LayerPickHeld = false;
            if(e.alt)
            {
                picking = true;
                pickSource = PixelPaintHotkeys.LayerPickHeld ? PixelPickSource.Layers : PixelPickSource.Screen;
                PickColorAt(layer, e.mousePosition, pickSource);
            }
            else if(CtrlSelectsLayer(e))
            {
                SelectLayerAt(layer, e.mousePosition);
            }
            else
            {
                var sub = CurrentSubTool;
                if(layer.locked && sub.ModifiesPixels)
                {
                    window.ShowNotification(new GUIContent("Layer is locked"));
                }
                else
                {
                    active = sub;
                    active.MouseDown(MakeContext(layer, e, pixel, pixelF));
                }
            }
            e.Use();
        }
        else if(type == EventType.MouseDrag && GUIUtility.hotControl == id && (active != null || picking))
        {
            if(picking) PickColorAt(layer, e.mousePosition, pickSource);
            else active.MouseDrag(MakeContext(layer, e, pixel, pixelF));
            e.Use();
        }
        else if(type == EventType.MouseUp && GUIUtility.hotControl == id)
        {
            GUIUtility.hotControl = 0;
            if(active != null)
            {
                active.MouseUp(MakeContext(layer, e, pixel, pixelF));
                active = null;
            }
            picking = false;
            e.Use();
        }
        else if(type == EventType.MouseMove) window.Repaint();

        if(e.type == EventType.Repaint && !unityEyeDropper && !inPanel)
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
        float pressure = pen != null
            ? pen.pressure.ReadValue()
            : 0f;
        
        if (Mouse.current != null &&
            Mouse.current.leftButton.isPressed)
        {
            pressure = 1f;
        }

        return new PixelToolContext
        {
            layer = layer,
            pixel = pixel,
            pixelF = pixelF,
            shift = e.shift,
            ctrl = e.control || e.command,
            pressure = pressure,
        };
    }
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

    static bool CtrlSelectsLayer(Event e)
    {
        if(!(e.control || e.command) || !PixelDrawerAssets.Settings.ctrlClickSelectsLayer) return false;
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
            if(other != layer) Selection.activeGameObject = other.gameObject;
            return;
        }
    }

    public static void PickColorAt(PixelLayer layer, Vector2 guiPosition, PixelPickSource source)
    {
        if(layer == null || layer.Root == null) return;
        UpdatePreview(layer, guiPosition, source);
        if(!previewFound) return;
        PixelPaintState.Color = previewColor;
        PixelPaintState.PushRecent(previewColor);
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

    void DrawPanel(EditorWindow window, PixelLayer layer, PixelBrush brush, Vector2Int canvasSize)
    {
        var e = Event.current;
        var settings = PixelDrawerAssets.Settings;

        ClampPanel(window);
        var oldLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 70;

        var pos = PixelPaintState.PanelPosition;
        GUILayout.BeginArea(new Rect(pos.x, pos.y, PanelWidth, 1400));
        var box = EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        var header = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
        var dragRect = new Rect(header.x, header.y, header.width - 78, header.height);
        HandleDrag(dragRect, e);
        EditorGUIUtility.AddCursorRect(dragRect, MouseCursor.Pan);
        GUI.Label(dragRect, "::  Pixel Paint" + FrameSuffix(layer.Root), EditorStyles.boldLabel);
        if(GUI.Button(new Rect(header.xMax - 76, header.y, 36, header.height), new GUIContent("Win", "Open the Pixel Canvas window: animation, canvas settings, palette, brush settings, selection actions"), EditorStyles.miniButton))
            PixelCanvasWindow.Open(layer.Root);
        if(GUI.Button(new Rect(header.xMax - 38, header.y, 38, header.height), new GUIContent("Cfg", "Open Pixel Drawer settings (hotkeys)"), EditorStyles.miniButton))
            PixelCanvasMenu.OpenSettings();

        if(PixelFloating.Active)
        {
            DrawFloatingPanel();
        }
        else
        {
            DrawToolRows(settings);
            DrawColorSection(settings);
            DrawBrushMini(brush, settings);
        }

        var oldColor = GUI.contentColor;
        if(PixelCanvasEditing.LimitHit) GUI.contentColor = new Color(1, 0.4f, 0.4f);
        GUILayout.Label("Canvas " + canvasSize.x + "x" + canvasSize.y + " / limit " + layer.Root.EffectiveMaxSize + (layer.locked ? "   [LOCKED]" : ""), EditorStyles.miniLabel);
        GUI.contentColor = oldColor;

        EditorGUILayout.EndVertical();
        if(e.type == EventType.Repaint) panelHeight = box.height;
        GUILayout.EndArea();

        EditorGUIUtility.labelWidth = oldLabelWidth;
    }

    static string FrameSuffix(PixelCanvas canvas)
    {
        return canvas.FrameCount > 1 ? "   f " + (canvas.frame + 1) + "/" + canvas.FrameCount : "";
    }

    static void DrawToolRows(PixelDrawerSettings s)
    {
        var row1 = new[]
        {
            new GUIContent("Brush", "Brush (" + s.brushTool + ")"),
            new GUIContent("Erase", "Eraser (" + s.eraserTool + ")"),
            new GUIContent("Fill", "Bucket fill, Shift = erase (" + s.fillTool + ")"),
            new GUIContent("Line", "Line, Shift = 45 degrees (" + s.lineTool + ")"),
            new GUIContent("Rect", "Rectangle, Shift = square (" + s.rectTool + ")"),
            new GUIContent("Oval", "Ellipse, Shift = circle (" + s.ellipseTool + ")")
        };
        var row2 = new[]
        {
            new GUIContent("Select", "Rectangle selection (" + s.selectRectTool + ")\nShift = add, Ctrl = subtract, Shift+Ctrl = intersect"),
            new GUIContent("Lasso", "Lasso selection (" + s.lassoTool + ")"),
            new GUIContent("Wand", "Magic wand (" + s.wandTool + ")"),
            new GUIContent("Move", "Move layer / selection (" + s.moveTool + ")")
        };
        var m = (int)PixelPaintState.Mode;
        var s1 = GUILayout.Toolbar(m < 6 ? m : -1, row1);
        if(s1 >= 0 && s1 != m) PixelPaintState.Mode = (PixelPaintMode)s1;
        m = (int)PixelPaintState.Mode;
        var s2 = GUILayout.Toolbar(m >= 6 ? m - 6 : -1, row2);
        if(s2 >= 0 && s2 + 6 != m) PixelPaintState.Mode = (PixelPaintMode)(s2 + 6);
    }

    const string GlowTip = "Off: normal pixels (removes glow under them).\nColor And Glow: new pixels glow with the amount.\nGlow Only: changes only the glow of existing pixels. Needs a Pixel Art asset layer.";

    static void DrawColorSection(PixelDrawerSettings settings)
    {
        using(new EditorGUI.DisabledScope(PixelPaintState.Mode == PixelPaintMode.Eraser))
        {
            EditorGUI.BeginChangeCheck();
            var color = EditorGUILayout.ColorField(new GUIContent("Color", "Alt + click: pick color from screen. Alt + " + settings.pickLayers.key + " + click: pick from all layers. Keys: " + settings.pickScreen + " / " + settings.pickLayers), PixelPaintState.Color);
            if(EditorGUI.EndChangeCheck()) PixelPaintState.Color = color;
        }

        DrawGlowCompact();

        var palette = PixelPaintState.Palette;
        if(palette != null && palette.colors.Count > 0)
        {
            int ignore;
            var clicked = DrawTiles(palette.colors, false, out ignore, PanelWidth);
            if(clicked.a != 0) PixelPaintState.Color = clicked;
        }
    }

    static void DrawGlowCompact()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        var mode = (PixelEmissionMode)EditorGUILayout.EnumPopup(new GUIContent("Glow", GlowTip), PixelPaintState.EmissionMode);
        if(EditorGUI.EndChangeCheck()) PixelPaintState.EmissionMode = mode;
        if(PixelPaintState.EmissionMode != PixelEmissionMode.Off)
        {
            EditorGUI.BeginChangeCheck();
            var amount = EditorGUILayout.IntField(PixelPaintState.EmissionValue, GUILayout.Width(40));
            if(EditorGUI.EndChangeCheck()) PixelPaintState.EmissionValue = amount;
        }
        EditorGUILayout.EndHorizontal();
    }

    public static void DrawGlowFull()
    {
        EditorGUI.BeginChangeCheck();
        var mode = (PixelEmissionMode)EditorGUILayout.EnumPopup(new GUIContent("Glow", GlowTip), PixelPaintState.EmissionMode);
        if(EditorGUI.EndChangeCheck()) PixelPaintState.EmissionMode = mode;
        if(PixelPaintState.EmissionMode == PixelEmissionMode.Off) return;
        EditorGUI.BeginChangeCheck();
        var amount = EditorGUILayout.IntSlider(new GUIContent("Amount", "Glow strength 0-255. The layer's Emission Intensity scales it."), PixelPaintState.EmissionValue, 0, 255);
        if(EditorGUI.EndChangeCheck()) PixelPaintState.EmissionValue = amount;
    }

    public static void DrawPaletteFull(float width)
    {
        var palettes = PixelDrawerAssets.Palettes;
        var palette = PixelPaintState.Palette;
        EditorGUILayout.BeginHorizontal();
        var names = new string[palettes.Count];
        var index = -1;
        for(var i = 0; i < palettes.Count; i++)
        {
            names[i] = palettes[i].name;
            if(palettes[i] == palette) index = i;
        }
        var next = EditorGUILayout.Popup("Palette", index, names);
        if(next != index && next >= 0) PixelPaintState.Palette = palettes[next];
        var limit = GUILayout.Toggle(PixelPaintState.LimitToPalette, new GUIContent("Lock", "Snap every color to the nearest palette color"), EditorStyles.miniButton, GUILayout.Width(36));
        if(limit != PixelPaintState.LimitToPalette) PixelPaintState.LimitToPalette = limit;
        if(GUILayout.Button(new GUIContent("+", "Add the current color to the palette"), EditorStyles.miniButton, GUILayout.Width(22)) && palette != null)
        {
            Undo.RecordObject(palette, "Add Palette Color");
            palette.colors.Add((Color32)PixelPaintState.Color);
            EditorUtility.SetDirty(palette);
        }
        EditorGUILayout.EndHorizontal();

        if(palette != null && palette.colors.Count > 0)
        {
            var removeAt = -1;
            var clicked = DrawTiles(palette.colors, true, out removeAt, width);
            if(removeAt >= 0)
            {
                Undo.RecordObject(palette, "Remove Palette Color");
                palette.colors.RemoveAt(removeAt);
                EditorUtility.SetDirty(palette);
            }
            if(clicked.a != 0) PixelPaintState.Color = clicked;
        }
        var recent = PixelPaintState.Recent;
        if(recent.Count > 0)
        {
            GUILayout.Label("Recent", EditorStyles.miniLabel);
            int ignore;
            var clicked = DrawTiles(new System.Collections.Generic.List<Color32>(recent), false, out ignore, width);
            if(clicked.a != 0) PixelPaintState.Color = clicked;
        }
    }

    static Color32 DrawTiles(System.Collections.Generic.List<Color32> colors, bool allowRemove, out int removeAt, float width)
    {
        removeAt = -1;
        var result = new Color32(0, 0, 0, 0);
        const float size = 16;
        var perRow = Mathf.Max(1, Mathf.FloorToInt((width - 24) / (size + 2)));
        var rows = Mathf.CeilToInt(colors.Count / (float)perRow);
        var area = GUILayoutUtility.GetRect(width - 20, rows * (size + 2));
        var e = Event.current;
        var current = (Color32)PixelPaintState.Color;
        for(var i = 0; i < colors.Count; i++)
        {
            var r = new Rect(area.x + (i % perRow) * (size + 2), area.y + (i / perRow) * (size + 2), size, size);
            var c = colors[i];
            EditorGUI.DrawRect(r, new Color32(c.r, c.g, c.b, 255));
            if(c.r == current.r && c.g == current.g && c.b == current.b)
            {
                EditorGUI.DrawRect(new Rect(r.x - 1, r.y - 1, r.width + 2, 1), Color.white);
                EditorGUI.DrawRect(new Rect(r.x - 1, r.yMax, r.width + 2, 1), Color.white);
                EditorGUI.DrawRect(new Rect(r.x - 1, r.y, 1, r.height), Color.white);
                EditorGUI.DrawRect(new Rect(r.xMax, r.y, 1, r.height), Color.white);
            }
            if(e.type == EventType.MouseDown && r.Contains(e.mousePosition))
            {
                if(e.button == 0) result = new Color32(c.r, c.g, c.b, 255);
                else if(e.button == 1 && allowRemove) removeAt = i;
                e.Use();
            }
        }
        return result;
    }

    static void DrawBrushMini(PixelBrush brush, PixelDrawerSettings settings)
    {
        var mode = PixelPaintState.Mode;
        if(mode == PixelPaintMode.SelectRect || mode == PixelPaintMode.SelectLasso || mode == PixelPaintMode.SelectWand || mode == PixelPaintMode.Move) return;

        var brushes = PixelDrawerAssets.Brushes;
        var names = new string[brushes.Count];
        var index = -1;
        for(var i = 0; i < brushes.Count; i++)
        {
            names[i] = brushes[i].name;
            if(brushes[i] == brush) index = i;
        }
        var next = EditorGUILayout.Popup("Brush", index, names);
        if(next != index && next >= 0) PixelPaintState.Brush = brushes[next];

        if(mode == PixelPaintMode.Fill) return;

        EditorGUI.BeginChangeCheck();
        var size = EditorGUILayout.IntSlider(new GUIContent("Size", "Hotkeys: " + settings.sizeDown + " / " + settings.sizeUp), PixelPaintState.Size, brush.MinSize, brush.MaxSize);
        if(EditorGUI.EndChangeCheck()) PixelPaintState.Size = size;
    }

    public static void DrawBrushSection(PixelLayer layer, PixelBrush brush, PixelDrawerSettings settings)
    {
        var mode = PixelPaintState.Mode;
        if(mode == PixelPaintMode.SelectRect || mode == PixelPaintMode.SelectLasso || mode == PixelPaintMode.SelectWand || mode == PixelPaintMode.Move) return;

        var brushes = PixelDrawerAssets.Brushes;
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

        if(mode == PixelPaintMode.Fill) return;

        EditorGUI.BeginChangeCheck();
        var size = EditorGUILayout.IntSlider(new GUIContent("Size", "Hotkeys: " + settings.sizeDown + " / " + settings.sizeUp), PixelPaintState.Size, brush.MinSize, brush.MaxSize);
        if(EditorGUI.EndChangeCheck()) PixelPaintState.Size = size;

        EditorGUI.BeginChangeCheck();
        var open = EditorGUILayout.Foldout(PixelPaintState.BrushSettingsOpen, "Brush settings", true);
        if(EditorGUI.EndChangeCheck())
        {
            PixelPaintState.BrushSettingsOpen = open;
            PixelPaintState.Save();
        }
        if(open) DrawBrushSettings(brush);
    }

    static void DrawBrushSettings(PixelBrush brush)
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

    public static void DrawToolOptions(PixelLayer layer)
    {
        var mode = PixelPaintState.Mode;
        if(mode == PixelPaintMode.Fill || mode == PixelPaintMode.SelectWand)
        {
            var t = EditorGUILayout.IntSlider(new GUIContent("Tolerance", "Max color difference (0-255) that still counts as the same color"), PixelPaintState.Tolerance, 0, 255);
            var c = EditorGUILayout.Toggle(new GUIContent("Contiguous", "Off: affects every matching pixel of the area, not only connected ones"), PixelPaintState.Contiguous);
            if(t != PixelPaintState.Tolerance || c != PixelPaintState.Contiguous)
            {
                PixelPaintState.Tolerance = t;
                PixelPaintState.Contiguous = c;
                PixelPaintState.Save();
            }
        }
        if(mode == PixelPaintMode.Rectangle || mode == PixelPaintMode.Ellipse)
        {
            var f = EditorGUILayout.Toggle("Filled", PixelPaintState.ShapeFilled);
            if(f != PixelPaintState.ShapeFilled)
            {
                PixelPaintState.ShapeFilled = f;
                PixelPaintState.Save();
            }
        }
        if(mode == PixelPaintMode.Brush)
        {
            var p = EditorGUILayout.Toggle(new GUIContent("Pixel perfect", "Removes corner pixels of 1px lines (needs size 1, no symmetry, no tiling)"), PixelPaintState.PixelPerfect);
            if(p != PixelPaintState.PixelPerfect)
            {
                PixelPaintState.PixelPerfect = p;
                PixelPaintState.Save();
            }
        }
        if(mode == PixelPaintMode.SelectRect || mode == PixelPaintMode.SelectLasso || mode == PixelPaintMode.SelectWand)
        {
            var labels = new[] { new GUIContent("New"), new GUIContent("Add"), new GUIContent("Sub"), new GUIContent("Inter") };
            var s = GUILayout.Toolbar((int)PixelPaintState.SelectMode, labels);
            if(s != (int)PixelPaintState.SelectMode)
            {
                PixelPaintState.SelectMode = (PixelSelectMode)s;
                PixelPaintState.Save();
            }
        }
    }

    public static void DrawOptionsFoldout(PixelLayer layer)
    {
        EditorGUI.BeginChangeCheck();
        var open = EditorGUILayout.Foldout(PixelPaintState.OptionsOpen, "Symmetry", true);
        if(EditorGUI.EndChangeCheck())
        {
            PixelPaintState.OptionsOpen = open;
            PixelPaintState.Save();
        }
        if(!open) return;

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        PixelPaintState.SymmetryX = EditorGUILayout.ToggleLeft("Mirror X", PixelPaintState.SymmetryX, GUILayout.Width(80));
        var ax = EditorGUILayout.FloatField(PixelPaintState.AxisX2 / 2f);
        PixelPaintState.AxisX2 = Mathf.RoundToInt(ax * 2f);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        PixelPaintState.SymmetryY = EditorGUILayout.ToggleLeft("Mirror Y", PixelPaintState.SymmetryY, GUILayout.Width(80));
        var ay = EditorGUILayout.FloatField(PixelPaintState.AxisY2 / 2f);
        PixelPaintState.AxisY2 = Mathf.RoundToInt(ay * 2f);
        EditorGUILayout.EndHorizontal();
        if(EditorGUI.EndChangeCheck()) PixelPaintState.Save();
        if(GUILayout.Button("Axes to layer center", EditorStyles.miniButton))
        {
            var b = PixelCanvasEditing.Bounds(layer);
            if(b.width > 0)
            {
                PixelPaintState.AxisX2 = b.xMin * 2 + b.width;
                PixelPaintState.AxisY2 = b.yMin * 2 + b.height;
                PixelPaintState.Save();
            }
        }

    }

    static void Run(bool defer, System.Action action)
    {
        if(defer) EditorApplication.delayCall += () => action();
        else action();
    }

    public static void DrawSelectionActions(PixelLayer layer, bool defer)
    {
        var canvas = layer.Root;
        var has = PixelSelection.Has(canvas);
        var s = PixelDrawerAssets.Settings;
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button(new GUIContent("All", "Select all (" + s.selectAll + ")"), EditorStyles.miniButton)) Run(defer, () => PixelSelection.SelectAll(canvas));
        using(new EditorGUI.DisabledScope(!has))
        {
            if(GUILayout.Button(new GUIContent("None", "Deselect (" + s.deselect + ")"), EditorStyles.miniButton)) Run(defer, () => PixelSelection.Clear(canvas));
            if(GUILayout.Button(new GUIContent("Invert", "Invert selection (" + s.invertSelection + ")"), EditorStyles.miniButton)) Run(defer, () => PixelSelection.Invert(canvas));
            if(GUILayout.Button(new GUIContent("Clear px", "Erase selected pixels (" + s.clearSelectionPixels + ")"), EditorStyles.miniButton)) Run(defer, () => PixelPaintHotkeys.ClearSelectionPixels(layer));
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button(new GUIContent("Copy", "(" + s.copy + ")  Copy selection, or the whole layer"), EditorStyles.miniButton)) Run(defer, () => PixelClipboard.Copy(layer, false, false));
        if(GUILayout.Button(new GUIContent("Cut", "(" + s.cut + ")"), EditorStyles.miniButton)) Run(defer, () => PixelClipboard.Copy(layer, true, false));
        if(GUILayout.Button(new GUIContent("Copy all", "(" + s.copyMerged + ")  Copy the visible result of all layers"), EditorStyles.miniButton)) Run(defer, () => PixelClipboard.Copy(layer, false, true));
        if(GUILayout.Button(new GUIContent("Paste", "(" + s.paste + ")"), EditorStyles.miniButton)) Run(defer, () => PixelClipboard.Paste(layer, false));
        if(GUILayout.Button(new GUIContent("New L.", "(" + s.pasteAsNewLayer + ")  Paste as a new layer"), EditorStyles.miniButton)) Run(defer, () => PixelClipboard.Paste(layer, true));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button(new GUIContent("Transform", "(" + s.transform + ")  Free transform of the selection or the whole layer"), EditorStyles.miniButton) && !layer.locked) Run(defer, () => PixelFloating.BeginFromLayer(layer));
        if(GUILayout.Button(new GUIContent("Stamp brush", "Create a stamp brush from the selection or layer"), EditorStyles.miniButton)) Run(defer, () => PixelPaintHotkeys.CreatePatternBrush(layer, PixelPatternBrush.PatternKind.Stamp));
        if(GUILayout.Button(new GUIContent("Tile brush", "Create a repeating tile brush from the selection or layer"), EditorStyles.miniButton)) Run(defer, () => PixelPaintHotkeys.CreatePatternBrush(layer, PixelPatternBrush.PatternKind.Tile));
        EditorGUILayout.EndHorizontal();
    }

    static void DrawFloatingPanel()
    {
        GUILayout.Label("Transform (Enter = apply, Esc = cancel)", EditorStyles.miniLabel);
        GUILayout.Label("Drag: move   corners: scale (Shift = snap, Ctrl = free)   blue dot: rotate", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button("Apply", EditorStyles.miniButton)) PixelFloating.Commit();
        if(GUILayout.Button("Cancel", EditorStyles.miniButton)) PixelFloating.Cancel();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button("Flip H", EditorStyles.miniButton)) PixelFloating.FlipHorizontal();
        if(GUILayout.Button("Flip V", EditorStyles.miniButton)) PixelFloating.FlipVertical();
        if(GUILayout.Button("Rot -90", EditorStyles.miniButton)) PixelFloating.Rotate90(false);
        if(GUILayout.Button("Rot +90", EditorStyles.miniButton)) PixelFloating.Rotate90(true);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button("x2", EditorStyles.miniButton)) PixelFloating.MultiplyScale(2f);
        if(GUILayout.Button("x3", EditorStyles.miniButton)) PixelFloating.MultiplyScale(3f);
        if(GUILayout.Button("/2", EditorStyles.miniButton)) PixelFloating.MultiplyScale(0.5f);
        if(GUILayout.Button("Reset", EditorStyles.miniButton))
        {
            PixelFloating.SetAngle(0);
            PixelFloating.SetScale(Vector2.one);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUI.BeginChangeCheck();
        var angle = EditorGUILayout.Slider("Angle", PixelFloating.Angle, -180f, 180f);
        if(EditorGUI.EndChangeCheck()) PixelFloating.SetAngle(angle);
        EditorGUI.BeginChangeCheck();
        var scale = EditorGUILayout.Vector2Field("Scale", PixelFloating.Scale);
        if(EditorGUI.EndChangeCheck()) PixelFloating.SetScale(scale);
        GUILayout.Label("Resampling: nearest neighbour (exact for x2/x3, 90 degree turns, flips)", EditorStyles.wordWrappedMiniLabel);
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

        if(size <= 16 && !(brush is PixelPatternBrush && ((PixelPatternBrush)brush).kind == PixelPatternBrush.PatternKind.Stamp))
        {
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
                        Handles.DrawSolidRectangleWithOutline(Quad(t, 1f, x0, y0, x0 + 1 / ppu, y0 + 1 / ppu), Color.clear, faint);
                    }
                }
            }
        }
        Handles.DrawSolidRectangleWithOutline(Quad(t, ppu, rect.xMin, rect.yMin, rect.xMax, rect.yMax), Color.clear, outline);
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
