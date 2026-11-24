#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class PixelCorrectionWindow : EditorWindow
{
    [SerializeField] PixelLayer layer;

    SerializedObject serialized;
    VisualElement fields;
    PixelCorrectionType shownType = (PixelCorrectionType)(-1);

    public static void Open(PixelLayer target)
    {
        if(target == null || !target.IsCorrection) return;
        var window = GetWindow<PixelCorrectionWindow>(true, "Correction", true);
        window.layer = target;
        window.minSize = new Vector2(300, 160);
        window.Build();
    }

    void CreateGUI()
    {
        Build();
    }

    void Build()
    {
        var root = rootVisualElement;
        root.Clear();
        root.Unbind();
        PixelUI.Setup(root);
        shownType = (PixelCorrectionType)(-1);
        if(layer == null || !layer.IsCorrection)
        {
            root.Add(PixelUI.Label("Select a correction layer", "pd-empty"));
            return;
        }
        titleContent = new GUIContent("Correction: " + layer.name);
        serialized = new SerializedObject(layer);

        var body = new VisualElement();
        body.style.paddingLeft = 10;
        body.style.paddingRight = 10;
        body.style.paddingTop = 8;
        body.style.paddingBottom = 8;
        root.Add(body);

        body.Add(new PropertyField(serialized.FindProperty("correction.type"), "Type"));
        var strength = new Slider("Strength", 0f, 1f) { bindingPath = "opacity", showInputField = true, tooltip = "How strongly the correction is applied" };
        body.Add(strength);
        body.Add(new PropertyField(serialized.FindProperty("visible"), "Enabled"));
        body.Add(PixelUI.Label("SETTINGS", "pd-sub"));
        fields = new VisualElement();
        body.Add(fields);
        body.Add(PixelUI.Label("Affects every layer below it on this canvas. With clipping on, only the layer it is clipped to.", "pd-hint"));

        root.Bind(serialized);
        root.TrackSerializedObjectValue(serialized, so => OnChanged());
        RebuildFields();
    }

    void OnChanged()
    {
        if(layer == null) return;
        if(layer.correction.type != shownType) RebuildFields();
        if(layer.Root != null) PixelCorrections.Refresh(layer.Root);
        SceneView.RepaintAll();
    }

    void RebuildFields()
    {
        if(fields == null || layer == null) return;
        fields.Clear();
        shownType = layer.correction.type;
        switch(shownType)
        {
            case PixelCorrectionType.BrightnessContrast:
                Add("brightness", "Brightness");
                Add("contrast", "Contrast");
                break;
            case PixelCorrectionType.HueSaturation:
                Add("hue", "Hue");
                Add("saturation", "Saturation");
                Add("value", "Value");
                break;
            case PixelCorrectionType.Posterize:
                Add("posterizeLevels", "Levels");
                break;
            case PixelCorrectionType.Invert:
                fields.Add(PixelUI.Label("Inverts every color below. Use Strength to blend.", "pd-hint"));
                break;
            case PixelCorrectionType.Levels:
                Add("inBlack", "Input black");
                Add("inWhite", "Input white");
                Add("gamma", "Gamma");
                Add("outBlack", "Output black");
                Add("outWhite", "Output white");
                break;
            case PixelCorrectionType.Curves:
                Add("curve", "RGB");
                Add("curveRed", "Red");
                Add("curveGreen", "Green");
                Add("curveBlue", "Blue");
                fields.Add(PixelUI.Label("Curves go from (0,0) to (1,1). Click a curve to edit it.", "pd-hint"));
                break;
            case PixelCorrectionType.ColorBalance:
                Balance("shadows", "SHADOWS");
                Balance("midtones", "MIDTONES");
                Balance("highlights", "HIGHLIGHTS");
                Add("preserveLuminosity", "Keep brightness");
                break;
            case PixelCorrectionType.Binarize:
                Add("threshold", "Threshold");
                break;
            case PixelCorrectionType.GradientMap:
                Add("gradient", "Gradient");
                fields.Add(PixelUI.Label("Dark colors take the left end of the gradient, light colors the right end.", "pd-hint"));
                break;
        }
        fields.Bind(serialized);
    }

    void Add(string name, string label)
    {
        var property = serialized.FindProperty("correction." + name);
        if(property == null) return;
        fields.Add(new PropertyField(property, label));
    }

    void Balance(string name, string title)
    {
        fields.Add(PixelUI.Label(title, "pd-sub"));
        fields.Add(BalanceSlider(name + ".x", "Cyan / Red"));
        fields.Add(BalanceSlider(name + ".y", "Magenta / Green"));
        fields.Add(BalanceSlider(name + ".z", "Yellow / Blue"));
    }

    Slider BalanceSlider(string path, string label)
    {
        var slider = new Slider(label, -100f, 100f) { bindingPath = "correction." + path, showInputField = true };
        return slider;
    }

    void OnSelectionChange()
    {
        var go = Selection.activeGameObject;
        var selected = go != null ? go.GetComponent<PixelLayer>() : null;
        if(selected == null || !selected.IsCorrection || selected == layer) return;
        layer = selected;
        Build();
    }
}
#endif
