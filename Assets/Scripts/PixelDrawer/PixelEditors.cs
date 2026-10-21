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
        if(GUILayout.Button("Open Layers window")) PixelLayersWindow.Open();
        DrawArt(layer);
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

public class PixelLayersWindow : EditorWindow
{
    ReorderableList list;
    PixelCanvas canvas;
    readonly List<PixelLayer> items = new List<PixelLayer>();
    PixelLayer renaming;

    [MenuItem("Window/Pixel Canvas/Layers")]
    public static void Open()
    {
        var w = GetWindow<PixelLayersWindow>("Pixel Layers");
        w.Show();
    }

    void OnEnable()
    {
        Selection.selectionChanged += Reload;
        EditorApplication.hierarchyChanged += Reload;
        Undo.undoRedoPerformed += Reload;
        Reload();
    }

    void OnDisable()
    {
        Selection.selectionChanged -= Reload;
        EditorApplication.hierarchyChanged -= Reload;
        Undo.undoRedoPerformed -= Reload;
    }

    void Reload()
    {
        var go = Selection.activeGameObject;
        PixelCanvas found = go != null ? go.GetComponentInParent<PixelCanvas>() : null;
        if(found != null) canvas = found;
        items.Clear();
        if(canvas != null)
        {
            for(var i = 0; i < canvas.transform.childCount; i++)
            {
                var l = canvas.transform.GetChild(i).GetComponent<PixelLayer>();
                if(l != null) items.Add(l);
            }
        }
        if(list == null) BuildList();
        list.list = items;
        var active = go != null ? go.GetComponent<PixelLayer>() : null;
        list.index = active != null ? items.IndexOf(active) : -1;
        Repaint();
    }

    void BuildList()
    {
        list = new ReorderableList(items, typeof(PixelLayer), true, false, false, false);
        list.elementHeight = 26;
        list.drawElementCallback = DrawElement;
        list.onSelectCallback = l =>
        {
            if(l.index >= 0 && l.index < items.Count) Selection.activeGameObject = items[l.index].gameObject;
        };
        list.onReorderCallback = l => ApplyOrder();
    }

    void ApplyOrder()
    {
        if(canvas == null) return;
        Undo.RegisterChildrenOrderUndo(canvas.transform, "Reorder Pixel Layers");
        for(var i = 0; i < items.Count; i++) items[i].transform.SetSiblingIndex(i);
        PixelCanvasSync.Sync(canvas);
        EditorUtility.SetDirty(canvas);
    }

    void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        if(index >= items.Count) return;
        var layer = items[index];
        if(layer == null) return;
        var y = rect.y + 3;
        var h = 20f;
        var x = rect.x;

        var vis = GUI.Toggle(new Rect(x, y, 22, h), layer.visible, new GUIContent("V", "Visible"), EditorStyles.miniButton);
        if(vis != layer.visible)
        {
            Undo.RecordObject(layer, "Layer Visibility");
            layer.visible = vis;
            layer.Apply();
            EditorUtility.SetDirty(layer);
        }
        x += 24;
        var lck = GUI.Toggle(new Rect(x, y, 22, h), layer.locked, new GUIContent("L", "Locked: no drawing"), EditorStyles.miniButton);
        if(lck != layer.locked)
        {
            Undo.RecordObject(layer, "Layer Lock");
            layer.locked = lck;
            EditorUtility.SetDirty(layer);
        }
        x += 26;

        var thumb = new Rect(x, y, h, h);
        EditorGUI.DrawRect(thumb, new Color(0.25f, 0.25f, 0.25f));
        var sr = layer.Surface(false);
        if(sr != null && sr.sprite != null && sr.sprite.texture != null)
            GUI.DrawTexture(thumb, sr.sprite.texture, ScaleMode.ScaleToFit);
        x += h + 4;

        var link = PixelLayerOps.LinkedLayers(layer).Count;
        var nameRect = new Rect(x, y, rect.xMax - x - (link > 1 ? 34 : 0), h);
        if(renaming == layer)
        {
            GUI.SetNextControlName("pixelLayerRename");
            var text = EditorGUI.DelayedTextField(nameRect, layer.gameObject.name);
            if(text != layer.gameObject.name)
            {
                Undo.RecordObject(layer.gameObject, "Rename Layer");
                layer.gameObject.name = text;
                renaming = null;
            }
            if(Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) renaming = null;
        }
        else
        {
            GUI.Label(nameRect, layer.gameObject.name);
            if(Event.current.type == EventType.MouseDown && Event.current.clickCount == 2 && nameRect.Contains(Event.current.mousePosition))
            {
                renaming = layer;
                Event.current.Use();
            }
        }
        if(link > 1) GUI.Label(new Rect(rect.xMax - 32, y, 32, h), "x" + link, EditorStyles.miniLabel);
    }

    void OnGUI()
    {
        if(canvas == null)
        {
            EditorGUILayout.HelpBox("Select a Pixel Canvas or a Pixel Layer.", MessageType.Info);
            if(GUILayout.Button("Create Pixel Canvas")) PixelLayerOps.CreateCanvas();
            return;
        }
        GUILayout.Label(canvas.name, EditorStyles.boldLabel);
        if(list == null) BuildList();
        list.DoLayoutList();

        var go = Selection.activeGameObject;
        var layer = go != null ? go.GetComponent<PixelLayer>() : null;

        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button("New")) PixelLayerOps.CreateLayer(canvas, layer, true);
        using(new EditorGUI.DisabledScope(layer == null))
        {
            if(GUILayout.Button("Dup Linked") && layer != null) PixelLayerOps.DuplicateLayer(layer, false);
            if(GUILayout.Button("Dup Full") && layer != null) PixelLayerOps.DuplicateLayer(layer, true);
        }
        EditorGUILayout.EndHorizontal();
        using(new EditorGUI.DisabledScope(layer == null))
        {
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Merge Down") && layer != null) PixelLayerOps.MergeDown(layer);
            if(GUILayout.Button("Make Unique") && layer != null) PixelLayerOps.MakeUnique(layer);
            if(GUILayout.Button("Delete") && layer != null) PixelLayerOps.DeleteLayer(layer);
            EditorGUILayout.EndHorizontal();
            if(layer != null)
            {
                EditorGUI.BeginChangeCheck();
                var opacity = EditorGUILayout.Slider("Opacity", layer.opacity, 0f, 1f);
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(layer, "Layer Opacity");
                    layer.opacity = opacity;
                    layer.Apply();
                    EditorUtility.SetDirty(layer);
                }
            }
        }
    }
}
#endif
