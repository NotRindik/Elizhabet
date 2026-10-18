#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Finds (or creates on first use) the settings asset and the brush assets.
public static class PixelDrawerAssets
{
    static PixelDrawerSettings settings;
    static List<PixelBrush> brushes;

    static PixelDrawerAssets()
    {
        EditorApplication.projectChanged += () => brushes = null;
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

    public static void Ensure()
    {
        var s = Settings;
        var b = Brushes;
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

    public static PixelBrush CreateBrush(string name, PixelBrush copyFrom)
    {
        var folder = Folder() + "/Brushes";
        EnsureFolder(folder);
        var brush = ScriptableObject.CreateInstance<PixelBrush>();
        if(copyFrom != null) EditorUtility.CopySerialized(copyFrom, brush);
        var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".asset");
        AssetDatabase.CreateAsset(brush, path);
        AssetDatabase.SaveAssets();
        brushes = null;
        return brush;
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

    // Folder where this script lives; falls back to Assets.
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

    static void EnsureFolder(string folder)
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
