#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PixelArtCompression
{
    [MenuItem("Tools/Pixel Canvas/Compress Pixel Art Assets (PNG)")]
    static void MenuCompressAll()
    {
        var arts = new List<PixelArtAsset>();
        foreach(var guid in AssetDatabase.FindAssets("t:PixelArtAsset"))
        {
            var art = AssetDatabase.LoadAssetAtPath<PixelArtAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if(art != null && NeedsCompression(art)) arts.Add(art);
        }

        if(arts.Count == 0)
        {
            EditorUtility.DisplayDialog("Pixel Canvas", "All Pixel Art assets are already compressed.", "OK");
            return;
        }

        if(!EditorUtility.DisplayDialog("Pixel Canvas",
               arts.Count + " Pixel Art asset(s) still store raw textures.\n\n" +
               "They will be converted to PNG data inside the same .asset files. Animation, glow and links between frames are kept.\n\n" +
               "Commit to git first.", "Compress", "Cancel"))
            return;

        Compress(arts);
    }

    [MenuItem("Assets/Pixel Canvas/Compress Selected Pixel Art Assets (PNG)")]
    static void MenuCompressSelected()
    {
        var arts = new List<PixelArtAsset>();
        foreach(var o in Selection.objects)
            if(o is PixelArtAsset art && NeedsCompression(art)) arts.Add(art);

        if(arts.Count == 0)
        {
            EditorUtility.DisplayDialog("Pixel Canvas", "Select Pixel Art assets that still store raw textures.", "OK");
            return;
        }
        Compress(arts);
    }

    public static bool NeedsCompression(PixelArtAsset art)
    {
        for(var i = 0; i < art.frames.Count; i++)
            if(art.frames[i].IsLegacy) return true;
        return HasLegacySubAssets(art);
    }

    public static void Compress(List<PixelArtAsset> arts)
    {
        PixelFloating.CommitIfActive();
        PixelArtPreview.StopAll();
        if(!PixelCanvasEditing.SaveAll())
        {
            EditorUtility.DisplayDialog("Pixel Canvas", "Could not save pending edits. Fix the errors in the console first.", "OK");
            return;
        }
        PixelHistory.Clear();

        long before = 0;
        long after = 0;
        try
        {
            for(var i = 0; i < arts.Count; i++)
            {
                var path = AssetDatabase.GetAssetPath(arts[i]);
                EditorUtility.DisplayProgressBar("Pixel Canvas", "Compressing " + path, (float)i / arts.Count);
                before += FileSize(path);
                CompressAsset(arts[i]);
            }
            AssetDatabase.SaveAssets();
            for(var i = 0; i < arts.Count; i++) after += FileSize(AssetDatabase.GetAssetPath(arts[i]));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        RefreshLayers(arts);
        SceneView.RepaintAll();
        Debug.Log("PixelCanvas: compressed " + arts.Count + " Pixel Art asset(s): " + Mb(before) + " → " + Mb(after) + ".");
    }

    static void CompressAsset(PixelArtAsset art)
    {
        var byTexture = new Dictionary<Texture2D, int>();
        for(var i = 0; i < art.frames.Count; i++)
        {
            var f = art.frames[i];
            if(f.legacyColor == null)
            {
                f.legacyEmission = null;
                f.legacySprite = null;
                continue;
            }

            if(!byTexture.TryGetValue(f.legacyColor, out var index))
            {
                var tex = f.legacyColor;
                var glow = f.legacyEmission != null ? AssetPixelStore.ReadEmission(f.legacyEmission, tex.width * tex.height) : null;
                art.pixelData.Add(new PixelArtAsset.FrameData
                {
                    width = tex.width,
                    height = tex.height,
                    png = AssetPixelStore.EncodePng(tex.GetPixels32(), tex.width, tex.height),
                    glow = glow != null && AnyNonZero(glow) ? PixelArtAsset.Deflate(glow) : null
                });
                index = art.pixelData.Count - 1;
                byTexture[tex] = index;
            }

            f.dataIndex = index;
            f.legacyColor = null;
            f.legacyEmission = null;
            f.legacySprite = null;
        }

        RemoveLegacySubAssets(art);
        AssetPixelStore.Compact(art);
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssetIfDirty(art);
    }

    static bool HasLegacySubAssets(PixelArtAsset art)
    {
        foreach(var o in AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(art)))
            if(o is Texture2D || o is Sprite) return true;
        return false;
    }

    static void RemoveLegacySubAssets(PixelArtAsset art)
    {
        var subs = AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(art));
        for(var i = 0; i < subs.Length; i++)
            if(subs[i] is Sprite) Object.DestroyImmediate(subs[i], true);
        for(var i = 0; i < subs.Length; i++)
            if(subs[i] != null && subs[i] is Texture2D) Object.DestroyImmediate(subs[i], true);
    }

    static void RefreshLayers(List<PixelArtAsset> arts)
    {
        var set = new HashSet<PixelArtAsset>(arts);
        foreach(var layer in Object.FindObjectsByType<PixelLayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if(layer.art == null || !set.Contains(layer.art)) continue;
            layer.Present();
            EditorUtility.SetDirty(layer);
            var scene = layer.gameObject.scene;
            if(scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    static bool AnyNonZero(byte[] bytes)
    {
        for(var i = 0; i < bytes.Length; i++)
            if(bytes[i] != 0) return true;
        return false;
    }

    static long FileSize(string assetPath)
    {
        var full = Path.GetFullPath(assetPath);
        return File.Exists(full) ? new FileInfo(full).Length : 0;
    }

    static string Mb(long bytes)
    {
        return (bytes / (1024f * 1024f)).ToString("0.##") + " MB";
    }
}
#endif
