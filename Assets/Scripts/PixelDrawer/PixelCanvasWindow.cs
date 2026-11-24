#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UIButton = UnityEngine.UIElements.Button;

public class PixelCanvasWindow : EditorWindow
{
    const string ResampleModePref = "PixelDrawer.ResampleMode";

    [SerializeField] PixelCanvas canvas;
    [SerializeField] bool locked;

    PixelColorPickerGUI picker;
    PixelCanvas boundCanvas;
    PixelCanvas resampleCanvas;
    float resampleFrom;
    float resampleTo;

    VisualElement body;
    Label empty;
    Label title;
    VisualElement canvasProps;
    readonly Dictionary<PixelPaintMode, UIButton> toolButtons = new Dictionary<PixelPaintMode, UIButton>();
    VisualElement toolOptions;
    PixelPaintMode optionsMode = (PixelPaintMode)(-1);
    ColorField colorField;
    IMGUIContainer pickerView;
    UIButton glowButton;
    EnumField glowMode;
    SliderInt glowAmount;
    PopupField<PixelPalette> paletteField;
    UIButton paletteLock;
    VisualElement swatches;
    VisualElement recentSwatches;
    Label recentLabel;
    VisualElement brushSection;
    PopupField<PixelBrush> brushField;
    SliderInt sizeSlider;
    IMGUIContainer brushSettingsView;
    UIButton brushSettingsButton;
    UIButton mirrorX;
    UIButton mirrorY;
    FloatField axisX;
    FloatField axisY;
    UIButton centerAxes;
    VisualElement selectionActions;
    SliderInt toleranceSlider;

    PixelPalette shownPalette;
    int shownPaletteCount = -1;
    Color32 shownColor;
    int shownRecentCount = -1;
    readonly List<VisualElement> swatchItems = new List<VisualElement>();
    readonly List<Color32> swatchColors = new List<Color32>();
    readonly List<VisualElement> recentItems = new List<VisualElement>();
    readonly List<Color32> recentColors = new List<Color32>();

    [MenuItem("Window/Pixel Canvas/Canvas")]
    public static void Open()
    {
        GetWindow<PixelCanvasWindow>("Pixel Canvas");
    }

    public static void Open(PixelCanvas target)
    {
        var window = GetWindow<PixelCanvasWindow>("Pixel Canvas");
        if(target != null) window.canvas = target;
        window.Refresh();
    }

    void OnEnable()
    {
        minSize = new Vector2(260, 240);
        Selection.selectionChanged += OnSelectionChanged;
        PixelPaintState.Changed += OnPaintChanged;
        Undo.undoRedoPerformed += OnPaintChanged;
    }

