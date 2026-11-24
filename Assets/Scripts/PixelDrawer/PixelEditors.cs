#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Editor = UnityEditor.Editor;

[CustomEditor(typeof(PixelLayer))]
public class PixelLayerEditor : UnityEditor.Editor
{
    int linkCount;
    string path;
    double nextRefresh;

    void Refresh()
    {
        var layer = (PixelLayer)target;
        path = PixelCanvasEditing.PathOf(layer);
        linkCount = PixelLayerOps.LinkedLayers(layer).Count;
        nextRefresh = EditorApplication.timeSinceStartup + 1.0;
    }

    void OnEnable()
    {
        Refresh();
    }

    public override void OnInspectorGUI()
    {
        var layer = (PixelLayer)target;
        if(layer.IsCorrection)
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Correction layer: " + ObjectNames.NicifyVariableName(layer.correction.type.ToString()) + ". It changes the colors of the layers below without touching their pixels.", MessageType.None);
            if(GUILayout.Button("Edit Correction")) PixelCorrectionWindow.Open(layer);
            DrawClip(layer);
            if(GUILayout.Button("Open Layers window")) PixelLayersWindow.Open();
            return;
        }
        if(EditorApplication.timeSinceStartup > nextRefresh) Refresh();
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if(!string.IsNullOrEmpty(path)) EditorGUILayout.LabelField(layer.art != null ? "Asset" : "PNG", path, EditorStyles.miniLabel);
        if(linkCount > 1)
        {
            EditorGUILayout.HelpBox("Linked: this pixel data is used by " + linkCount + " layers. Painting here changes all of them.", MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Make Unique")) MakeUnique(layer);
            if(GUILayout.Button("Select Linked")) SelectLinked(layer);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button("Duplicate Linked")) PixelLayerOps.DuplicateLayer(layer, false);
        if(GUILayout.Button("Duplicate Full Copy")) PixelLayerOps.DuplicateLayer(layer, true);
        EditorGUILayout.EndHorizontal();
        DrawClip(layer);
        if(GUILayout.Button("Open Layers window")) PixelLayersWindow.Open();
        DrawArt(layer);
    }

    static void DrawClip(PixelLayer layer)
    {
        var label = layer.clip ? "Release Clipping" : "Clip With Layer Below";
        var tip = "Clipping layer: shows only over the pixels of the layer below, like in Clip Studio.";
        var prev = GUI.backgroundColor;
        if(layer.clip) GUI.backgroundColor = PixelLayersWindow.ClipButtonColor;
        if(GUILayout.Button(new GUIContent(label, tip))) PixelLayerOps.SetClip(layer, !layer.clip);
        GUI.backgroundColor = prev;
        if(layer.clip && !layer.clipped) EditorGUILayout.HelpBox("No layer below to clip to: drawn as a normal layer.", MessageType.None);
    }

    static void Defer(System.Action action)
    {
        EditorApplication.delayCall += () => action();
    }

    static void DrawArt(PixelLayer layer)
    {
        EditorGUILayout.Space();
        var art = layer.art;
        if(art == null)
        {
            EditorGUILayout.HelpBox("Storage: PNG file. It shows the same image on every frame of the canvas. Convert it to a Pixel Art asset to give it its own frames and per-pixel glow. The PNG stays in the project.", MessageType.None);
            if(GUILayout.Button("Convert to Pixel Art asset")) Defer(() => PixelArtOps.ConvertLayer(layer));
            return;
        }

        using(new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("Pixel Art", art, typeof(PixelArtAsset), false);
        EditorGUILayout.LabelField("Frames", art.frames.Count + "  (shown: " + (layer.frame + 1) + ", follows the canvas timeline)", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button("Export frame PNG")) Defer(() => PixelArtOps.ExportPngs(layer, false));
        if(GUILayout.Button("Export all frames")) Defer(() => PixelArtOps.ExportPngs(layer, true));
        EditorGUILayout.EndHorizontal();
    }

    static void MakeUnique(PixelLayer layer)
    {
        PixelLayerOps.MakeUnique(layer);
    }

    static void SelectLinked(PixelLayer layer)
    {
        var linked = PixelLayerOps.LinkedLayers(layer);
        var objs = new Object[linked.Count];
        for(var i = 0; i < objs.Length; i++) objs[i] = linked[i].gameObject;
        Selection.objects = objs;
    }
}

[CustomEditor(typeof(PixelCanvas))]
public class PixelCanvasEditor : UnityEditor.Editor
{
    static bool advanced;

    public override void OnInspectorGUI()
    {
        var canvas = (PixelCanvas)target;
        EditorGUILayout.HelpBox("Canvas settings, animation, palette and brush settings live in the Pixel Canvas window. It follows the selected canvas or any of its layers.", MessageType.None);
        if(GUILayout.Button("Open Pixel Canvas window")) PixelCanvasWindow.Open(canvas);
        advanced = EditorGUILayout.Foldout(advanced, "All fields (advanced)", true);
        if(advanced) DrawDefaultInspector();
    }
}
#endif
