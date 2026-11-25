using System.Collections.Generic;
using System.IO;
using System.Linq;
using Controllers;
using Systems;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ColorPointsBake))]
public class ColorPointsBakeEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        var bake = (ColorPointsBake)target;

        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(ColorPointsBake.sources)), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(ColorPointsBake.trackedColors)), true);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.HelpBox("Sources: sprites, textures, folders, animation clips or animator controllers. Tracked colors: from the PlayerController's ColorPositioningComponent.", MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Collect colors from scene player"))
                CollectColors(bake);
            if (GUILayout.Button("Bake"))
                ColorPointsBaker.Bake(bake);
        }

        EditorGUILayout.LabelField($"Baked sprites: {bake.entries.Count}");
    }

    static void CollectColors(ColorPointsBake bake)
    {
        PlayerController player = null;
        if (Selection.activeGameObject != null)
            player = Selection.activeGameObject.GetComponentInChildren<PlayerController>(true);
        if (player == null)
            player = Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player == null || player.colorPositioningComponent?.pointsGroup == null)
        {
            Debug.LogWarning("ColorPointsBake: PlayerController not found (select it or open a scene with the player).");
            return;
        }

        var colors = new List<Color32>();
        var seen = new HashSet<int>();
        foreach (var group in player.colorPositioningComponent.pointsGroup.Values)
        {
            if (group.points == null)
                continue;
            foreach (var point in group.points)
                if (seen.Add(ColorPointsBake.Pack(point.color)))
                    colors.Add(point.color);
        }

        Undo.RecordObject(bake, "Collect Color Points");
        bake.trackedColors = colors.ToArray();
        EditorUtility.SetDirty(bake);
        Debug.Log($"ColorPointsBake: collected {colors.Count} colors from {player.name}.", bake);
    }
}

public static class ColorPointsBaker
{
    public static void Bake(ColorPointsBake bake)
    {
        var sprites = CollectSprites(bake.sources);
        var targets = new Dictionary<int, int>();
        foreach (var c in bake.trackedColors)
            targets[ColorPointsBake.Pack(c)] = 0;

        var cache = new Dictionary<Texture2D, Color32[]>();
        var entries = new List<ColorPointsBake.Entry>();
        int skipped = 0;

        try
        {
            for (int i = 0; i < sprites.Count; i++)
            {
                var sprite = sprites[i];
                if (EditorUtility.DisplayCancelableProgressBar("Bake Color Points", sprite.name, (float)i / sprites.Count))
                    break;

                var pixels = ReadSource(sprite.texture, cache);
                if (pixels == null)
                {
                    skipped++;
                    continue;
                }

                var entry = BakeSprite(sprite, pixels, sprite.texture.width, targets);
                if (entry.colors.Length > 0)
                    entries.Add(entry);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Undo.RecordObject(bake, "Bake Color Points");
        bake.entries = entries;
        bake.Invalidate();
        EditorUtility.SetDirty(bake);
        AssetDatabase.SaveAssetIfDirty(bake);
        Debug.Log($"ColorPointsBake: {sprites.Count} sprites scanned, {entries.Count} with points, {skipped} skipped (unreadable source).", bake);
    }

    static ColorPointsBake.Entry BakeSprite(Sprite sprite, Color32[] pixels, int width, Dictionary<int, int> targets)
    {
        var rect = sprite.rect;
        int rx = (int)rect.x, ry = (int)rect.y, rw = (int)rect.width, rh = (int)rect.height;
        var colors = new List<int>();
        var points = new List<Vector2Int>();
        var found = new HashSet<int>();

        for (int y = ry; y < ry + rh && found.Count < targets.Count; y++)
        {
            int row = y * width;
            for (int x = rx; x < rx + rw; x++)
            {
                var p = pixels[row + x];
                if (p.a == 0)
                    continue;
                int key = ColorPointsBake.Pack(p);
                if (!targets.ContainsKey(key) || !found.Add(key))
                    continue;
                colors.Add(key);
                points.Add(new Vector2Int(x - rx, y - ry));
            }
        }

        return new ColorPointsBake.Entry { sprite = sprite, colors = colors.ToArray(), pixels = points.ToArray() };
    }

    static Color32[] ReadSource(Texture2D texture, Dictionary<Texture2D, Color32[]> cache)
    {
        if (texture == null)
            return null;
        if (cache.TryGetValue(texture, out var cached))
            return cached;

        Color32[] result = null;
        var path = AssetDatabase.GetAssetPath(texture);
        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
        {
            var copy = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (copy.LoadImage(File.ReadAllBytes(path)) && copy.width == texture.width && copy.height == texture.height)
                result = copy.GetPixels32();
            else
                Debug.LogWarning($"ColorPointsBake: '{path}' imported size differs from the file (Max Size?), skipped.", texture);
            Object.DestroyImmediate(copy);
        }
        else if (texture.isReadable)
        {
            result = texture.GetPixels32();
        }
        else
        {
            if (UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsCompressedFormat(texture.graphicsFormat))
                Debug.LogWarning($"ColorPointsBake: '{path}' is compressed, colors may not match exactly.", texture);
            result = ReadViaGpu(texture);
        }

        cache[texture] = result;
        return result;
    }

    public static Color32[] ReadViaGpu(Texture2D texture)
    {
        bool srgb = UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat);
        var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32,
            srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active;
        Graphics.Blit(texture, rt);
        RenderTexture.active = rt;
        var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, !srgb);
        copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        copy.Apply(false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        var pixels = copy.GetPixels32();
        Object.DestroyImmediate(copy);
        return pixels;
    }

    static List<Sprite> CollectSprites(Object[] sources)
    {
        var set = new HashSet<Sprite>();
        if (sources == null)
            return new List<Sprite>();

        foreach (var source in sources)
        {
            switch (source)
            {
                case null:
                    break;
                case Sprite sprite:
                    set.Add(sprite);
                    break;
                case Texture2D texture:
                    AddSpritesAt(AssetDatabase.GetAssetPath(texture), set);
                    break;
                case AnimationClip clip:
                    AddClip(clip, set);
                    break;
                case RuntimeAnimatorController controller:
                    foreach (var clip in controller.animationClips)
                        AddClip(clip, set);
                    break;
                case DefaultAsset folder when AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(folder)):
                    foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { AssetDatabase.GetAssetPath(folder) }))
                        AddSpritesAt(AssetDatabase.GUIDToAssetPath(guid), set);
                    break;
            }
        }

        return set.OrderBy(s => s.name).ToList();
    }

    static void AddSpritesAt(string path, HashSet<Sprite> set)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Sprite sprite)
                set.Add(sprite);
    }

    static void AddClip(AnimationClip clip, HashSet<Sprite> set)
    {
        if (clip == null)
            return;
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                if (key.value is Sprite sprite)
                    set.Add(sprite);
    }
}
