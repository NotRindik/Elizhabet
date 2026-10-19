#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PixelLayerOps
{
    public static PixelLayer CreateLayer(PixelCanvas canvas, PixelLayer above, bool select)
    {
        var count = canvas.GetComponentsInChildren<PixelLayer>(true).Length;
        var go = new GameObject(GameObjectUtility.GetUniqueNameForSibling(canvas.transform, "Layer " + (count + 1)), typeof(PixelLayer));
        go.transform.SetParent(canvas.transform, false);
        var layer = go.GetComponent<PixelLayer>();
        layer.id = PixelCanvasEditing.NewId();
        var index = above != null && above.transform.parent == canvas.transform ? above.transform.GetSiblingIndex() : 0;
        go.transform.SetSiblingIndex(index);
        Undo.RegisterCreatedObjectUndo(go, "Add Pixel Layer");
        if(select) Selection.activeGameObject = go;
        PixelCanvasSync.Sync(canvas);
        return layer;
    }

    public static PixelCanvas CreateCanvas()
    {
        var go = new GameObject("Pixel Canvas", typeof(PixelCanvas));
        var canvas = go.GetComponent<PixelCanvas>();
        canvas.id = PixelCanvasEditing.NewId();
        canvas.orderVersion = 1;
        if(SceneView.lastActiveSceneView != null) go.transform.position = SceneView.lastActiveSceneView.pivot;
        Undo.RegisterCreatedObjectUndo(go, "Create Pixel Canvas");
        CreateLayer(canvas, null, true);
        return canvas;
    }

    public static List<PixelLayer> LinkedLayers(PixelLayer layer)
    {
        var result = new List<PixelLayer>();
        var path = PixelCanvasEditing.PathOf(layer);
        if(string.IsNullOrEmpty(path)) return result;
        var all = PixelCanvasSync.Layers();
        for(var i = 0; i < all.Count; i++)
            if(PixelCanvasEditing.PathOf(all[i]) == path) result.Add(all[i]);
        return result;
    }

    public static bool MakeUnique(PixelLayer layer)
    {
        return MakeUnique(layer, null);
    }

    public static bool MakeUnique(PixelLayer layer, string folder)
    {
        PixelFloating.CommitIfActive();
        PixelCanvasEditing.SaveAll();
        var canvas = layer.Root;
        var newId = PixelCanvasEditing.NewId();
        var path = PixelCanvasEditing.PathOf(layer);
        Undo.RecordObject(layer, "Make Layer Unique");
        if(string.IsNullOrEmpty(path))
        {
            layer.id = newId;
            EditorUtility.SetDirty(layer);
            return false;
        }
        if(string.IsNullOrEmpty(folder)) folder = PixelCanvasEditing.FolderOf(canvas);
        PixelDrawerAssets.EnsureFolder(folder);
        var newPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + newId + ".png");
        if(!AssetDatabase.CopyAsset(path, newPath))
        {
            Debug.LogError("PixelCanvas: could not copy " + path + " to " + newPath);
            return false;
        }
        AssetDatabase.ImportAsset(newPath, ImportAssetOptions.ForceSynchronousImport);
        PixelCanvasEditing.Configure(newPath, canvas.pixelsPerUnit, layer.origin);
        var sprite = PixelCanvasEditing.LoadSprite(newPath);
        if(sprite == null) return false;
        var sr = layer.Surface(true);
        Undo.RecordObject(sr, "Make Layer Unique");
        layer.id = newId;
        sr.sprite = sprite;
        EditorUtility.SetDirty(layer);
        EditorUtility.SetDirty(sr);
        EditorSceneManager.MarkSceneDirty(layer.gameObject.scene);
        return true;
    }

    public static PixelLayer DuplicateLayer(PixelLayer layer, bool fullCopy)
    {
        PixelFloating.CommitIfActive();
        PixelCanvasEditing.SaveAll();
        var copy = Object.Instantiate(layer.gameObject, layer.transform.parent);
        CleanTemp(copy);
        copy.name = GameObjectUtility.GetUniqueNameForSibling(layer.transform.parent, layer.gameObject.name + " copy");
        copy.transform.SetSiblingIndex(layer.transform.GetSiblingIndex());
        Undo.RegisterCreatedObjectUndo(copy, fullCopy ? "Duplicate Layer (Full Copy)" : "Duplicate Layer (Linked)");
        var copyLayer = copy.GetComponent<PixelLayer>();
        FinishLayerDuplicate(copyLayer, fullCopy);
        Selection.activeGameObject = copy;
        if(copyLayer.Root != null) PixelCanvasSync.Sync(copyLayer.Root);
        return copyLayer;
    }

    public static void FinishLayerDuplicate(PixelLayer copy, bool fullCopy)
    {
        FinishLayerDuplicate(copy, fullCopy, null);
    }

    public static void FinishLayerDuplicate(PixelLayer copy, bool fullCopy, PixelLayer source)
    {
        if(source != null)
        {
            var from = source.Surface(false);
            if(from != null)
            {
                var to = copy.Surface(true);
                to.sprite = from.sprite;
                copy.origin = source.origin;
                copy.PlaceSurface();
            }
        }
        if(fullCopy)
        {
            MakeUnique(copy);
        }
        else
        {
            Undo.RecordObject(copy, "Duplicate Layer");
            copy.id = PixelCanvasEditing.NewId();
            EditorUtility.SetDirty(copy);
        }
    }

    public static PixelCanvas DuplicateCanvas(PixelCanvas canvas, bool fullCopy)
    {
        PixelFloating.CommitIfActive();
        PixelCanvasEditing.SaveAll();
        var copyGo = Object.Instantiate(canvas.gameObject, canvas.transform.parent);
        if(canvas.transform.parent == null) SceneManager.MoveGameObjectToScene(copyGo, canvas.gameObject.scene);
        CleanTemp(copyGo);
        copyGo.name = GameObjectUtility.GetUniqueNameForSibling(canvas.transform.parent, canvas.gameObject.name + " copy");
        Undo.RegisterCreatedObjectUndo(copyGo, fullCopy ? "Duplicate Canvas (Full Copy)" : "Duplicate Canvas (Linked)");
        var copy = copyGo.GetComponent<PixelCanvas>();
        FinishCanvasDuplicate(copy, fullCopy);
        Selection.activeGameObject = copyGo;
        return copy;
    }

    public static void FinishCanvasDuplicate(PixelCanvas copy, bool fullCopy)
    {
        FinishCanvasDuplicate(copy, fullCopy, null);
    }

    public static void FinishCanvasDuplicate(PixelCanvas copy, bool fullCopy, PixelCanvas source)
    {
        CleanTemp(copy.gameObject);
        if(source != null)
        {
            var from = source.GetComponentsInChildren<PixelLayer>(true);
            var to = copy.GetComponentsInChildren<PixelLayer>(true);
            for(var i = 0; i < Mathf.Min(from.Length, to.Length); i++)
            {
                var sr = from[i].Surface(false);
                if(sr == null) continue;
                to[i].Surface(true).sprite = sr.sprite;
                to[i].origin = from[i].origin;
                to[i].PlaceSurface();
            }
        }
        Undo.RecordObject(copy, "Duplicate Canvas");
        copy.id = PixelCanvasEditing.NewId();
        copy.baked = null;
        copy.useBakedInPlay = false;
        var layers = copy.GetComponentsInChildren<PixelLayer>(true);
        if(fullCopy)
        {
            copy.assetFolder = null;
            var folder = PixelCanvasEditing.NewFolderFor(copy);
            copy.assetFolder = folder;
            for(var i = 0; i < layers.Length; i++) MakeUnique(layers[i], folder);
        }
        else
        {
            for(var i = 0; i < layers.Length; i++)
            {
                Undo.RecordObject(layers[i], "Duplicate Canvas");
                layers[i].id = PixelCanvasEditing.NewId();
                EditorUtility.SetDirty(layers[i]);
            }
        }
        EditorUtility.SetDirty(copy);
        PixelCanvasSync.Sync(copy);
        EditorSceneManager.MarkSceneDirty(copy.gameObject.scene);
    }

    public static void MakeCanvasUnique(PixelCanvas canvas)
    {
        PixelFloating.CommitIfActive();
        PixelCanvasEditing.SaveAll();
        Undo.RecordObject(canvas, "Make Canvas Unique");
        canvas.assetFolder = null;
        var folder = PixelCanvasEditing.NewFolderFor(canvas);
        canvas.assetFolder = folder;
        var layers = canvas.GetComponentsInChildren<PixelLayer>(true);
        for(var i = 0; i < layers.Length; i++) MakeUnique(layers[i], folder);
        EditorUtility.SetDirty(canvas);
    }

    static void CleanTemp(GameObject root)
    {
        var markers = root.GetComponentsInChildren<PixelTempMarker>(true);
        for(var i = 0; i < markers.Length; i++)
            if(markers[i] != null) Object.DestroyImmediate(markers[i].gameObject);
    }

    public static PixelLayer LayerBelow(PixelLayer layer)
    {
        var parent = layer.transform.parent;
        if(parent == null) return null;
        for(var i = layer.transform.GetSiblingIndex() + 1; i < parent.childCount; i++)
        {
            var other = parent.GetChild(i).GetComponent<PixelLayer>();
            if(other != null) return other;
        }
        return null;
    }

    public static bool MergeDown(PixelLayer upper)
    {
        PixelFloating.CommitIfActive();
        var lower = LayerBelow(upper);
        if(lower == null || lower.locked) return false;
        var group = Undo.GetCurrentGroup();
        var items = new List<PixelCompositor.Item>();
        items.Add(new PixelCompositor.Item { layer = lower, opacity = 1f });
        if(upper.visible) items.Add(new PixelCompositor.Item { layer = upper, opacity = upper.opacity });
        var rect = PixelCanvasEditing.Bounds(upper);
        if(rect.width > 0 && rect.height > 0 && upper.visible)
        {
            var merged = PixelCompositor.Composite(items, rect);
            PixelHistory.BeginStroke(lower);
            for(var y = 0; y < rect.height; y++)
                for(var x = 0; x < rect.width; x++)
                    PixelCanvasEditing.SetPixel(lower, rect.xMin + x, rect.yMin + y, merged[y * rect.width + x]);
            PixelCanvasEditing.Apply();
            PixelHistory.EndStroke();
        }
        var canvas = upper.Root;
        Selection.activeGameObject = lower.gameObject;
        Undo.DestroyObjectImmediate(upper.gameObject);
        Undo.CollapseUndoOperations(group);
        if(canvas != null) PixelCanvasSync.Sync(canvas);
        return true;
    }

    public static void DeleteLayer(PixelLayer layer)
    {
        PixelFloating.CommitIfActive();
        var canvas = layer.Root;
        Undo.DestroyObjectImmediate(layer.gameObject);
        if(canvas != null) PixelCanvasSync.Sync(canvas);
    }

    public static void CleanUnusedPngs(PixelCanvas canvas)
    {
        PixelFloating.CommitIfActive();
        PixelCanvasEditing.SaveAll();
        var folder = PixelCanvasEditing.FolderOf(canvas);
        var used = new HashSet<string>();
        var layers = PixelCanvasSync.Layers();
        for(var i = 0; i < layers.Count; i++)
        {
            var p = PixelCanvasEditing.PathOf(layers[i]);
            if(!string.IsNullOrEmpty(p)) used.Add(p);
        }
        var canvases = PixelCanvasSync.Canvases();
        for(var i = 0; i < canvases.Count; i++)
        {
            if(canvases[i].baked != null && canvases[i].baked.sprite != null)
                used.Add(AssetDatabase.GetAssetPath(canvases[i].baked.sprite));
        }
        var assets = new List<string>();
        assets.AddRange(AssetDatabase.FindAssets("t:Scene"));
        assets.AddRange(AssetDatabase.FindAssets("t:Prefab"));
        for(var i = 0; i < assets.Count; i++)
        {
            var deps = AssetDatabase.GetDependencies(AssetDatabase.GUIDToAssetPath(assets[i]), true);
            for(var k = 0; k < deps.Length; k++) used.Add(deps[k]);
        }
        var unused = new List<string>();
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        for(var i = 0; i < guids.Length; i++)
        {
            var p = AssetDatabase.GUIDToAssetPath(guids[i]);
            if(p.EndsWith(".png") && !used.Contains(p)) unused.Add(p);
        }
        if(unused.Count == 0)
        {
            EditorUtility.DisplayDialog("Pixel Canvas", "No unused PNG files in " + folder, "OK");
            return;
        }
        if(!EditorUtility.DisplayDialog("Pixel Canvas", "Delete " + unused.Count + " unused PNG file(s) from " + folder + "?\n\n" + string.Join("\n", unused.GetRange(0, Mathf.Min(10, unused.Count))), "Delete", "Cancel")) return;
        for(var i = 0; i < unused.Count; i++) AssetDatabase.DeleteAsset(unused[i]);
        AssetDatabase.SaveAssets();
    }
}

