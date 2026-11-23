#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

public class PixelLayersWindow : EditorWindow
{
    const string ListHeightPref = "PixelDrawer.LayersListHeight";
    const float SplitterHeight = 6f;
    const float MinListHeight = 60f;
    const float MinInspectorHeight = 90f;
    const float HeaderHeight = 72f;

    static GUIStyle componentBody;

    ReorderableList list;
    PixelCanvas canvas;
    readonly List<PixelLayer> items = new List<PixelLayer>();
    PixelLayer renaming;

    Vector2 listScroll;
    Vector2 inspectorScroll;
    float listHeight;
    readonly Dictionary<Component, UnityEditor.Editor> editors = new Dictionary<Component, UnityEditor.Editor>();
    readonly List<Component> staleEditors = new List<Component>();
    GameObject inspected;

    [MenuItem("Window/Pixel Canvas/Layers")]
    public static void Open()
    {
        var w = GetWindow<PixelLayersWindow>("Pixel Layers");
        w.Show();
    }

    void OnEnable()
    {
        listHeight = EditorPrefs.GetFloat(ListHeightPref, 220f);
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
        ClearEditors();
    }

    void OnInspectorUpdate()
    {
        if(inspected != null) Repaint();
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

        var go = Selection.activeGameObject;
        var layer = go != null ? go.GetComponent<PixelLayer>() : null;

        GUILayout.Label(canvas.name, EditorStyles.boldLabel);
        DrawLayerButtons(layer);
        DrawList();
        DrawSplitter();
        DrawOpacity(layer);
        DrawInspector(layer);
    }

    void DrawLayerButtons(PixelLayer layer)
    {
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
        }
    }

    void DrawList()
    {
        if(list == null) BuildList();
        var maxHeight = Mathf.Max(MinListHeight, position.height - HeaderHeight - SplitterHeight - MinInspectorHeight);
        listHeight = Mathf.Clamp(listHeight, MinListHeight, maxHeight);

        listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Height(listHeight));
        list.DoLayoutList();
        EditorGUILayout.EndScrollView();
    }

    void DrawSplitter()
    {
        var e = Event.current;
        var rect = GUILayoutUtility.GetRect(0, SplitterHeight, GUILayout.ExpandWidth(true));
        EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);
        var id = GUIUtility.GetControlID(FocusType.Passive);

        if(e.type == EventType.Repaint)
        {
            var hot = GUIUtility.hotControl == id;
            var line = EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.6f) : new Color(0f, 0f, 0f, 0.3f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.center.y - 1, rect.width, 2), hot ? new Color(0.27f, 0.55f, 0.95f) : line);
        }

        switch(e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
                if(e.button == 0 && rect.Contains(e.mousePosition))
                {
                    GUIUtility.hotControl = id;
                    e.Use();
                }
                break;
            case EventType.MouseDrag:
                if(GUIUtility.hotControl == id)
                {
                    listHeight += e.delta.y;
                    Repaint();
                    e.Use();
                }
                break;
            case EventType.MouseUp:
                if(GUIUtility.hotControl == id)
                {
                    GUIUtility.hotControl = 0;
                    EditorPrefs.SetFloat(ListHeightPref, listHeight);
                    e.Use();
                }
                break;
        }
    }

    void DrawOpacity(PixelLayer layer)
    {
        if(layer == null) return;
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

    void DrawInspector(PixelLayer layer)
    {
        if(layer == null)
        {
            if(inspected != null) ClearEditors();
            EditorGUILayout.HelpBox("Select a layer to see its components.", MessageType.None);
            return;
        }

        var go = layer.gameObject;
        if(go != inspected)
        {
            ClearEditors();
            inspected = go;
            inspectorScroll = Vector2.zero;
        }
        PruneEditors();

        var oldWide = EditorGUIUtility.wideMode;
        var oldLabel = EditorGUIUtility.labelWidth;
        EditorGUIUtility.wideMode = position.width > 330f;
        EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.4f, 90f, 160f);

        inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
        var components = go.GetComponents<Component>();
        for(var i = 0; i < components.Length; i++)
        {
            var component = components[i];
            if(component == null)
            {
                EditorGUILayout.HelpBox("Missing script", MessageType.Warning);
                continue;
            }
            DrawComponent(component);
        }

        GUILayout.Space(6);
        var content = new GUIContent("Add Component");
        var rect = GUILayoutUtility.GetRect(content, GUI.skin.button, GUILayout.Height(24));
        rect.x += rect.width * 0.15f;
        rect.width *= 0.7f;
        if(GUI.Button(rect, content)) new PixelComponentDropdown(new AdvancedDropdownState(), go).Show(rect);
        GUILayout.Space(6);
        EditorGUILayout.EndScrollView();

        EditorGUIUtility.wideMode = oldWide;
        EditorGUIUtility.labelWidth = oldLabel;
    }

    void DrawComponent(Component component)
    {
        var key = "plw.fold." + component.GetType().FullName;
        var open = SessionState.GetBool(key, !(component is Transform));
        var next = EditorGUILayout.InspectorTitlebar(open, component);
        if(next != open) SessionState.SetBool(key, next);
        if(!next || component == null) return;

        UnityEditor.Editor editor;
        editors.TryGetValue(component, out editor);
        UnityEditor.Editor.CreateCachedEditor(component, null, ref editor);
        editors[component] = editor;
        if(editor == null) return;

        if(componentBody == null) componentBody = new GUIStyle { padding = new RectOffset(14, 4, 2, 6) };
        EditorGUILayout.BeginVertical(componentBody);
        editor.OnInspectorGUI();
        EditorGUILayout.EndVertical();
    }

    void PruneEditors()
    {
        staleEditors.Clear();
        foreach(var pair in editors)
            if(pair.Key == null) staleEditors.Add(pair.Key);
        for(var i = 0; i < staleEditors.Count; i++)
        {
            var editor = editors[staleEditors[i]];
            if(editor != null) DestroyImmediate(editor);
            editors.Remove(staleEditors[i]);
        }
    }

    void ClearEditors()
    {
        foreach(var editor in editors.Values)
            if(editor != null) DestroyImmediate(editor);
        editors.Clear();
        inspected = null;
    }
}
#endif
