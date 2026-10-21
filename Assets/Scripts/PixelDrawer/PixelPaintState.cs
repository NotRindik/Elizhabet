#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public enum PixelPaintMode { Brush, Eraser, Fill, Line, Rectangle, Ellipse, SelectRect, SelectLasso, SelectWand, Move }

public enum PixelPickSource { Screen, Layers }

public enum PixelEmissionMode { Off, ColorAndGlow, GlowOnly }

public static class PixelPaintState
{
    const string Prefix = "PixelDrawer.";
    const int MaxRecent = 20;

    static PixelPaintMode mode;
    static Color color = Color.black;
    static int size = 1;
    static PixelBrush brush;
    static string brushGuid;
    static PixelPalette palette;
    static string paletteGuid;
    static bool limitToPalette;
    static PixelEmissionMode emissionMode;
    static int emissionValue = 255;
    static readonly List<Color32> recent = new List<Color32>();

    public static Vector2 PanelPosition = new Vector2(10, 10);
    public static bool BrushSettingsOpen;
    public static bool OptionsOpen;
    public static PixelSelectMode SelectMode;
    public static int Tolerance;
    public static bool Contiguous = true;
    public static bool ShapeFilled;
    public static bool PixelPerfect;
    public static bool SymmetryX;
    public static bool SymmetryY;
    public static int AxisX2;
    public static int AxisY2;