    void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
        PixelPaintState.Changed -= OnPaintChanged;
        Undo.undoRedoPerformed -= OnPaintChanged;
        if(picker != null)
        {
            picker.Dispose();
            picker = null;
        }
    }

    void ShowButton(Rect rect)
    {
        var next = GUI.Toggle(rect, locked, GUIContent.none, "IN LockButton");
        if(next == locked) return;
        locked = next;
        Refresh();
    }

    void OnSelectionChanged()
    {
        Refresh();
    }

    void OnPaintChanged()
    {
        if(body != null) UpdatePaint();
    }

    void CreateGUI()
    {
        var root = rootVisualElement;
        PixelUI.Setup(root);

        body = new VisualElement();
        body.style.flexGrow = 1;
        root.Add(body);

        BuildHeader();
        var scroll = new PixelScrollArea(true, false, true);
        body.Add(scroll);

        BuildTools(scroll);
        BuildColor(scroll);
        BuildPalette(scroll);
        BuildBrush(scroll);
        BuildSymmetry(scroll);
        BuildSelection(scroll);
        canvasProps = new VisualElement();
        scroll.Add(canvasProps);
        BuildCanvas(canvasProps);
        BuildReference(canvasProps);
        BuildBake(canvasProps);

        empty = PixelUI.Label("Select a Pixel Canvas or one of its layers", "pd-empty");
        root.Add(empty);

        root.schedule.Execute(Poll).Every(300);
        Refresh();
    }

    void BuildHeader()
    {
        var bar = new VisualElement();
        bar.AddToClassList("pd-bar");
        bar.Add(PixelUI.Icon("canvas"));
        title = PixelUI.Label("", "pd-title");
        bar.Add(title);
        bar.Add(PixelUI.Spacer());
        bar.Add(PixelUI.IconButton("target", "Highlight the canvas in the Hierarchy", () =>
        {
            if(canvas != null) EditorGUIUtility.PingObject(canvas);
        }));
        bar.Add(PixelUI.IconButton("onion", "Open the Layers and timeline window", PixelLayersWindow.Open));
        bar.Add(PixelUI.IconButton("gear", "Pixel Drawer settings and hotkeys", PixelCanvasMenu.OpenSettings));
        body.Add(bar);
    }

    void BuildTools(VisualElement parent)
    {
        VisualElement content;
        PixelUI.Section(parent, "tools", "Tools", true, out content);
        var s = PixelDrawerAssets.Settings;
        var line = s.brushLineModifier == PixelLineModifier.Off ? "" : "\n" + s.BrushLineKeyName + " + click: line from the end of the last stroke";
        var row = PixelUI.Row(
            ToolButton(PixelPaintMode.Brush, "brush", "Brush (" + s.brushTool + ")" + line + "\nCtrl + click: select the layer under the cursor"),
            ToolButton(PixelPaintMode.Eraser, "eraser", "Eraser (" + s.eraserTool + ")" + line),
            ToolButton(PixelPaintMode.Fill, "fill", "Bucket fill (" + s.fillTool + ")\nShift: erase the area"),
            ToolButton(PixelPaintMode.Line, "line", "Line (" + s.lineTool + ")\nShift: 45 degree steps"),
            ToolButton(PixelPaintMode.Rectangle, "rect", "Rectangle (" + s.rectTool + ")\nShift: square"),
            ToolButton(PixelPaintMode.Ellipse, "ellipse", "Ellipse (" + s.ellipseTool + ")\nShift: circle"),
            PixelUI.Separator(),
            ToolButton(PixelPaintMode.SelectRect, "select", "Rectangle selection (" + s.selectRectTool + ")\nShift: add, Ctrl: subtract, Shift+Ctrl: intersect"),
            ToolButton(PixelPaintMode.SelectLasso, "lasso", "Lasso selection (" + s.lassoTool + ")"),
            ToolButton(PixelPaintMode.SelectWand, "wand", "Magic wand (" + s.wandTool + ")"),
            ToolButton(PixelPaintMode.Move, "move", "Move layer or selection (" + s.moveTool + ")\nArrows: move by one pixel"));
        content.Add(row);
        toolOptions = new VisualElement();
        toolOptions.AddToClassList("pd-row");
        toolOptions.style.marginTop = 4;
        content.Add(toolOptions);
    }

    UIButton ToolButton(PixelPaintMode mode, string icon, string tip)
    {
        var button = PixelUI.IconButton(icon, tip, () => PixelPaintState.Mode = mode);
        toolButtons[mode] = button;
        return button;
    }

    void BuildToolOptions()
    {
        var mode = PixelPaintState.Mode;
        optionsMode = mode;
        toolOptions.Clear();
        toleranceSlider = null;
        switch(mode)
        {
            case PixelPaintMode.Brush:
                toolOptions.Add(PixelUI.Group(Toggle("pixel_perfect", "Pixel perfect: removes corner pixels of 1 px lines (size 1, no symmetry, no tiling)",
                    () => PixelPaintState.PixelPerfect, v => PixelPaintState.PixelPerfect = v)));
                break;
            case PixelPaintMode.Rectangle:
            case PixelPaintMode.Ellipse:
                toolOptions.Add(PixelUI.Group(Toggle("filled", "Filled shape", () => PixelPaintState.ShapeFilled, v => PixelPaintState.ShapeFilled = v)));
                break;
            case PixelPaintMode.Fill:
            case PixelPaintMode.SelectWand:
                toolOptions.Add(PixelUI.Group(
                    Choice("sample_layer", "Sample the active layer", () => PixelPaintState.FillSample == PixelFillSample.Layer, () => PixelPaintState.FillSample = PixelFillSample.Layer),
                    Choice("sample_all", "Sample all visible layers merged", () => PixelPaintState.FillSample == PixelFillSample.AllLayers, () => PixelPaintState.FillSample = PixelFillSample.AllLayers),
                    Choice("sample_below", "Sample the layer below", () => PixelPaintState.FillSample == PixelFillSample.LayerBelow, () => PixelPaintState.FillSample = PixelFillSample.LayerBelow)));
                toolOptions.Add(PixelUI.Group(
                    Choice("area_connected", "Only pixels connected to the clicked one", () => PixelPaintState.Contiguous, () => PixelPaintState.Contiguous = true),
                    Choice("area_global", "Every pixel of the same color", () => !PixelPaintState.Contiguous, () => PixelPaintState.Contiguous = false)));
                toleranceSlider = new SliderInt("Tolerance", 0, 255) { showInputField = true, tooltip = "Max color difference that still counts as the same color" };
                toleranceSlider.AddToClassList("pd-slider");
                toleranceSlider.style.minWidth = 180;
                toleranceSlider.SetValueWithoutNotify(PixelPaintState.Tolerance);
                toleranceSlider.RegisterValueChangedCallback(e =>
                {
                    PixelPaintState.Tolerance = e.newValue;
                    PixelPaintState.Save();
                });
                toolOptions.Add(toleranceSlider);
                break;
        }
        if(mode == PixelPaintMode.SelectRect || mode == PixelPaintMode.SelectLasso || mode == PixelPaintMode.SelectWand)
        {
            toolOptions.Add(PixelUI.Group(
                Choice("sel_new", "New selection", () => PixelPaintState.SelectMode == PixelSelectMode.Replace, () => PixelPaintState.SelectMode = PixelSelectMode.Replace),
                Choice("sel_add", "Add to selection (Shift)", () => PixelPaintState.SelectMode == PixelSelectMode.Add, () => PixelPaintState.SelectMode = PixelSelectMode.Add),
                Choice("sel_sub", "Subtract from selection (Ctrl)", () => PixelPaintState.SelectMode == PixelSelectMode.Subtract, () => PixelPaintState.SelectMode = PixelSelectMode.Subtract),
                Choice("sel_inter", "Intersect with selection (Shift+Ctrl)", () => PixelPaintState.SelectMode == PixelSelectMode.Intersect, () => PixelPaintState.SelectMode = PixelSelectMode.Intersect)));
        }
        if(toolOptions.childCount == 0) toolOptions.Add(PixelUI.Label(mode == PixelPaintMode.Move ? "Drag to move, arrows nudge by one pixel" : "No options", "pd-dim"));
    }

    readonly List<System.Action> optionRefresh = new List<System.Action>();

    UIButton Toggle(string icon, string tip, System.Func<bool> get, System.Action<bool> set)
    {
        UIButton button = null;
        button = PixelUI.IconButton(icon, tip, () =>
        {
            set(!get());
            PixelPaintState.Save();
        });
        PixelUI.SetOn(button, get());
        optionRefresh.Add(() => PixelUI.SetOn(button, get()));
        return button;
    }

    UIButton Choice(string icon, string tip, System.Func<bool> isOn, System.Action pick)
    {
        UIButton button = null;
        button = PixelUI.IconButton(icon, tip, () =>
        {
            pick();
            PixelPaintState.Save();
        });
        PixelUI.SetOn(button, isOn());
        optionRefresh.Add(() => PixelUI.SetOn(button, isOn()));
        return button;
    }

    void BuildColor(VisualElement parent)
    {
        VisualElement content;
        var s = PixelDrawerAssets.Settings;
        PixelUI.Section(parent, "color", "Color", true, out content);
        colorField = new ColorField { showAlpha = true, tooltip = "Brush color\nAlt + click in the scene: pick from the screen (also over other programs)\nAlt + " + s.pickLayers.key + " + click: pick from all layers" };
        colorField.style.flexGrow = 1;
        colorField.RegisterValueChangedCallback(e => PixelPaintState.Color = e.newValue);
        content.Add(PixelUI.Row(colorField));

        pickerView = new IMGUIContainer(() =>
        {
            if(picker == null) picker = new PixelColorPickerGUI();
            var c = PixelPaintState.Color;
            var width = pickerView.contentRect.width;
            if(width <= 1) width = position.width - 40;
            if(picker.Draw(ref c, width + 20f)) PixelPaintState.Color = c;
        });
        pickerView.AddToClassList("pd-imgui");
        content.Add(pickerView);

        glowButton = PixelUI.IconButton("glow", "Glow: painted pixels glow (needs a Pixel Art asset layer)", () =>
        {
            PixelPaintState.EmissionMode = PixelPaintState.EmissionMode == PixelEmissionMode.Off ? PixelEmissionMode.ColorAndGlow : PixelEmissionMode.Off;
        });
        glowMode = new EnumField(PixelEmissionMode.ColorAndGlow) { tooltip = "Color And Glow: new pixels glow.\nGlow Only: changes only the glow of existing pixels." };
        glowMode.style.width = 110;
        glowMode.RegisterValueChangedCallback(e =>
        {
            if(e.newValue != null) PixelPaintState.EmissionMode = (PixelEmissionMode)e.newValue;
        });
        glowAmount = new SliderInt(0, 255) { showInputField = true, tooltip = "Glow strength. The layer's Emission Intensity scales it." };
        glowAmount.AddToClassList("pd-slider");
        glowAmount.RegisterValueChangedCallback(e => PixelPaintState.EmissionValue = e.newValue);
        var glowRow = PixelUI.Row(glowButton, glowMode, glowAmount);
        glowRow.AddToClassList("pd-row--nowrap");
        content.Add(glowRow);
    }

    void BuildPalette(VisualElement parent)
    {
        VisualElement content;
        PixelUI.Section(parent, "palette", "Palette", true, out content);
        var palettes = PixelDrawerAssets.Palettes;
        paletteField = new PopupField<PixelPalette>(new List<PixelPalette>(palettes), PixelPaintState.Palette, p => p != null ? p.name : "None", p => p != null ? p.name : "None");
        paletteField.style.flexGrow = 1;
        paletteField.RegisterValueChangedCallback(e =>
        {
            if(e.newValue != null) PixelPaintState.Palette = e.newValue;
        });
        paletteLock = PixelUI.IconButton("lock", "Lock to palette: every color snaps to the nearest palette color", () => PixelPaintState.LimitToPalette = !PixelPaintState.LimitToPalette);
        var add = PixelUI.IconButton("plus", "Add the current color to the palette", () =>
        {
            var palette = PixelPaintState.Palette;
            if(palette == null) return;
            Undo.RecordObject(palette, "Add Palette Color");
            palette.colors.Add((Color32)PixelPaintState.Color);
            EditorUtility.SetDirty(palette);
            UpdateSwatches(true);
        });
        var row = PixelUI.Row(paletteField, paletteLock, add);
        row.AddToClassList("pd-row--nowrap");
        content.Add(row);
        swatches = new VisualElement();
        swatches.AddToClassList("pd-swatches");
        swatches.tooltip = "Click: use the color. Right click: remove it from the palette";
        content.Add(swatches);
        recentLabel = PixelUI.Label("RECENT", "pd-sub");
        content.Add(recentLabel);
        recentSwatches = new VisualElement();
        recentSwatches.AddToClassList("pd-swatches");
        content.Add(recentSwatches);
    }

    void BuildBrush(VisualElement parent)
    {
        VisualElement content;
        var s = PixelDrawerAssets.Settings;
        brushSection = PixelUI.Section(parent, "brush", "Brush", true, out content).parent;
        var brushes = PixelDrawerAssets.Brushes;
        brushField = new PopupField<PixelBrush>(new List<PixelBrush>(brushes), PixelPaintState.Brush, b => b != null ? b.name : "None", b => b != null ? b.name : "None");
        brushField.style.flexGrow = 1;
        brushField.RegisterValueChangedCallback(e =>
        {
            if(e.newValue != null) PixelPaintState.Brush = e.newValue;
        });
        var add = PixelUI.IconButton("plus", "New brush: copy of the current one", () =>
        {
            var created = PixelDrawerAssets.CreateBrush("New Brush", PixelPaintState.Brush);
            PixelPaintState.Brush = created;
            EditorGUIUtility.PingObject(created);
            brushField.choices = new List<PixelBrush>(PixelDrawerAssets.Brushes);
            UpdatePaint();
        });
        brushSettingsButton = PixelUI.IconButton("gear", "Brush settings", () =>
        {
            PixelPaintState.BrushSettingsOpen = !PixelPaintState.BrushSettingsOpen;
            PixelPaintState.Save();
        });
        var row = PixelUI.Row(brushField, add, brushSettingsButton);
        row.AddToClassList("pd-row--nowrap");
        content.Add(row);
        sizeSlider = new SliderInt("Size", 1, 64) { showInputField = true, tooltip = "Brush size. Keys: " + s.sizeDown + " / " + s.sizeUp };
        sizeSlider.AddToClassList("pd-slider");
        sizeSlider.style.marginLeft = 0;
        sizeSlider.RegisterValueChangedCallback(e => PixelPaintState.Size = e.newValue);
        content.Add(sizeSlider);
        brushSettingsView = new IMGUIContainer(() =>
        {
            var brush = PixelPaintState.Brush;
            if(brush == null) return;
            var old = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(brushSettingsView.contentRect.width * 0.4f, 80f, 140f);
            PixelPaintTool.DrawBrushSettings(brush);
            EditorGUIUtility.labelWidth = old;
        });
        brushSettingsView.AddToClassList("pd-imgui");
        content.Add(brushSettingsView);
    }

    void BuildSymmetry(VisualElement parent)
    {
        VisualElement content;
        PixelUI.Section(parent, "symmetry", "Symmetry", false, out content);
        mirrorX = PixelUI.IconButton("mirror_x", "Mirror horizontally around the vertical axis", () =>
        {
            PixelPaintState.SymmetryX = !PixelPaintState.SymmetryX;
            PixelPaintState.Save();
            SceneView.RepaintAll();
        });
        axisX = new FloatField { tooltip = "X position of the vertical axis, in pixels" };
        axisX.AddToClassList("pd-field-small");
        axisX.RegisterValueChangedCallback(e =>
        {
            PixelPaintState.AxisX2 = Mathf.RoundToInt(e.newValue * 2f);
            PixelPaintState.Save();
            SceneView.RepaintAll();
        });
        mirrorY = PixelUI.IconButton("mirror_y", "Mirror vertically around the horizontal axis", () =>
        {
            PixelPaintState.SymmetryY = !PixelPaintState.SymmetryY;
            PixelPaintState.Save();
            SceneView.RepaintAll();
        });
        axisY = new FloatField { tooltip = "Y position of the horizontal axis, in pixels" };
        axisY.AddToClassList("pd-field-small");
        axisY.RegisterValueChangedCallback(e =>
        {
            PixelPaintState.AxisY2 = Mathf.RoundToInt(e.newValue * 2f);
            PixelPaintState.Save();
            SceneView.RepaintAll();
        });
        centerAxes = PixelUI.IconButton("center", "Put the axes in the center of the selected layer", () =>
        {
            var layer = ActiveLayer();
            if(layer == null) return;
            var b = PixelCanvasEditing.Bounds(layer);
            if(b.width <= 0) return;
            PixelPaintState.AxisX2 = b.xMin * 2 + b.width;
            PixelPaintState.AxisY2 = b.yMin * 2 + b.height;
            PixelPaintState.Save();
            SceneView.RepaintAll();
        });
        content.Add(PixelUI.Row(PixelUI.Group(mirrorX, axisX), PixelUI.Group(mirrorY, axisY), centerAxes));
    }

    void BuildSelection(VisualElement parent)
    {
        VisualElement content;
        var s = PixelDrawerAssets.Settings;
        PixelUI.Section(parent, "selection", "Selection and clipboard", true, out content);
        selectionActions = content;
        content.Add(PixelUI.Row(
            PixelUI.Group(
                LayerAction("select_all", "Select all (" + s.selectAll + ")", l => PixelSelection.SelectAll(l.Root)),
                LayerAction("deselect", "Deselect (" + s.deselect + ")", l => PixelSelection.Clear(l.Root)),
                LayerAction("invert", "Invert selection (" + s.invertSelection + ")", l => PixelSelection.Invert(l.Root)),
                LayerAction("clear_px", "Erase the selected pixels (" + s.clearSelectionPixels + ")", PixelPaintHotkeys.ClearSelectionPixels)),
            PixelUI.Group(
                LayerAction("copy", "Copy the selection or the whole layer (" + s.copy + ")", l => PixelClipboard.Copy(l, false, false)),
                LayerAction("cut", "Cut (" + s.cut + ")", l => PixelClipboard.Copy(l, true, false)),
                LayerAction("copy_merged", "Copy the visible result of all layers (" + s.copyMerged + ")", l => PixelClipboard.Copy(l, false, true)),
                LayerAction("paste", "Paste (" + s.paste + ")\nAn image copied in another program is pasted as a new layer", l => PixelClipboard.Paste(l, false)),
                LayerAction("paste_layer", "Paste as a new layer (" + s.pasteAsNewLayer + ")", l => PixelClipboard.Paste(l, true))),
            PixelUI.Group(
                LayerAction("transform", "Free transform of the selection or the whole layer (" + s.transform + ")", l =>
                {
                    if(!l.locked) PixelFloating.BeginFromLayer(l);
                }),
                LayerAction("stamp", "Create a stamp brush from the selection or layer", l => PixelPaintHotkeys.CreatePatternBrush(l, PixelPatternBrush.PatternKind.Stamp)),
                LayerAction("tile", "Create a repeating tile brush from the selection or layer", l => PixelPaintHotkeys.CreatePatternBrush(l, PixelPatternBrush.PatternKind.Tile))),
            PixelUI.Group(
                LayerAction("palette", "Hue / Saturation / Value of the layer or selection (" + s.hueSaturation + ")", PixelAdjustWindow.Open))));
    }

    UIButton LayerAction(string icon, string tip, System.Action<PixelLayer> action)
    {
        return PixelUI.IconButton(icon, tip, () =>
        {
            var layer = ActiveLayer();
            if(layer == null) return;
            EditorApplication.delayCall += () =>
            {
                if(layer != null) action(layer);
                SceneView.RepaintAll();
            };
        });
    }

    void BuildCanvas(VisualElement parent)
    {
        VisualElement content;
        PixelUI.Section(parent, "canvas", "Canvas", false, out content);
        content.Add(PixelUI.Label("SIZE", "pd-sub"));
        content.Add(new PropertyField { bindingPath = "pixelsPerUnit" });
        content.Add(new PropertyField { bindingPath = "maxSize" });
        content.Add(new PropertyField { bindingPath = "growStep" });
        content.Add(PixelUI.Label("RESAMPLE PPU", "pd-sub"));
        var resample = new IMGUIContainer(DrawResample);
        resample.AddToClassList("pd-imgui");
        content.Add(resample);
        content.Add(PixelUI.Label("STORAGE", "pd-sub"));
        content.Add(new PropertyField { bindingPath = "assetFolder" });
        content.Add(PixelUI.Label("TILING", "pd-sub"));
        content.Add(new PropertyField { bindingPath = "tileMode" });
        content.Add(new PropertyField { bindingPath = "tileSize" });
        content.Add(PixelUI.Label("COPIES AND ASSETS", "pd-sub"));
        content.Add(PixelUI.Row(
            PixelUI.TextButton("Duplicate linked", "Copy of the canvas that shares the pixels", () => CanvasOp(c => PixelLayerOps.DuplicateCanvas(c, false))),
            PixelUI.TextButton("Duplicate full", "Copy of the canvas with its own pixels", () => CanvasOp(c => PixelLayerOps.DuplicateCanvas(c, true))),
            PixelUI.TextButton("Make all unique", "Give every layer its own files in a new folder", () => CanvasOp(PixelLayerOps.MakeCanvasUnique)),
            PixelUI.TextButton("Convert all", "Convert every layer to a Pixel Art asset", () => CanvasOp(c => PixelArtOps.ConvertCanvas(c))),
            PixelUI.TextButton("Clean unused PNGs", "Delete PNG files no layer uses anymore", () => CanvasOp(PixelLayerOps.CleanUnusedPngs))));
    }

    void BuildReference(VisualElement parent)
    {
        VisualElement content;
        PixelUI.Section(parent, "reference", "Reference image", false, out content);
        content.Add(new PropertyField { bindingPath = "reference" });
        content.Add(new PropertyField { bindingPath = "referenceVisible", label = "Visible" });
        content.Add(new PropertyField { bindingPath = "referenceOpacity", label = "Opacity" });
        content.Add(new PropertyField { bindingPath = "referenceOffset", label = "Offset" });
        content.Add(new PropertyField { bindingPath = "referenceScale", label = "Scale" });
    }

    void BuildBake(VisualElement parent)
    {
        VisualElement content;
        PixelUI.Section(parent, "bake", "Bake", false, out content);
        content.Add(new PropertyField { bindingPath = "baked" });
        content.Add(new PropertyField { bindingPath = "useBakedInPlay" });
        var bake = PixelUI.TextButton("Bake to one sprite", "Merge all visible layers into one sprite used in play mode and builds", () => CanvasOp(c => PixelBaker.Bake(c)));
        bake.AddToClassList("pd-btn--accent");
        content.Add(PixelUI.Row(bake));
    }

    void CanvasOp(System.Action<PixelCanvas> action)
    {
        var c = canvas;
        if(c == null) return;
        EditorApplication.delayCall += () =>
        {
            if(c != null) action(c);
        };
    }

    void Refresh()
    {
        if(body == null) return;
        if(!locked)
        {
            var go = Selection.activeGameObject;
            if(go != null && go.scene.IsValid())
            {
                var found = go.GetComponentInParent<PixelCanvas>(true);
                if(found != null) canvas = found;
            }
        }
        var has = canvas != null;
        body.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
        empty.style.display = has ? DisplayStyle.None : DisplayStyle.Flex;
        if(!has) return;
        if(boundCanvas != canvas)
        {
            boundCanvas = canvas;
            canvasProps.Unbind();
            canvasProps.Bind(new SerializedObject(canvas));
            resampleCanvas = null;
        }
        title.text = canvas.name;
        UpdatePaint();
    }

    void Poll()
    {
        if(canvas == null)
        {
            Refresh();
            return;
        }
        if(title.text != canvas.name) title.text = canvas.name;
        var palette = PixelPaintState.Palette;
        if(palette != shownPalette || (palette != null && palette.colors.Count != shownPaletteCount)) UpdateSwatches(true);
        selectionActions.SetEnabled(ActiveLayer() != null);
        centerAxes.SetEnabled(ActiveLayer() != null);
    }

    PixelLayer ActiveLayer()
    {
        var go = Selection.activeGameObject;
        var layer = go != null ? go.GetComponentInParent<PixelLayer>(true) : null;
        return layer != null && layer.Root == canvas ? layer : null;
    }

    void UpdatePaint()
    {
        if(canvas == null || toolOptions == null) return;
        var mode = PixelPaintState.Mode;
        foreach(var pair in toolButtons) PixelUI.SetOn(pair.Value, pair.Key == mode);
        if(mode != optionsMode)
        {
            optionRefresh.Clear();
            BuildToolOptions();
        }
        for(var i = 0; i < optionRefresh.Count; i++) optionRefresh[i]();
        if(toleranceSlider != null) toleranceSlider.SetValueWithoutNotify(PixelPaintState.Tolerance);

        var color = PixelPaintState.Color;
        colorField.SetValueWithoutNotify(color);
        colorField.SetEnabled(mode != PixelPaintMode.Eraser);
        pickerView.MarkDirtyRepaint();

        var glow = PixelPaintState.EmissionMode;
        PixelUI.SetOn(glowButton, glow != PixelEmissionMode.Off);
        glowMode.style.display = glow != PixelEmissionMode.Off ? DisplayStyle.Flex : DisplayStyle.None;
        glowAmount.style.display = glow != PixelEmissionMode.Off ? DisplayStyle.Flex : DisplayStyle.None;
        if(glow != PixelEmissionMode.Off) glowMode.SetValueWithoutNotify(glow);
        glowAmount.SetValueWithoutNotify(PixelPaintState.EmissionValue);

        var palettes = PixelDrawerAssets.Palettes;
        if(paletteField.choices.Count != palettes.Count) paletteField.choices = new List<PixelPalette>(palettes);
        paletteField.SetValueWithoutNotify(PixelPaintState.Palette);
        PixelUI.SetOn(paletteLock, PixelPaintState.LimitToPalette);
        UpdateSwatches(false);

        var selectMode = mode == PixelPaintMode.SelectRect || mode == PixelPaintMode.SelectLasso || mode == PixelPaintMode.SelectWand || mode == PixelPaintMode.Move;
        brushSection.style.display = selectMode ? DisplayStyle.None : DisplayStyle.Flex;
        var brush = PixelPaintState.Brush;
        var brushes = PixelDrawerAssets.Brushes;
        if(brushField.choices.Count != brushes.Count) brushField.choices = new List<PixelBrush>(brushes);
        brushField.SetValueWithoutNotify(brush);
        if(brush != null)
        {
            sizeSlider.lowValue = brush.MinSize;
            sizeSlider.highValue = Mathf.Max(brush.MinSize + 1, brush.MaxSize);
        }
        sizeSlider.SetValueWithoutNotify(PixelPaintState.Size);
        sizeSlider.style.display = mode == PixelPaintMode.Fill ? DisplayStyle.None : DisplayStyle.Flex;
        var settingsOpen = PixelPaintState.BrushSettingsOpen && mode != PixelPaintMode.Fill;
        PixelUI.SetOn(brushSettingsButton, PixelPaintState.BrushSettingsOpen);
        brushSettingsView.style.display = settingsOpen ? DisplayStyle.Flex : DisplayStyle.None;
        if(settingsOpen) brushSettingsView.MarkDirtyRepaint();

        PixelUI.SetOn(mirrorX, PixelPaintState.SymmetryX);
        PixelUI.SetOn(mirrorY, PixelPaintState.SymmetryY);
        axisX.SetValueWithoutNotify(PixelPaintState.AxisX2 / 2f);
        axisY.SetValueWithoutNotify(PixelPaintState.AxisY2 / 2f);

        var layer = ActiveLayer();
        selectionActions.SetEnabled(layer != null);
        centerAxes.SetEnabled(layer != null);
    }

    void UpdateSwatches(bool force)
    {
        var palette = PixelPaintState.Palette;
        var current = (Color32)PixelPaintState.Color;
        var count = palette != null ? palette.colors.Count : 0;
        if(force || palette != shownPalette || count != shownPaletteCount)
        {
            shownPalette = palette;
            shownPaletteCount = count;
            BuildSwatches(swatches, swatchItems, swatchColors, palette != null ? palette.colors : null, true);
        }
        var recent = PixelPaintState.Recent;
        if(force || recent.Count != shownRecentCount || !SameList(recent, recentColors))
        {
            shownRecentCount = recent.Count;
            BuildSwatches(recentSwatches, recentItems, recentColors, new List<Color32>(recent), false);
        }
        recentLabel.style.display = recent.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        if(!force && current.Equals(shownColor)) return;
        shownColor = current;
        MarkCurrent(swatchItems, swatchColors, current);
        MarkCurrent(recentItems, recentColors, current);
    }

    static bool SameList(IList<Color32> a, List<Color32> b)
    {
        if(a.Count != b.Count) return false;
        for(var i = 0; i < a.Count; i++)
            if(!a[i].Equals(b[i])) return false;
        return true;
    }

    void BuildSwatches(VisualElement container, List<VisualElement> items, List<Color32> colors, List<Color32> source, bool removable)
    {
        container.Clear();
        items.Clear();
        colors.Clear();
        if(source == null) return;
        for(var i = 0; i < source.Count; i++)
        {
            var c = source[i];
            var index = i;
            var swatch = new VisualElement();
            swatch.AddToClassList("pd-swatch");
            swatch.style.backgroundColor = (Color)new Color32(c.r, c.g, c.b, 255);
            swatch.RegisterCallback<PointerDownEvent>(e =>
            {
                if(e.button == 0) PixelPaintState.Color = new Color32(c.r, c.g, c.b, 255);
                else if(e.button == 1 && removable)
                {
                    var palette = PixelPaintState.Palette;
                    if(palette == null || index >= palette.colors.Count) return;
                    Undo.RecordObject(palette, "Remove Palette Color");
                    palette.colors.RemoveAt(index);
                    EditorUtility.SetDirty(palette);
                    UpdateSwatches(true);
                }
                e.StopPropagation();
            });
            container.Add(swatch);
            items.Add(swatch);
            colors.Add(c);
        }
        shownColor = new Color32(0, 0, 0, 0);
    }

    static void MarkCurrent(List<VisualElement> items, List<Color32> colors, Color32 current)
    {
        for(var i = 0; i < items.Count; i++)
        {
            var c = colors[i];
            items[i].EnableInClassList("pd-swatch--current", c.r == current.r && c.g == current.g && c.b == current.b);
        }
    }

    void DrawResample()
    {
        if(canvas == null) return;
        var stored = PixelPpuResampler.DetectStoredPpu(canvas);
        if(resampleCanvas != canvas)
        {
            resampleCanvas = canvas;
            resampleFrom = stored > 0 ? stored : canvas.pixelsPerUnit;
            resampleTo = resampleFrom;
        }
        var old = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 80;
        if(stored > 0 && !Mathf.Approximately(stored, canvas.pixelsPerUnit))
            EditorGUILayout.HelpBox("Layers were saved at PPU " + stored + " but the canvas says " + canvas.pixelsPerUnit + ". Use From = " + stored + ".", MessageType.Warning);
        resampleFrom = Mathf.Max(1, EditorGUILayout.FloatField(new GUIContent("From", "PPU the art was drawn at"), resampleFrom));
        resampleTo = Mathf.Max(1, EditorGUILayout.FloatField(new GUIContent("To", "New PPU"), resampleTo));
        var mode = (PixelResampleMode)EditorPrefs.GetInt(ResampleModePref, (int)PixelResampleMode.PixelTrue);
        var newMode = (PixelResampleMode)EditorGUILayout.EnumPopup(new GUIContent("Algorithm",
            "Blocky: each old pixel becomes a block, the art looks just stretched.\n" +
            "Smooth: Scale2x/Scale3x on upscale (rounded diagonals and corners, no new colors), thin lines survive downscale.\n" +
            "Pixel True: Smooth + 1 px lines and outlines stay 1 px at the new PPU."), mode);
        if(newMode != mode) EditorPrefs.SetInt(ResampleModePref, (int)newMode);
        using(new EditorGUI.DisabledScope(Mathf.Approximately(resampleFrom, resampleTo)))
        {
            if(GUILayout.Button("Resample " + resampleFrom + " → " + resampleTo))
            {
                var c = canvas;
                var from = resampleFrom;
                var to = resampleTo;
                EditorApplication.delayCall += () =>
                {
                    if(PixelPpuResampler.Resample(c, from, to, newMode)) resampleCanvas = null;
                };
            }
        }
        EditorGUIUtility.labelWidth = old;
    }
}
#endif
