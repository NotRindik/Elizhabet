#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PixelCanvasMenu
{
    [MenuItem("GameObject/2D Object/Pixel Canvas", false, 10)]
    static void CreateCanvas(MenuCommand command)
    {
        var go = new GameObject("Pixel Canvas", typeof(PixelCanvas));
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        var canvas = go.GetComponent<PixelCanvas>();
        canvas.id = PixelCanvasEditing.NewId();
        canvas.orderVersion = 1;
        Undo.RegisterCreatedObjectUndo(go, "Create Pixel Canvas");
        PixelLayerOps.CreateLayer(canvas, null, true);
    }

    [MenuItem("CONTEXT/PixelCanvas/Add Layer")]
    static void AddLayer(MenuCommand command)
    {
        PixelLayerOps.CreateLayer((PixelCanvas)command.context, null, true);
    }

    [MenuItem("CONTEXT/PixelLayer/Trim Empty Borders")]
    static void TrimContext(MenuCommand command)
    {
        TrimLayer((PixelLayer)command.context);
    }

    [MenuItem("CONTEXT/PixelLayer/Make Unique (own PNG)")]
    static void MakeUniqueContext(MenuCommand command)
    {
        PixelLayerOps.MakeUnique((PixelLayer)command.context);
    }

    [MenuItem("CONTEXT/PixelLayer/Duplicate Linked")]
    static void DuplicateLinkedContext(MenuCommand command)
    {
        PixelLayerOps.DuplicateLayer((PixelLayer)command.context, false);
    }

    [MenuItem("CONTEXT/PixelLayer/Duplicate Full Copy")]
    static void DuplicateFullContext(MenuCommand command)
    {
        PixelLayerOps.DuplicateLayer((PixelLayer)command.context, true);
    }

    [MenuItem("CONTEXT/PixelLayer/Merge Down")]
    static void MergeDownContext(MenuCommand command)
    {
        PixelLayerOps.MergeDown((PixelLayer)command.context);
    }

    [MenuItem("CONTEXT/PixelCanvas/Duplicate Linked")]
    static void DuplicateCanvasLinked(MenuCommand command)
    {
        PixelLayerOps.DuplicateCanvas((PixelCanvas)command.context, false);
    }

    [MenuItem("CONTEXT/PixelCanvas/Duplicate Full Copy")]
    static void DuplicateCanvasFull(MenuCommand command)
    {
        PixelLayerOps.DuplicateCanvas((PixelCanvas)command.context, true);
    }

    [MenuItem("CONTEXT/PixelCanvas/Bake To One Sprite")]
    static void BakeContext(MenuCommand command)
    {
        PixelBaker.Bake((PixelCanvas)command.context);
    }

    [MenuItem("Tools/Pixel Canvas/Settings")]
    public static void OpenSettings()
    {
        var settings = PixelDrawerAssets.Settings;
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }

    [MenuItem("Tools/Pixel Canvas/Trim Selected Layer")]
    static void TrimSelected()
    {
        var go = Selection.activeGameObject;
        var layer = go != null ? go.GetComponentInParent<PixelLayer>() : null;
        if(layer == null)
        {
            Debug.LogWarning("PixelCanvas: select a Pixel Layer first.");
            return;
        }
        TrimLayer(layer);
    }

    [MenuItem("Tools/Pixel Canvas/Create Palette From Selected Layer")]
    static void PaletteFromLayer()
    {
        var go = Selection.activeGameObject;
        var layer = go != null ? go.GetComponentInParent<PixelLayer>() : null;
        if(layer == null)
        {
            Debug.LogWarning("PixelCanvas: select a Pixel Layer first.");
            return;
        }
        Vector2Int origin;
        int w, h;
        var pixels = PixelCanvasEditing.ReadPixels(layer, out origin, out w, out h);
        if(pixels == null || w == 0) return;
        var seen = new HashSet<int>();
        var colors = new List<Color32>();
        for(var i = 0; i < pixels.Length && colors.Count < 256; i++)
        {
            var c = pixels[i];
            if(c.a == 0) continue;
            var key = (c.r << 16) | (c.g << 8) | c.b;
            if(seen.Add(key)) colors.Add(new Color32(c.r, c.g, c.b, 255));
        }
        var palette = PixelDrawerAssets.CreatePalette(layer.gameObject.name + " Palette", colors);
        PixelPaintState.Palette = palette;
        EditorGUIUtility.PingObject(palette);
    }

    [MenuItem("Tools/Pixel Canvas/Toggle Sprite Visibility In Hierarchy")]
    static void ToggleSurfaces()
    {
        var surfaces = Resources.FindObjectsOfTypeAll<PixelSurface>();
        if(surfaces.Length == 0) return;
        var hide = (surfaces[0].gameObject.hideFlags & HideFlags.HideInHierarchy) == 0;
        for(var i = 0; i < surfaces.Length; i++)
        {
            if(!surfaces[i].gameObject.scene.IsValid()) continue;
            surfaces[i].gameObject.hideFlags = hide ? HideFlags.HideInHierarchy : HideFlags.None;
        }
        EditorApplication.RepaintHierarchyWindow();
    }

    static void TrimLayer(PixelLayer layer)
    {
        if(layer.Root == null) return;
        var before = PixelCanvasEditing.Bounds(layer);
        if(PixelCanvasEditing.Trim(layer)) PixelHistory.RecordTrim(layer, before);
        SceneView.RepaintAll();
    }
}
#endif