    static PixelPaintState()
    {
        mode = (PixelPaintMode)EditorPrefs.GetInt(Prefix + "mode", 0);
        size = EditorPrefs.GetInt(Prefix + "size", 1);
        brushGuid = EditorPrefs.GetString(Prefix + "brush", "");
        paletteGuid = EditorPrefs.GetString(Prefix + "palette", "");
        limitToPalette = EditorPrefs.GetBool(Prefix + "limitPalette", false);
        emissionMode = (PixelEmissionMode)EditorPrefs.GetInt(Prefix + "emissionMode", 0);
        emissionValue = EditorPrefs.GetInt(Prefix + "emissionValue", 255);
        BrushSettingsOpen = EditorPrefs.GetBool(Prefix + "brushOpen", false);
        OptionsOpen = EditorPrefs.GetBool(Prefix + "optionsOpen", false);
        SelectMode = (PixelSelectMode)EditorPrefs.GetInt(Prefix + "selectMode", 0);
        Tolerance = EditorPrefs.GetInt(Prefix + "tolerance", 0);
        Contiguous = EditorPrefs.GetBool(Prefix + "contiguous", true);
        ShapeFilled = EditorPrefs.GetBool(Prefix + "shapeFilled", false);
        PixelPerfect = EditorPrefs.GetBool(Prefix + "pixelPerfect", false);
        SymmetryX = EditorPrefs.GetBool(Prefix + "symX", false);
        SymmetryY = EditorPrefs.GetBool(Prefix + "symY", false);
        AxisX2 = EditorPrefs.GetInt(Prefix + "axisX2", 0);
        AxisY2 = EditorPrefs.GetInt(Prefix + "axisY2", 0);
        PanelPosition = new Vector2(EditorPrefs.GetFloat(Prefix + "panelX", 10), EditorPrefs.GetFloat(Prefix + "panelY", 10));
        if(ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString(Prefix + "color", "000000FF"), out var c)) color = c;
        var recentText = EditorPrefs.GetString(Prefix + "recent", "");
        if(!string.IsNullOrEmpty(recentText))
        {
            var parts = recentText.Split(',');
            for(var i = 0; i < parts.Length; i++)
            {
                Color rc;
                if(ColorUtility.TryParseHtmlString("#" + parts[i], out rc)) recent.Add(rc);
            }
        }
    }

    public static PixelPaintMode Mode
    {
        get { return mode; }
        set
        {
            mode = value;
            Save();
        }
    }

    public static Color Color
    {
        get { return color; }
        set
        {
            color = Snap(value);
            Save();
        }
    }

    public static int Size
    {
        get { return size; }
        set
        {
            var b = Brush;
            size = b != null ? b.ClampSize(value) : Mathf.Max(1, value);
            Save();
        }
    }

    public static PixelEmissionMode EmissionMode
    {
        get { return emissionMode; }
        set
        {
            emissionMode = value;
            Save();
        }
    }

    public static int EmissionValue
    {
        get { return emissionValue; }
        set
        {
            emissionValue = Mathf.Clamp(value, 0, 255);
            Save();
        }
    }

    public static bool LimitToPalette
    {
        get { return limitToPalette; }
        set
        {
            limitToPalette = value;
            color = Snap(color);
            Save();
        }
    }

    public static IList<Color32> Recent
    {
        get { return recent; }
    }

    public static PixelBrush Brush
    {
        get
        {
            if(brush == null)
            {
                if(!string.IsNullOrEmpty(brushGuid))
                    brush = AssetDatabase.LoadAssetAtPath<PixelBrush>(AssetDatabase.GUIDToAssetPath(brushGuid));
                if(brush == null)
                {
                    var all = PixelDrawerAssets.Brushes;
                    brush = all.Count > 0 ? all[0] : null;
                }
                if(brush != null) size = brush.ClampSize(size);
            }
            return brush;
        }
        set
        {
            brush = value;
            brushGuid = value != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value)) : "";
            if(value != null) size = value.ClampSize(size);
            Save();
        }
    }

    public static PixelPalette Palette
    {
        get
        {
            if(palette == null)
            {
                if(!string.IsNullOrEmpty(paletteGuid))
                    palette = AssetDatabase.LoadAssetAtPath<PixelPalette>(AssetDatabase.GUIDToAssetPath(paletteGuid));
                if(palette == null)
                {
                    var all = PixelDrawerAssets.Palettes;
                    palette = all.Count > 0 ? all[0] : null;
                }
            }
            return palette;
        }
        set
        {
            palette = value;
            paletteGuid = value != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value)) : "";
            color = Snap(color);
            Save();
        }
    }

    public static void PushRecent(Color32 c)
    {
        for(var i = recent.Count - 1; i >= 0; i--)
        {
            var r = recent[i];
            if(r.r == c.r && r.g == c.g && r.b == c.b && r.a == c.a) recent.RemoveAt(i);
        }
        recent.Insert(0, c);
        while(recent.Count > MaxRecent) recent.RemoveAt(recent.Count - 1);
        Save();
    }

    static Color Snap(Color c)
    {
        if(!limitToPalette) return c;
        var p = Palette;
        Color32 result;
        if(p != null && p.TryNearest(c, out result))
        {
            result.a = 255;
            return result;
        }
        return c;
    }

    public static void Save()
    {
        EditorPrefs.SetInt(Prefix + "mode", (int)mode);
        EditorPrefs.SetInt(Prefix + "size", size);
        EditorPrefs.SetString(Prefix + "brush", brushGuid ?? "");
        EditorPrefs.SetString(Prefix + "palette", paletteGuid ?? "");
        EditorPrefs.SetBool(Prefix + "limitPalette", limitToPalette);
        EditorPrefs.SetInt(Prefix + "emissionMode", (int)emissionMode);
        EditorPrefs.SetInt(Prefix + "emissionValue", emissionValue);
        EditorPrefs.SetString(Prefix + "color", ColorUtility.ToHtmlStringRGBA(color));
        EditorPrefs.SetBool(Prefix + "brushOpen", BrushSettingsOpen);
        EditorPrefs.SetBool(Prefix + "optionsOpen", OptionsOpen);
        EditorPrefs.SetInt(Prefix + "selectMode", (int)SelectMode);
        EditorPrefs.SetInt(Prefix + "tolerance", Tolerance);
        EditorPrefs.SetBool(Prefix + "contiguous", Contiguous);
        EditorPrefs.SetBool(Prefix + "shapeFilled", ShapeFilled);
        EditorPrefs.SetBool(Prefix + "pixelPerfect", PixelPerfect);
        EditorPrefs.SetBool(Prefix + "symX", SymmetryX);
        EditorPrefs.SetBool(Prefix + "symY", SymmetryY);
        EditorPrefs.SetInt(Prefix + "axisX2", AxisX2);
        EditorPrefs.SetInt(Prefix + "axisY2", AxisY2);
        EditorPrefs.SetFloat(Prefix + "panelX", PanelPosition.x);
        EditorPrefs.SetFloat(Prefix + "panelY", PanelPosition.y);
        var sb = new StringBuilder();
        for(var i = 0; i < recent.Count; i++)
        {
            if(i > 0) sb.Append(',');
            sb.Append(ColorUtility.ToHtmlStringRGBA(recent[i]));
        }
        EditorPrefs.SetString(Prefix + "recent", sb.ToString());
    }
}
#endif
