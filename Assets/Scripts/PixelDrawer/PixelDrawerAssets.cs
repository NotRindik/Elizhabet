#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PixelDrawerAssets
{
    static PixelDrawerSettings settings;
    static List<PixelBrush> brushes;
    static List<PixelPalette> palettes;

    static PixelDrawerAssets()
    {
        EditorApplication.projectChanged += () =>
        {
            brushes = null;
            palettes = null;
            PixelPatternBrush.ClearCache();
        };
    }

    public static PixelDrawerSettings Settings
    {
        get
        {
            if(settings == null) settings = FindOrCreateSettings();
            return settings;
        }
    }

    public static List<PixelBrush> Brushes
    {
        get
        {
            if(brushes == null) RefreshBrushes();
            return brushes;
        }
    }

    public static List<PixelPalette> Palettes
    {
        get
        {
            if(palettes == null) RefreshPalettes();
            return palettes;
        }
    }

    public static void Ensure()
    {
        var s = Settings;
        var b = Brushes;
        var p = Palettes;
    }

    public static string RootFolder()
    {
        return Folder();
    }

    public static void RefreshBrushes()
    {
        var list = new List<PixelBrush>();
        var guids = AssetDatabase.FindAssets("t:PixelBrush");
        for(var i = 0; i < guids.Length; i++)
        {
            var brush = AssetDatabase.LoadAssetAtPath<PixelBrush>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if(brush != null) list.Add(brush);
        }
        if(list.Count == 0) list.Add(CreateBrush("Pixel Brush", null));
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        brushes = list;
    }

    public static void RefreshPalettes()
    {
        var list = new List<PixelPalette>();
        var guids = AssetDatabase.FindAssets("t:PixelPalette");
        for(var i = 0; i < guids.Length; i++)
        {
            var palette = AssetDatabase.LoadAssetAtPath<PixelPalette>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if(palette != null) list.Add(palette);
        }
        if(list.Count == 0) list.Add(CreateDefaultPalette());
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        palettes = list;
    }

    public static PixelBrush CreateBrush(string name, PixelBrush copyFrom)
    {
        var folder = Folder() + "/Brushes";
        EnsureFolder(folder);
        var brush = (PixelBrush)ScriptableObject.CreateInstance(copyFrom != null ? copyFrom.GetType() : typeof(PixelBrush));
        if(copyFrom != null) EditorUtility.CopySerialized(copyFrom, brush);
        var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".asset");
        AssetDatabase.CreateAsset(brush, path);
        AssetDatabase.SaveAssets();
        brushes = null;
        return brush;
    }

    public static PixelPatternBrush CreatePatternBrush(string name, PixelPatternBrush.PatternKind kind, Texture2D image)
    {
        var folder = Folder() + "/Brushes";
        EnsureFolder(folder);
        var brush = ScriptableObject.CreateInstance<PixelPatternBrush>();
        brush.kind = kind;
        if(image != null) brush.images.Add(image);
        if(kind == PixelPatternBrush.PatternKind.Stamp) brush.spacing = Mathf.Max(1, image != null ? image.width : 8);
        var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".asset");
        AssetDatabase.CreateAsset(brush, path);
        AssetDatabase.SaveAssets();
        brushes = null;
        return brush;
    }

    public static Texture2D CreatePatternTexture(string name, Color32[] pixels, int w, int h)
    {
        var folder = Folder() + "/Patterns";
        EnsureFolder(folder);
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply(false);
        var bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".png");
        File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    public static PixelPalette CreatePalette(string name, List<Color32> colors)
    {
        var folder = Folder() + "/Palettes";
        EnsureFolder(folder);
        var palette = ScriptableObject.CreateInstance<PixelPalette>();
        palette.colors = colors != null ? colors : new List<Color32>();
        var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".asset");
        AssetDatabase.CreateAsset(palette, path);
        AssetDatabase.SaveAssets();
        palettes = null;
        return palette;
    }

    static PixelPalette CreateDefaultPalette()
    {
        var hex = new[]
        {
            "000000", "1D2B53", "7E2553", "008751", "AB5236", "5F574F", "C2C3C7", "FFF1E8",
            "FF004D", "FFA300", "FFEC27", "00E436", "29ADFF", "83769C", "FF77A8", "FFCCAA"
        };
        var colors = new List<Color32>();
        for(var i = 0; i < hex.Length; i++)
        {
            Color c;
            if(ColorUtility.TryParseHtmlString("#" + hex[i], out c)) colors.Add(c);
        }
        return CreatePalette("PICO-8", colors);
    }

    static PixelDrawerSettings FindOrCreateSettings()
    {
        var guids = AssetDatabase.FindAssets("t:PixelDrawerSettings");
        for(var i = 0; i < guids.Length; i++)
        {
            var found = AssetDatabase.LoadAssetAtPath<PixelDrawerSettings>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if(found != null) return found;
        }
        var created = ScriptableObject.CreateInstance<PixelDrawerSettings>();
        AssetDatabase.CreateAsset(created, AssetDatabase.GenerateUniqueAssetPath(Folder() + "/PixelDrawerSettings.asset"));
        AssetDatabase.SaveAssets();
        return created;
    }

    static Material emissive;

    public static Material EmissiveMaterial()
    {
        if(emissive != null) return emissive;
        var guids = AssetDatabase.FindAssets("PixelEmissive t:Material");
        for(var i = 0; i < guids.Length; i++)
        {
            var found = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if(found == null || found.shader == null || found.shader.name != "PixelDrawer/SpriteEmissive") continue;
            emissive = found;
            return emissive;
        }
        var shader = Shader.Find("PixelDrawer/SpriteEmissive");
        if(shader == null)
        {
            Debug.LogError("PixelCanvas: shader 'PixelDrawer/SpriteEmissive' not found. Keep PixelSpriteEmissive.shader in the project.");
            return null;
        }
        var material = new Material(shader);
        material.name = "PixelEmissive";
        AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(Folder() + "/PixelEmissive.mat"));
        AssetDatabase.SaveAssets();
        emissive = material;
        return emissive;
    }

    static string Folder()
    {
        var guids = AssetDatabase.FindAssets("PixelDrawerAssets t:MonoScript");
        for(var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if(Path.GetFileNameWithoutExtension(path) == "PixelDrawerAssets")
                return Path.GetDirectoryName(path).Replace('\\', '/');
        }
        return "Assets";
    }

    public static void EnsureFolder(string folder)
    {
        var parts = folder.Split('/');
        var current = parts[0];
        for(var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if(!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
