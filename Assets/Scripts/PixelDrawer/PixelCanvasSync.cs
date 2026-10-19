#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class PixelCanvasSync
{
    static bool pending;

    static PixelCanvasSync()
    {
        EditorApplication.hierarchyChanged += Request;
        Undo.undoRedoPerformed += Request;
        EditorSceneManager.sceneOpened += (scene, mode) => Request();
        EditorApplication.delayCall += Request;
    }

    public static void Request()
    {
        if(pending) return;
        pending = true;
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        pending = false;
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        SyncAll();
        PixelTempView.SyncReferences();
    }

    public static List<PixelCanvas> Canvases()
    {
        var result = new List<PixelCanvas>();
        var found = Resources.FindObjectsOfTypeAll<PixelCanvas>();
        for(var i = 0; i < found.Length; i++)
        {
            var c = found[i];
            if(c == null || EditorUtility.IsPersistent(c) || !c.gameObject.scene.IsValid()) continue;
            result.Add(c);
        }
        return result;
    }

    public static List<PixelLayer> Layers()
    {
        var result = new List<PixelLayer>();
        var found = Resources.FindObjectsOfTypeAll<PixelLayer>();
        for(var i = 0; i < found.Length; i++)
        {
            var l = found[i];
            if(l == null || EditorUtility.IsPersistent(l) || !l.gameObject.scene.IsValid()) continue;
            result.Add(l);
        }
        return result;
    }

    public static void SyncAll()
    {
        var canvases = Canvases();
        for(var i = 0; i < canvases.Count; i++) Sync(canvases[i]);
    }

    public static void Sync(PixelCanvas canvas)
    {
        if(canvas == null) return;
        var group = canvas.GetComponent<SortingGroup>();
        if(group == null)
        {
            group = canvas.gameObject.AddComponent<SortingGroup>();
            EditorUtility.SetDirty(canvas);
        }
        var layers = canvas.GetComponentsInChildren<PixelLayer>(true);

        if(canvas.orderVersion < 1)
        {
            Migrate(canvas, group, layers);
            layers = canvas.GetComponentsInChildren<PixelLayer>(true);
        }

        var count = layers.Length;
        for(var i = 0; i < count; i++)
        {
            var layer = layers[i];
            var order = count - 1 - i;
            if(layer.sortIndex == order) continue;
            layer.sortIndex = order;
            EditorUtility.SetDirty(layer);
            if(PrefabUtility.IsPartOfPrefabInstance(layer)) PrefabUtility.RecordPrefabInstancePropertyModifications(layer);
            layer.Apply();
        }
    }

    static void Migrate(PixelCanvas canvas, SortingGroup group, PixelLayer[] layers)
    {
        var direct = new List<PixelLayer>();
        for(var i = 0; i < layers.Length; i++)
            if(layers[i].transform.parent == canvas.transform) direct.Add(layers[i]);

        direct.Sort((a, b) =>
        {
            var la = SortingLayer.GetLayerValueFromName(a.legacySortingLayer);
            var lb = SortingLayer.GetLayerValueFromName(b.legacySortingLayer);
            if(la != lb) return la.CompareTo(lb);
            if(a.legacySortingOrder != b.legacySortingOrder) return a.legacySortingOrder.CompareTo(b.legacySortingOrder);
            return a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex());
        });

        if(direct.Count > 0 && group.sortingLayerID == 0 && !string.IsNullOrEmpty(direct[0].legacySortingLayer))
        {
            var id = SortingLayer.NameToID(direct[0].legacySortingLayer);
            if(SortingLayer.IsValid(id)) group.sortingLayerID = id;
        }

        var n = direct.Count;
        for(var i = 0; i < n; i++) direct[n - 1 - i].transform.SetSiblingIndex(i);
        canvas.orderVersion = 1;
        EditorUtility.SetDirty(canvas);
        EditorUtility.SetDirty(group);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
    }
}
#endif
