#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class AssetPixelStore : IPixelStore
{
    public bool SupportsEmission
    {
        get { return true; }
    }

    public string KeyOf(PixelLayer layer)
    {
        if(layer.art == null) return null;
        var path = AssetDatabase.GetAssetPath(layer.art);
        return string.IsNullOrEmpty(path) ? null : path + "#" + layer.frame;
    }

    public bool Load(PixelLayer layer, out PixelData data)
    {
        data = default(PixelData);
        var f = layer.CurrentFrame;
        if(f == null || f.color == null) return false;
        data.width = f.color.width;
        data.height = f.color.height;
        data.pixels = f.color.GetPixels32();
        data.emission = f.emission != null ? ReadEmission(f.emission, data.width * data.height) : null;
        data.origin = f.origin;
        return true;
    }

    public bool Save(PixelLayer first, List<PixelLayer> users, string key, PixelData data, PixelCanvas canvas, out PixelSaved saved)
    {
        saved = default(PixelSaved);
        var empty = data.width == 0 || data.height == 0;
        var art = first.art;
        if(art == null)
        {
            if(empty) return true;
            art = Create(first, users, canvas);
            if(art == null) return false;
            var at = Mathf.Clamp(canvas.frame, 0, canvas.FrameCount - 1);
            while(art.frames.Count < canvas.FrameCount) art.frames.Add(new PixelArtAsset.Frame());
            for(var i = 0; i < users.Count; i++)
                if(users[i] != null) users[i].frame = at;
        }
        Write(art, first.frame, data, canvas.pixelsPerUnit);
        var f = art.frames[first.frame];
        saved.sprite = f.sprite;
        saved.emission = f.emission;
        saved.material = art.material;
        saved.key = KeyOf(first);
        return true;
    }

    public bool Refresh(PixelLayer layer)
    {
        var f = layer.CurrentFrame;
        if(f == null) return false;
        var sr = layer.Surface(true);
        var origin = f.sprite != null ? f.origin : Vector2Int.zero;
        var changed = sr.sprite != f.sprite || layer.origin != origin;
        layer.Present();
        return changed;
    }

    public bool MakeUnique(PixelLayer layer, string folder, string newId)
    {
        var source = AssetDatabase.GetAssetPath(layer.art);
        PixelDrawerAssets.EnsureFolder(folder);
        var newPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + newId + ".asset");
        if(!AssetDatabase.CopyAsset(source, newPath))
        {
            Debug.LogError("PixelCanvas: could not copy " + source + " to " + newPath);
            return false;
        }
        var copy = AssetDatabase.LoadAssetAtPath<PixelArtAsset>(newPath);
        if(copy == null) return false;
        layer.id = newId;
        layer.art = copy;
        layer.Present();
        EditorUtility.SetDirty(layer);
        EditorSceneManager.MarkSceneDirty(layer.gameObject.scene);
        return true;
    }

    public static PixelArtAsset Create(PixelLayer first, IList<PixelLayer> users, PixelCanvas canvas)
    {
        if(string.IsNullOrEmpty(first.id)) first.id = PixelCanvasEditing.NewId();
        var path = AssetDatabase.GenerateUniqueAssetPath(PixelCanvasEditing.FolderOf(canvas) + "/" + first.id + ".asset");
        var art = ScriptableObject.CreateInstance<PixelArtAsset>();
        art.pixelsPerUnit = canvas.pixelsPerUnit;
        art.frames.Add(new PixelArtAsset.Frame());
        AssetDatabase.CreateAsset(art, path);
        for(var i = 0; i < users.Count; i++)
        {
            if(users[i] == null) continue;
            users[i].art = art;
            users[i].frame = 0;
            EditorUtility.SetDirty(users[i]);
        }
        return art;
    }

    public static void Write(PixelArtAsset art, int index, PixelData data, float ppu)
    {
        while(art.frames.Count <= index) art.frames.Add(new PixelArtAsset.Frame());
        art.pixelsPerUnit = ppu;
        var f = art.frames[index];
        if(data.width == 0 || data.height == 0) ReleaseFrame(art, f);
        else WriteFrame(art, f, data, index);
        if(f.emission != null && art.material == null) art.material = PixelDrawerAssets.EmissiveMaterial();
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssetIfDirty(art);
    }

    public static PixelArtAsset.Frame Link(PixelArtAsset.Frame source)
    {
        var f = new PixelArtAsset.Frame();
        f.color = source.color;
        f.emission = source.emission;
        f.sprite = source.sprite;
        f.origin = source.origin;
        f.duration = source.duration;
        return f;
    }

    public static void Materialize(PixelArtAsset art, int count)
    {
        if(art.frames.Count == 0) art.frames.Add(new PixelArtAsset.Frame());
        if(art.frames.Count >= count) return;
        while(art.frames.Count < count) art.frames.Add(Link(art.frames[art.frames.Count - 1]));
        EditorUtility.SetDirty(art);
    }

    public static void ReleaseFrame(PixelArtAsset art, PixelArtAsset.Frame f)
    {
        if(f.sprite != null && !Shared(art, f, f.sprite)) Object.DestroyImmediate(f.sprite, true);
        if(f.color != null && !Shared(art, f, f.color)) Object.DestroyImmediate(f.color, true);
        if(f.emission != null && !Shared(art, f, f.emission)) Object.DestroyImmediate(f.emission, true);
        f.sprite = null;
        f.color = null;
        f.emission = null;
        f.origin = Vector2Int.zero;
    }

    static bool Shared(PixelArtAsset art, PixelArtAsset.Frame f, Object o)
    {
        for(var i = 0; i < art.frames.Count; i++)
        {
            var g = art.frames[i];
            if(g == f) continue;
            if(g.color == o || g.sprite == o || g.emission == o) return true;
        }
        return false;
    }

    static void WriteFrame(PixelArtAsset art, PixelArtAsset.Frame f, PixelData data, int index)
    {
        var w = data.width;
        var h = data.height;
        var name = art.name + " " + index;
        if(f.color != null && Shared(art, f, f.color))
        {
            f.color = null;
            f.sprite = null;
        }
        if(f.sprite != null && Shared(art, f, f.sprite)) f.sprite = null;
        if(f.emission != null && Shared(art, f, f.emission)) f.emission = null;
        var resized = f.color == null || f.color.width != w || f.color.height != h;
        if(f.color == null) f.color = NewTexture(art, w, h, TextureFormat.RGBA32, false, name);
        else if(resized) f.color.Reinitialize(w, h);
        f.color.SetPixels32(data.pixels);
        f.color.Apply(false);

        if(resized || f.sprite == null || !Mathf.Approximately(f.sprite.pixelsPerUnit, art.pixelsPerUnit))
        {
            if(f.sprite != null) Object.DestroyImmediate(f.sprite, true);
            f.sprite = Sprite.Create(f.color, new Rect(0, 0, w, h), Vector2.zero, art.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            f.sprite.name = name;
            AssetDatabase.AddObjectToAsset(f.sprite, art);
        }

        if(data.emission != null && AnyGlow(data.emission))
        {
            if(f.emission == null) f.emission = NewTexture(art, w, h, TextureFormat.R8, true, name + " emission");
            else if(f.emission.width != w || f.emission.height != h) f.emission.Reinitialize(w, h);
            f.emission.SetPixelData(data.emission, 0);
            f.emission.Apply(false);
        }
        else if(f.emission != null)
        {
            Object.DestroyImmediate(f.emission, true);
            f.emission = null;
        }
        f.origin = data.origin;
    }

    static Texture2D NewTexture(PixelArtAsset art, int w, int h, TextureFormat format, bool linear, string name)
    {
        var tex = new Texture2D(w, h, format, false, linear);
        tex.name = name;
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        AssetDatabase.AddObjectToAsset(tex, art);
        return tex;
    }

    static bool AnyGlow(byte[] emission)
    {
        for(var i = 0; i < emission.Length; i++)
            if(emission[i] != 0) return true;
        return false;
    }

    public static byte[] ReadEmission(Texture2D tex, int length)
    {
        var raw = tex.GetRawTextureData();
        if(raw == null || raw.Length < length) return null;
        if(raw.Length == length) return raw;
        var bytes = new byte[length];
        System.Array.Copy(raw, bytes, length);
        return bytes;
    }
}
#endif
