#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class PixelAdjustWindow : EditorWindow
{
    PixelLayer layer;
    Color32[] snapshot;
    byte[] glow;
    Vector2Int origin;
    int width;
    int height;
    PixelMask mask;
    int strokeId;
    bool finished;
    bool dirty;

    SliderInt hueSlider;
    SliderInt saturationSlider;
    SliderInt valueSlider;
    Toggle previewToggle;

    public static void Open(PixelLayer target)
    {
        if(target == null || target.Root == null) return;
        if(target.locked)
        {
            EditorUtility.DisplayDialog("Pixel Canvas", "The layer is locked.", "OK");
            return;
        }
        var open = Resources.FindObjectsOfTypeAll<PixelAdjustWindow>();
        for(var i = 0; i < open.Length; i++) open[i].Close();

        PixelFloating.CommitIfActive();
        var window = CreateInstance<PixelAdjustWindow>();
        window.titleContent = new GUIContent("Hue / Saturation / Value");
        if(!window.Begin(target))
        {
            DestroyImmediate(window);
            return;
        }
        window.minSize = new Vector2(320, 150);
        window.maxSize = new Vector2(600, 150);
        window.ShowUtility();
    }

    bool Begin(PixelLayer target)
    {
        layer = target;
        var pixels = PixelCanvasEditing.ReadPixels(layer, out origin, out width, out height);
        if(pixels == null || pixels.Length == 0) return false;
        snapshot = (Color32[])pixels.Clone();
        var emission = PixelCanvasEditing.ReadEmission(layer);
        glow = emission != null ? (byte[])emission.Clone() : null;
        mask = PixelSelection.For(layer.Root);
        PixelHistory.BeginStroke(layer);
        strokeId = PixelHistory.StrokeId;
        return true;
    }

    void CreateGUI()
    {
        var root = rootVisualElement;
        PixelUI.Setup(root);
        root.style.paddingLeft = 10;
        root.style.paddingRight = 10;
        root.style.paddingTop = 8;
        root.focusable = true;
        root.RegisterCallback<KeyDownEvent>(e =>
        {
            if(e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Commit();
            else if(e.keyCode == KeyCode.Escape) Cancel();
            else return;
            e.StopPropagation();
        });

        hueSlider = Slider("Hue", -180, 180, "Shifts every color around the color wheel, in degrees");
        saturationSlider = Slider("Saturation", -100, 100, "-100 = grey, +100 = fully saturated");
        valueSlider = Slider("Value", -100, 100, "-100 = black, +100 = full brightness");
        root.Add(hueSlider);
        root.Add(saturationSlider);
        root.Add(valueSlider);

        previewToggle = new Toggle("Preview") { value = true };
        previewToggle.RegisterValueChangedCallback(e => dirty = true);
        var buttons = PixelUI.Row(
            previewToggle,
            PixelUI.Spacer(),
            PixelUI.TextButton("Reset", "Back to zero", ResetValues),
            PixelUI.TextButton("Cancel", "Close without changes (Esc)", Cancel),
            PixelUI.TextButton("OK", "Apply to the layer (Enter)", Commit));
        buttons.AddToClassList("pd-row--nowrap");
        buttons.style.marginTop = 6;
        buttons[buttons.childCount - 1].AddToClassList("pd-btn--accent");
        root.Add(buttons);

        root.schedule.Execute(Tick).Every(50);
        root.schedule.Execute(() => root.Focus());
    }

    SliderInt Slider(string label, int min, int max, string tip)
    {
        var slider = new SliderInt(label, min, max) { value = 0, showInputField = true, tooltip = tip };
        slider.AddToClassList("pd-slider");
        slider.RegisterValueChangedCallback(e => dirty = true);
        return slider;
    }

    void ResetValues()
    {
        hueSlider.value = 0;
        saturationSlider.value = 0;
        valueSlider.value = 0;
    }

    bool StillOwnsStroke()
    {
        return layer != null && PixelHistory.IsOpen && PixelHistory.StrokeId == strokeId;
    }

    void Tick()
    {
        if(finished) return;
        if(!StillOwnsStroke())
        {
            finished = true;
            Close();
            return;
        }
        if(!dirty) return;
        dirty = false;
        Render(previewToggle.value);
    }

    void Render(bool apply)
    {
        PixelHistory.Rollback();
        var dh = hueSlider.value / 360f;
        var ds = saturationSlider.value / 100f;
        var dv = valueSlider.value / 100f;
        if(apply && (dh != 0 || ds != 0 || dv != 0))
        {
            for(var y = 0; y < height; y++)
            {
                for(var x = 0; x < width; x++)
                {
                    var i = y * width + x;
                    var c = snapshot[i];
                    if(c.a == 0) continue;
                    var wx = origin.x + x;
                    var wy = origin.y + y;
                    if(mask != null && !mask.Contains(wx, wy)) continue;
                    var n = Adjust(c, dh, ds, dv);
                    if(n.r == c.r && n.g == c.g && n.b == c.b) continue;
                    PixelCanvasEditing.SetPixel(layer, wx, wy, n, glow != null ? glow[i] : 0);
                }
            }
        }
        PixelCanvasEditing.Apply();
        SceneView.RepaintAll();
    }

    public static Color32 Adjust(Color32 c, float dh, float ds, float dv)
    {
        float h, s, v;
        Color.RGBToHSV(c, out h, out s, out v);
        h = Mathf.Repeat(h + dh, 1f);
        s = ds < 0 ? s * (1f + ds) : s + (1f - s) * ds;
        v = dv < 0 ? v * (1f + dv) : v + (1f - v) * dv;
        var rgb = Color.HSVToRGB(h, Mathf.Clamp01(s), Mathf.Clamp01(v));
        var result = (Color32)rgb;
        result.a = c.a;
        return result;
    }

    void Commit()
    {
        if(finished) return;
        finished = true;
        if(StillOwnsStroke())
        {
            Render(true);
            PixelHistory.EndStroke();
        }
        Close();
    }

    void Cancel()
    {
        if(finished) return;
        finished = true;
        Discard();
        Close();
    }

    void Discard()
    {
        if(!StillOwnsStroke()) return;
        PixelHistory.Rollback();
        PixelHistory.Discard();
        PixelCanvasEditing.Apply();
        SceneView.RepaintAll();
    }

    void OnDestroy()
    {
        if(finished) return;
        finished = true;
        Discard();
    }
}
#endif
