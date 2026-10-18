#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public enum PixelPaintMode { Brush, Eraser }

// Screen: color as it appears on screen (includes scene effects). Layers: composite of all visible layers of the canvas.
public enum PixelPickSource { Screen, Layers }

// Current tool state (mode, brush, size, color, panel position). Persisted in EditorPrefs.
public static class PixelPaintState
{
    const string Prefix = "PixelDrawer.";

    static PixelPaintMode mode;
    static Color color = Color.black;
    static int size = 1;
    static PixelBrush brush;
    static string brushGuid;

    public static Vector2 PanelPosition = new Vector2(10, 10);
    public static bool BrushSettingsOpen;

    static PixelPaintState()
    {
        mode = (PixelPaintMode)EditorPrefs.GetInt(Prefix + "mode", 0);
        size = EditorPrefs.GetInt(Prefix + "size", 1);
        brushGuid = EditorPrefs.GetString(Prefix + "brush", "");
        BrushSettingsOpen = EditorPrefs.GetBool(Prefix + "brushOpen", false);
        PanelPosition = new Vector2(EditorPrefs.GetFloat(Prefix + "panelX", 10), EditorPrefs.GetFloat(Prefix + "panelY", 10));
        if(ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString(Prefix + "color", "000000FF"), out var c)) color = c;
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
            color = value;
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

    public static void Save()
    {
        EditorPrefs.SetInt(Prefix + "mode", (int)mode);
        EditorPrefs.SetInt(Prefix + "size", size);
        EditorPrefs.SetString(Prefix + "brush", brushGuid ?? "");
        EditorPrefs.SetString(Prefix + "color", ColorUtility.ToHtmlStringRGBA(color));
        EditorPrefs.SetBool(Prefix + "brushOpen", BrushSettingsOpen);
        EditorPrefs.SetFloat(Prefix + "panelX", PanelPosition.x);
        EditorPrefs.SetFloat(Prefix + "panelY", PanelPosition.y);
    }
}
#endif
