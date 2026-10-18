using UnityEditor;
using UnityEngine;

public static class PixelCanvasMenu
{
    [MenuItem("GameObject/2D Object/Pixel Canvas", false, 10)]
    static void CreateCanvas(MenuCommand command)
    {
        var go = new GameObject("Pixel Canvas", typeof(PixelCanvas));
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create Pixel Canvas");
        CreateLayer(go.GetComponent<PixelCanvas>());
    }

    [MenuItem("CONTEXT/PixelCanvas/Add Layer")]
    static void AddLayer(MenuCommand command)
    {
        CreateLayer((PixelCanvas)command.context);
    }

    [MenuItem("CONTEXT/PixelLayer/Trim Empty Borders")]
    static void TrimContext(MenuCommand command)
    {
        TrimLayer((PixelLayer)command.context);
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
        PixelCanvasEditing.Trim(layer);
        PixelCanvasEditing.SaveAll();
        SceneView.RepaintAll();
    }

    static void CreateLayer(PixelCanvas canvas)
    {
        var index = canvas.transform.childCount;
        var go = new GameObject("Layer " + (index + 1), typeof(PixelLayer));
        go.transform.SetParent(canvas.transform, false);
        var layer = go.GetComponent<PixelLayer>();
        layer.id = System.Guid.NewGuid().ToString("N").Substring(0, 8);
        layer.sortingOrder = index;
        Undo.RegisterCreatedObjectUndo(go, "Add Pixel Layer");
        Selection.activeGameObject = go;
    }
}