[InitializeOnLoad]
public static class PixelDuplicateWatcher
{
    static readonly HashSet<int> known = new HashSet<int>();
    static bool pending;

    static PixelDuplicateWatcher()
    {
        EditorApplication.hierarchyChanged += Request;
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += Rebuild;
        EditorApplication.delayCall += Rebuild;
    }

    static bool Skip(Object o)
    {
        return o == null || EditorUtility.IsPersistent(o) || PrefabUtility.IsPartOfPrefabInstance(o) || PrefabStageUtility.GetCurrentPrefabStage() != null;
    }

    static void Rebuild()
    {
        known.Clear();
        var canvases = PixelCanvasSync.Canvases();
        for(var i = 0; i < canvases.Count; i++) known.Add(canvases[i].GetInstanceID());
        var layers = PixelCanvasSync.Layers();
        for(var i = 0; i < layers.Count; i++) known.Add(layers[i].GetInstanceID());
    }

    static void Request()
    {
        if(pending || EditorApplication.isPlayingOrWillChangePlaymode) return;
        pending = true;
        EditorApplication.delayCall += Check;
    }

    static void Check()
    {
        pending = false;
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        var canvases = PixelCanvasSync.Canvases();
        var layers = PixelCanvasSync.Layers();

        var newCanvases = new List<PixelCanvas>();
        var canvasSource = new Dictionary<PixelCanvas, PixelCanvas>();
        var layerSource = new Dictionary<PixelLayer, PixelLayer>();
        var byId = new Dictionary<string, PixelCanvas>();
        for(var i = 0; i < canvases.Count; i++)
        {
            var c = canvases[i];
            if(Skip(c)) continue;
            if(string.IsNullOrEmpty(c.id))
            {
                c.id = PixelCanvasEditing.NewId();
                EditorUtility.SetDirty(c);
                continue;
            }
            PixelCanvas other;
            if(!byId.TryGetValue(c.id, out other))
            {
                byId[c.id] = c;
                continue;
            }
            var dup = known.Contains(c.GetInstanceID()) && !known.Contains(other.GetInstanceID()) ? other : c;
            var orig = dup == c ? other : c;
            byId[c.id] = orig;
            newCanvases.Add(dup);
            canvasSource[dup] = orig;
        }

        var handledRoots = new HashSet<PixelCanvas>(newCanvases);
        var newLayers = new List<PixelLayer>();
        var layerById = new Dictionary<string, PixelLayer>();
        for(var i = 0; i < layers.Count; i++)
        {
            var l = layers[i];
            if(Skip(l)) continue;
            if(l.Root != null && handledRoots.Contains(l.Root)) continue;
            if(string.IsNullOrEmpty(l.id))
            {
                l.id = PixelCanvasEditing.NewId();
                EditorUtility.SetDirty(l);
                continue;
            }
            PixelLayer other;
            if(!layerById.TryGetValue(l.id, out other))
            {
                layerById[l.id] = l;
                continue;
            }
            var dup = known.Contains(l.GetInstanceID()) && !known.Contains(other.GetInstanceID()) ? other : l;
            var orig = dup == l ? other : l;
            layerById[l.id] = orig;
            newLayers.Add(dup);
            layerSource[dup] = orig;
        }

        Rebuild();
        if(newCanvases.Count == 0 && newLayers.Count == 0) return;

        PixelFloating.CommitIfActive();
        PixelCanvasEditing.SaveAll();
        var mode = PixelDrawerAssets.Settings.duplicateMode;
        if(newCanvases.Count > 0)
        {
            var full = Decide(mode, "Pixel Canvas duplicated", "The canvas '" + newCanvases[0].name + "' was duplicated.\n\nFull copy: its layers get their own PNG files (edits stay independent).\nLinked: the copy keeps using the same PNG files (painting on one changes both).");
            for(var i = 0; i < newCanvases.Count; i++) PixelLayerOps.FinishCanvasDuplicate(newCanvases[i], full, canvasSource[newCanvases[i]]);
        }
        if(newLayers.Count > 0)
        {
            var full = Decide(mode, "Pixel Layer duplicated", "The layer '" + newLayers[0].name + "' was duplicated.\n\nFull copy: the copy gets its own PNG file (edits stay independent).\nLinked: the copy keeps using the same PNG (painting on one changes both).");
            for(var i = 0; i < newLayers.Count; i++) PixelLayerOps.FinishLayerDuplicate(newLayers[i], full, layerSource[newLayers[i]]);
        }
        Rebuild();
    }

    static bool Decide(PixelDuplicateMode mode, string title, string message)
    {
        if(mode == PixelDuplicateMode.FullCopy) return true;
        if(mode == PixelDuplicateMode.Linked) return false;
        return EditorUtility.DisplayDialog(title, message, "Full copy (new PNG)", "Linked (same PNG)");
    }
}
#endif
