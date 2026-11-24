#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[Overlay(typeof(SceneView), "pixel-drawer-paint", "Pixel Paint", true)]
public class PixelPaintOverlay : Overlay, ITransientOverlay
{
    const float Width = 252;

    readonly Dictionary<PixelPaintMode, Button> toolButtons = new Dictionary<PixelPaintMode, Button>();
    VisualElement paintPart;
    VisualElement floatPart;
    ColorField colorField;
    Button glowButton;
    SliderInt glowAmount;
    VisualElement swatches;
    readonly List<VisualElement> swatchItems = new List<VisualElement>();
    readonly List<Color32> swatchColors = new List<Color32>();
    PixelPalette shownPalette;
    int shownPaletteCount = -1;
    VisualElement brushPart;
    PopupField<PixelBrush> brushField;
    SliderInt sizeSlider;
    Slider angleSlider;
    Vector2Field scaleField;
    Label info;
    bool shownFloating;

    public bool visible
    {
        get { return ToolManager.activeToolType == typeof(PixelPaintTool); }
    }

    public override void OnCreated()
    {
        PixelPaintState.Changed += Refresh;
        Undo.undoRedoPerformed += Refresh;
    }

    public override void OnWillBeDestroyed()
    {
        PixelPaintState.Changed -= Refresh;
        Undo.undoRedoPerformed -= Refresh;
    }

    public override VisualElement CreatePanelContent()
    {
        toolButtons.Clear();
        var root = new VisualElement();
        PixelUI.Setup(root);
        root.style.width = Width;
        root.style.paddingLeft = 4;
        root.style.paddingRight = 4;
        root.style.paddingTop = 4;
        root.style.paddingBottom = 4;

        paintPart = new VisualElement();
        root.Add(paintPart);
        BuildTools(paintPart);
        BuildColor(paintPart);
        BuildBrush(paintPart);

        floatPart = new VisualElement();
        root.Add(floatPart);
        BuildFloating(floatPart);

        info = PixelUI.Label("", "pd-dim");
        info.style.marginTop = 4;
        var bottom = PixelUI.Row(info, PixelUI.Spacer(),
            PixelUI.IconButton("palette", "Pixel Canvas window: palette, brush settings, symmetry, selection, canvas", OpenCanvasWindow),
            PixelUI.IconButton("onion", "Layers and timeline window", PixelLayersWindow.Open),
            PixelUI.IconButton("gear", "Pixel Drawer settings and hotkeys", PixelCanvasMenu.OpenSettings));
        bottom.AddToClassList("pd-row--nowrap");
        root.Add(bottom);

        root.schedule.Execute(Poll).Every(200);
        shownPalette = null;
        shownPaletteCount = -1;
        Refresh();
        return root;
    }

    static PixelLayer ActiveLayer()
    {
        var go = Selection.activeGameObject;
        var layer = go != null ? go.GetComponent<PixelLayer>() : null;
        return layer != null && layer.Root != null ? layer : null;
    }

    static void OpenCanvasWindow()
    {
        var layer = ActiveLayer();
        PixelCanvasWindow.Open(layer != null ? layer.Root : null);
    }

    void BuildTools(VisualElement parent)
    {
        var s = PixelDrawerAssets.Settings;
        var line = s.brushLineModifier == PixelLineModifier.Off ? "" : "\n" + s.BrushLineKeyName + " + click: line from the end of the last stroke";
        var row = PixelUI.Row(
            ToolButton(PixelPaintMode.Brush, "brush", "Brush (" + s.brushTool + ")" + line + "\nCtrl + click: select the layer under the cursor"),
            ToolButton(PixelPaintMode.Eraser, "eraser", "Eraser (" + s.eraserTool + ")" + line),
            ToolButton(PixelPaintMode.Fill, "fill", "Bucket fill (" + s.fillTool + ")\nShift: erase the area"),
            ToolButton(PixelPaintMode.Line, "line", "Line (" + s.lineTool + ")\nShift: 45 degree steps"),
            ToolButton(PixelPaintMode.Rectangle, "rect", "Rectangle (" + s.rectTool + ")\nShift: square"),
            ToolButton(PixelPaintMode.Ellipse, "ellipse", "Ellipse (" + s.ellipseTool + ")\nShift: circle"),
            ToolButton(PixelPaintMode.SelectRect, "select", "Rectangle selection (" + s.selectRectTool + ")\nShift: add, Ctrl: subtract, Shift+Ctrl: intersect"),
            ToolButton(PixelPaintMode.SelectLasso, "lasso", "Lasso selection (" + s.lassoTool + ")"),
            ToolButton(PixelPaintMode.SelectWand, "wand", "Magic wand (" + s.wandTool + ")"),
            ToolButton(PixelPaintMode.Move, "move", "Move layer or selection (" + s.moveTool + ")\nArrows: move by one pixel"));
        parent.Add(row);
    }

