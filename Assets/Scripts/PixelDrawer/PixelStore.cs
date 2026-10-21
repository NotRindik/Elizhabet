#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public struct PixelData
{
    public Color32[] pixels;
    public byte[] emission;
    public int width;
    public int height;
    public Vector2Int origin;
}

public struct PixelSaved
{
    public Sprite sprite;
    public Texture2D emission;
    public Material material;
    public string key;
}

public interface IPixelStore
{
    bool SupportsEmission { get; }
    string KeyOf(PixelLayer layer);
    bool Load(PixelLayer layer, out PixelData data);
    bool Save(PixelLayer first, List<PixelLayer> users, string key, PixelData data, PixelCanvas canvas, out PixelSaved saved);
    bool Refresh(PixelLayer layer);
    bool MakeUnique(PixelLayer layer, string folder, string newId);
}

public static class PixelStores
{
    static readonly PngPixelStore png = new PngPixelStore();
    static readonly AssetPixelStore asset = new AssetPixelStore();

    public static IPixelStore Existing(PixelLayer layer)
    {
        if(layer.art != null) return asset;
        var sr = layer.Surface(false);
        if(sr != null && sr.sprite != null) return png;
        return null;
    }

    public static IPixelStore For(PixelLayer layer)
    {
        var existing = Existing(layer);
        if(existing != null) return existing;
        return PixelDrawerAssets.Settings.storage == PixelStorageMode.Png ? (IPixelStore)png : asset;
    }
}

public class PngPixelStore : IPixelStore
{
    public bool SupportsEmission
    {
        get { return false; }
    }

    public string KeyOf(PixelLayer layer)
    {
        var sr = layer.Surface(false);
        return sr != null && sr.sprite != null ? AssetDatabase.GetAssetPath(sr.sprite) : null;
    }

    public bool Load(PixelLayer layer, out PixelData data)
    {
        data = default(PixelData);
        var path = KeyOf(layer);
        if(string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
        var temp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        temp.LoadImage(File.ReadAllBytes(path));
        data.width = temp.width;
        data.height = temp.height;
        data.pixels = temp.GetPixels32();
        Object.DestroyImmediate(temp);
        data.origin = PixelCanvasEditing.ReadOrigin(path, layer.origin);
        return true;
    }

    public bool Save(PixelLayer first, List<PixelLayer> users, string key, PixelData data, PixelCanvas canvas, out PixelSaved saved)
    {
        saved = default(PixelSaved);
        if(data.width == 0 || data.height == 0)
        {
            if(!string.IsNullOrEmpty(key) && File.Exists(key)) AssetDatabase.DeleteAsset(key);
            return true;
        }

        var path = key;
        if(string.IsNullOrEmpty(path))
        {
            if(string.IsNullOrEmpty(first.id)) first.id = PixelCanvasEditing.NewId();
            path = AssetDatabase.GenerateUniqueAssetPath(PixelCanvasEditing.FolderOf(canvas) + "/" + first.id + ".png");
        }
        var tex = new Texture2D(data.width, data.height, TextureFormat.RGBA32, false);
        tex.SetPixels32(data.pixels);
        tex.Apply(false);
        var bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        PixelCanvasEditing.Configure(path, canvas.pixelsPerUnit, data.origin);
        var sprite = PixelCanvasEditing.LoadSprite(path);
        if(sprite == null)
        {
            Debug.LogError("PixelCanvas: sprite not found after import: " + path + " | file exists: " + File.Exists(path) + " | importer: " + (AssetImporter.GetAtPath(path) != null));
            return false;
        }
        saved.sprite = sprite;
        saved.key = path;
        return true;
    }

    public bool Refresh(PixelLayer layer)
    {
        var path = KeyOf(layer);
        if(string.IsNullOrEmpty(path)) return false;
        var o = PixelCanvasEditing.ReadOrigin(path, layer.origin);
        if(o == layer.origin) return false;
        layer.origin = o;
        layer.PlaceSurface();
        return true;
    }

    public bool MakeUnique(PixelLayer layer, string folder, string newId)
    {
        var canvas = layer.Root;
        var path = KeyOf(layer);
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
}
#endif
