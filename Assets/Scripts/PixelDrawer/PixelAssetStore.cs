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
        var art = layer.art;
        if(f == null || !art.HasPixels(f)) return false;
        var size = art.SizeOf(f);
        data.width = size.x;
        data.height = size.y;
        data.pixels = ReadPixels(art, f);
        data.emission = ReadGlow(art, f);
        data.origin = f.origin;
        return data.pixels != null;
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
        saved.sprite = art.SpriteOf(f);
        saved.emission = art.EmissionOf(f);
        saved.material = art.material;
        saved.key = KeyOf(first);
        return true;
    }

    public bool Refresh(PixelLayer layer)
    {
        var f = layer.CurrentFrame;
        if(f == null) return false;
        var sr = layer.Surface(true);
        var sprite = layer.art.SpriteOf(f);
        var origin = sprite != null ? f.origin : Vector2Int.zero;
        var changed = sr.sprite != sprite || layer.origin != origin;
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
        else WriteFrame(art, f, data);
        if(art.HasGlow(f) && art.material == null) art.material = PixelDrawerAssets.EmissiveMaterial();
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssetIfDirty(art);
    }

    public static PixelArtAsset.Frame Link(PixelArtAsset.Frame source)
    {
        var f = new PixelArtAsset.Frame();
        f.dataIndex = source.dataIndex;
        f.legacyColor = source.legacyColor;
        f.legacyEmission = source.legacyEmission;
        f.legacySprite = source.legacySprite;
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
        ReleaseLegacy(art, f);
        f.dataIndex = -1;
        f.origin = Vector2Int.zero;
        Compact(art);
    }

    public static Color32[] ReadPixels(PixelArtAsset art, PixelArtAsset.Frame f)
    {
        var tex = art.ColorOf(f);
        return tex != null ? tex.GetPixels32() : null;
    }

    public static byte[] ReadGlow(PixelArtAsset art, PixelArtAsset.Frame f)
    {
        var d = art.DataOf(f);
        if(d != null) return d.glow != null ? PixelArtAsset.Inflate(d.glow, d.width * d.height) : null;
        if(f.legacyEmission == null) return null;
        return ReadEmission(f.legacyEmission, f.legacyEmission.width * f.legacyEmission.height);
    }

    public static bool SameData(PixelArtAsset.Frame a, PixelArtAsset.Frame b)
    {
        if(a.dataIndex >= 0 || b.dataIndex >= 0) return a.dataIndex == b.dataIndex;
        return a.legacyColor != null && a.legacyColor == b.legacyColor;
    }

    static void WriteFrame(PixelArtAsset art, PixelArtAsset.Frame f, PixelData data)
    {
        ReleaseLegacy(art, f);
        var d = new PixelArtAsset.FrameData
        {
            width = data.width,
            height = data.height,
            png = EncodePng(data.pixels, data.width, data.height),
            glow = data.emission != null && AnyGlow(data.emission) ? PixelArtAsset.Deflate(data.emission) : null
        };

        var old = art.DataOf(f);
        if(old != null && !SharedData(art, f))
        {
            art.ReleaseCache(old);
            art.pixelData[f.dataIndex] = d;
        }
        else
        {
            art.pixelData.Add(d);
            f.dataIndex = art.pixelData.Count - 1;
        }
        f.origin = data.origin;
        Compact(art);
    }

    static bool SharedData(PixelArtAsset art, PixelArtAsset.Frame f)
    {
        for(var i = 0; i < art.frames.Count; i++)
        {
            var g = art.frames[i];
            if(g != f && g.dataIndex == f.dataIndex) return true;
        }
        return false;
    }

    static void ReleaseLegacy(PixelArtAsset art, PixelArtAsset.Frame f)
    {
        if(!f.IsLegacy) return;
        if(f.legacySprite != null && !SharedLegacy(art, f, f.legacySprite)) Object.DestroyImmediate(f.legacySprite, true);
        if(f.legacyColor != null && !SharedLegacy(art, f, f.legacyColor)) Object.DestroyImmediate(f.legacyColor, true);
        if(f.legacyEmission != null && !SharedLegacy(art, f, f.legacyEmission)) Object.DestroyImmediate(f.legacyEmission, true);
        f.legacySprite = null;
        f.legacyColor = null;
        f.legacyEmission = null;
    }

    static bool SharedLegacy(PixelArtAsset art, PixelArtAsset.Frame f, Object o)
    {
        for(var i = 0; i < art.frames.Count; i++)
        {
            var g = art.frames[i];
            if(g == f) continue;
            if(g.legacyColor == o || g.legacySprite == o || g.legacyEmission == o) return true;
        }
        return false;
    }

    public static void Compact(PixelArtAsset art)
    {
        var used = new bool[art.pixelData.Count];
        for(var i = 0; i < art.frames.Count; i++)
        {
            var index = art.frames[i].dataIndex;
            if(index >= 0 && index < used.Length) used[index] = true;
            else art.frames[i].dataIndex = -1;
        }

        var remap = new int[used.Length];
        var kept = new List<PixelArtAsset.FrameData>(used.Length);
        for(var i = 0; i < used.Length; i++)
        {
            if(used[i])
            {
                remap[i] = kept.Count;
                kept.Add(art.pixelData[i]);
            }
            else
            {
                remap[i] = -1;
                art.ReleaseCache(art.pixelData[i]);
            }
        }
        if(kept.Count == art.pixelData.Count) return;

        for(var i = 0; i < art.frames.Count; i++)
        {
            var f = art.frames[i];
            if(f.dataIndex >= 0) f.dataIndex = remap[f.dataIndex];
        }
        art.pixelData = kept;
    }

    public static byte[] EncodePng(Color32[] pixels, int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply(false);
        var bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        return bytes;
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