    Button ToolButton(PixelPaintMode mode, string icon, string tip)
    {
        var button = PixelUI.IconButton(icon, tip, () => PixelPaintState.Mode = mode);
        toolButtons[mode] = button;
        return button;
    }

    void BuildColor(VisualElement parent)
    {
        var s = PixelDrawerAssets.Settings;
        colorField = new ColorField { showAlpha = true, tooltip = "Brush color\nAlt + click: pick from the screen (also over other programs)\nAlt + " + s.pickLayers.key + " + click: pick from all layers" };
        colorField.style.flexGrow = 1;
        colorField.RegisterValueChangedCallback(e => PixelPaintState.Color = e.newValue);
        glowButton = PixelUI.IconButton("glow", "Glow: painted pixels glow (needs a Pixel Art asset layer). Glow Only mode is in the Pixel Canvas window", () =>
        {
            PixelPaintState.EmissionMode = PixelPaintState.EmissionMode == PixelEmissionMode.Off ? PixelEmissionMode.ColorAndGlow : PixelEmissionMode.Off;
        });
        glowAmount = new SliderInt(0, 255) { showInputField = true, tooltip = "Glow strength" };
        glowAmount.AddToClassList("pd-slider");
        glowAmount.style.width = 96;
        glowAmount.RegisterValueChangedCallback(e => PixelPaintState.EmissionValue = e.newValue);
        var row = PixelUI.Row(colorField, glowButton, glowAmount);
        row.AddToClassList("pd-row--nowrap");
        row.style.marginTop = 4;
        parent.Add(row);

        swatches = new VisualElement();
        swatches.AddToClassList("pd-swatches");
        swatches.tooltip = "Click: use the color";
        parent.Add(swatches);
    }

    void BuildBrush(VisualElement parent)
    {
        var s = PixelDrawerAssets.Settings;
        brushPart = new VisualElement();
        brushPart.style.marginTop = 4;
        parent.Add(brushPart);
        brushField = new PopupField<PixelBrush>(new List<PixelBrush>(PixelDrawerAssets.Brushes), PixelPaintState.Brush, b => b != null ? b.name : "None", b => b != null ? b.name : "None");
        brushField.style.flexGrow = 1;
        brushField.tooltip = "Brush";
        brushField.RegisterValueChangedCallback(e =>
        {
            if(e.newValue != null) PixelPaintState.Brush = e.newValue;
        });
        brushPart.Add(PixelUI.Row(brushField));
        sizeSlider = new SliderInt("Size", 1, 64) { showInputField = true, tooltip = "Brush size. Keys: " + s.sizeDown + " / " + s.sizeUp };
        sizeSlider.AddToClassList("pd-slider");
        sizeSlider.style.marginLeft = 0;
        sizeSlider.RegisterValueChangedCallback(e => PixelPaintState.Size = e.newValue);
        brushPart.Add(sizeSlider);
    }

    void BuildFloating(VisualElement parent)
    {
        parent.Add(PixelUI.Label("Transform: Enter = apply, Esc = cancel. Corners scale (Shift = snap, Ctrl = free), blue dot rotates", "pd-hint"));
        var apply = PixelUI.TextButton("Apply", "Apply the transform (Enter)", PixelFloating.Commit);
        var cancel = PixelUI.TextButton("Cancel", "Cancel the transform (Esc)", PixelFloating.Cancel);
        apply.style.flexGrow = 1;
        cancel.style.flexGrow = 1;
        parent.Add(PixelUI.Row(apply, cancel));
        parent.Add(PixelUI.Row(
            PixelUI.IconButton("mirror_x", "Flip horizontally", PixelFloating.FlipHorizontal),
            PixelUI.IconButton("mirror_y", "Flip vertically", PixelFloating.FlipVertical),
            PixelUI.TextButton("-90", "Rotate 90 degrees counterclockwise", () => PixelFloating.Rotate90(false)),
            PixelUI.TextButton("+90", "Rotate 90 degrees clockwise", () => PixelFloating.Rotate90(true)),
            PixelUI.TextButton("x2", "Scale x2", () => PixelFloating.MultiplyScale(2f)),
            PixelUI.TextButton("x3", "Scale x3", () => PixelFloating.MultiplyScale(3f)),
            PixelUI.TextButton("/2", "Scale /2", () => PixelFloating.MultiplyScale(0.5f)),
            PixelUI.TextButton("Reset", "Reset angle and scale", () =>
            {
                PixelFloating.SetAngle(0);
                PixelFloating.SetScale(Vector2.one);
            })));
        angleSlider = new Slider("Angle", -180f, 180f) { showInputField = true };
        angleSlider.AddToClassList("pd-slider");
        angleSlider.style.marginLeft = 0;
        angleSlider.RegisterValueChangedCallback(e => PixelFloating.SetAngle(e.newValue));
        parent.Add(angleSlider);
        scaleField = new Vector2Field("Scale");
        scaleField.RegisterValueChangedCallback(e => PixelFloating.SetScale(e.newValue));
        parent.Add(scaleField);
        parent.Add(PixelUI.Label("Nearest neighbour: exact for x2, x3, 90 degree turns and flips", "pd-hint"));
    }

