#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PixelBaker
{
    public static bool Bake(PixelCanvas canvas)
    {
        PixelFloating.CommitIfActive();
        var items = PixelCompositor.ForCanvas(canvas, true);
        var rect = PixelCompositor.UnionBounds(items);
        if(rect.width <= 0 || rect.height <= 0)
        {
            EditorUtility.DisplayDialog("Pixel Canvas", "Nothing to bake: no visible pixels.", "OK");
            return false;
        }
        var pixels = PixelCompositor.Composite(items, rect);
        var tex = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply(false);
        var bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        if(string.IsNullOrEmpty(canvas.id)) canvas.id = PixelCanvasEditing.NewId();
        var folder = PixelCanvasEditing.FolderOf(canvas);
        var path = folder + "/" + canvas.id + "_baked.png";
        File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        PixelCanvasEditing.Configure(path, canvas.pixelsPerUnit, rect.min);
        var sprite = PixelCanvasEditing.LoadSprite(path);
        if(sprite == null) return false;

        Undo.RecordObject(canvas, "Bake Pixel Canvas");
        var sr = canvas.baked;
        if(sr == null)
        {
            var go = new GameObject(canvas.name + " (Baked)", typeof(SpriteRenderer));
            go.transform.SetParent(canvas.transform.parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Bake Pixel Canvas");
            sr = go.GetComponent<SpriteRenderer>();
            canvas.baked = sr;
        }
        var t = sr.transform;
        t.position = canvas.transform.TransformPoint(new Vector3(rect.xMin, rect.yMin, 0) / canvas.pixelsPerUnit);
        t.rotation = canvas.transform.rotation;
        t.localScale = canvas.transform.localScale;
        sr.sprite = sprite;
        var group = canvas.GetComponent<SortingGroup>();
        if(group != null)
        {
            sr.sortingLayerID = group.sortingLayerID;
            sr.sortingOrder = group.sortingOrder;
        }
        sr.gameObject.SetActive(false);
        canvas.useBakedInPlay = true;
        EditorUtility.SetDirty(canvas);
        EditorUtility.SetDirty(sr);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("PixelCanvas: baked " + rect.width + "x" + rect.height + " sprite to " + path + ". In play mode and builds the layers are replaced by it.");
        return true;
    }
}
#endif