    void Poll()
    {
        if(paintPart == null) return;
        if(PixelFloating.Active != shownFloating) Refresh();
        if(PixelFloating.Active)
        {
            angleSlider.SetValueWithoutNotify(PixelFloating.Angle);
            scaleField.SetValueWithoutNotify(PixelFloating.Scale);
        }
        BuildSwatchesIfNeeded();
        UpdateInfo();
    }

    void Refresh()
    {
        if(paintPart == null) return;
        shownFloating = PixelFloating.Active;
        paintPart.style.display = shownFloating ? DisplayStyle.None : DisplayStyle.Flex;
        floatPart.style.display = shownFloating ? DisplayStyle.Flex : DisplayStyle.None;

        var mode = PixelPaintState.Mode;
        foreach(var pair in toolButtons) PixelUI.SetOn(pair.Value, pair.Key == mode);

        colorField.SetValueWithoutNotify(PixelPaintState.Color);
        colorField.SetEnabled(mode != PixelPaintMode.Eraser);
        var glow = PixelPaintState.EmissionMode != PixelEmissionMode.Off;
        PixelUI.SetOn(glowButton, glow);
        glowAmount.style.display = glow ? DisplayStyle.Flex : DisplayStyle.None;
        glowAmount.SetValueWithoutNotify(PixelPaintState.EmissionValue);
        BuildSwatchesIfNeeded();
        MarkCurrent();

        var noBrush = mode == PixelPaintMode.SelectRect || mode == PixelPaintMode.SelectLasso || mode == PixelPaintMode.SelectWand || mode == PixelPaintMode.Move;
        brushPart.style.display = noBrush ? DisplayStyle.None : DisplayStyle.Flex;
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
        UpdateInfo();
    }

    void UpdateInfo()
    {
        var layer = ActiveLayer();
        if(layer == null)
        {
            info.text = "";
            return;
        }
        var canvas = layer.Root;
        var size = PixelCanvasEditing.Size(layer);
        var text = size.x + "x" + size.y + " / " + canvas.EffectiveMaxSize;
        if(canvas.FrameCount > 1) text += "   f " + (canvas.frame + 1) + "/" + canvas.FrameCount;
        if(layer.locked) text += "   locked";
        if(info.text != text) info.text = text;
        var limit = PixelCanvasEditing.LimitHit;
        info.style.color = limit ? new StyleColor(new Color(1f, 0.4f, 0.4f)) : new StyleColor(StyleKeyword.Null);
        info.tooltip = limit ? "The canvas reached its size limit. Raise Max Size in the canvas settings" : "Canvas size / size limit";
    }

    void BuildSwatchesIfNeeded()
    {
        var palette = PixelPaintState.Palette;
        if(palette != shownPalette || (palette != null && palette.colors.Count != shownPaletteCount)) BuildSwatches();
    }

    void BuildSwatches()
    {
        var palette = PixelPaintState.Palette;
        shownPalette = palette;
        shownPaletteCount = palette != null ? palette.colors.Count : 0;
        swatches.Clear();
        swatchItems.Clear();
        swatchColors.Clear();
        if(palette == null) return;
        for(var i = 0; i < palette.colors.Count; i++)
        {
            var c = palette.colors[i];
            var swatch = new VisualElement();
            swatch.AddToClassList("pd-swatch");
            swatch.style.backgroundColor = (Color)new Color32(c.r, c.g, c.b, 255);
            swatch.RegisterCallback<PointerDownEvent>(e =>
            {
                if(e.button != 0) return;
                PixelPaintState.Color = new Color32(c.r, c.g, c.b, 255);
                e.StopPropagation();
            });
            swatches.Add(swatch);
            swatchItems.Add(swatch);
            swatchColors.Add(c);
        }
        MarkCurrent();
    }

    void MarkCurrent()
    {
        var current = (Color32)PixelPaintState.Color;
        for(var i = 0; i < swatchItems.Count; i++)
        {
            var c = swatchColors[i];
            swatchItems[i].EnableInClassList("pd-swatch--current", c.r == current.r && c.g == current.g && c.b == current.b);
        }
    }
}
#endif
