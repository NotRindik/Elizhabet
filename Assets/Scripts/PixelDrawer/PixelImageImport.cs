#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

public static class PixelImageImport
{
    public class Image
    {
        public string name;
        public Color32[] pixels;
        public int width;
        public int height;
    }

    static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };

    public static bool HasImages()
    {
        var objects = DragAndDrop.objectReferences;
        for(var i = 0; i < objects.Length; i++)
            if(objects[i] is Sprite || objects[i] is Texture2D) return true;
        var paths = DragAndDrop.paths;
        for(var i = 0; i < paths.Length; i++)
            if(IsImagePath(paths[i])) return true;
        return false;
    }

    public static List<Image> FromDrag()
    {
        var result = new List<Image>();
        var used = new HashSet<string>();
        var objects = DragAndDrop.objectReferences;
        for(var i = 0; i < objects.Length; i++)
        {
            Image image;
            if(!TryObject(objects[i], out image)) continue;
            result.Add(image);
            var path = AssetDatabase.GetAssetPath(objects[i]);
            if(!string.IsNullOrEmpty(path)) used.Add(Full(path));
        }
        var paths = DragAndDrop.paths;
        for(var i = 0; i < paths.Length; i++)
        {
            if(!IsImagePath(paths[i])) continue;
            var full = Full(paths[i]);
            if(used.Contains(full)) continue;
            used.Add(full);
            Image image;
            if(TryFile(full, out image)) result.Add(image);
        }
        return result;
    }

    public static void PasteAsLayers(PixelCanvas canvas, PixelLayer above, List<Image> images, Vector2? center)
    {
        if(canvas == null || images == null || images.Count == 0) return;
        for(var i = 0; i < images.Count; i++)
        {
            var image = images[i];
            PixelFloating.CommitIfActive();
            var layer = PixelLayerOps.CreateLayer(canvas, above, true);
            layer.gameObject.name = GameObjectUtility.GetUniqueNameForSibling(canvas.transform, string.IsNullOrEmpty(image.name) ? "Dropped Image" : image.name);
            Begin(layer, image, center);
            above = layer;
        }
        Activate();
    }

    public static void PasteInto(PixelLayer layer, List<Image> images, Vector2? center)
    {
        if(layer == null || images == null || images.Count == 0) return;
        if(layer.locked || images.Count > 1)
        {
            PasteAsLayers(layer.Root, layer, images, center);
            return;
        }
        PixelFloating.CommitIfActive();
        Selection.activeGameObject = layer.gameObject;
        Begin(layer, images[0], center);
        Activate();
    }

    static void Begin(PixelLayer layer, Image image, Vector2? center)
    {
        if(center.HasValue) PixelFloating.BeginPaste(layer, image.pixels, image.width, image.height, center.Value);
        else PixelFloating.BeginPaste(layer, image.pixels, image.width, image.height);
    }

    static void Activate()
    {
        if(ToolManager.activeToolType != typeof(PixelPaintTool)) ToolManager.SetActiveTool<PixelPaintTool>();
        SceneView.RepaintAll();
    }

    static bool IsImagePath(string path)
    {
        if(string.IsNullOrEmpty(path)) return false;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        for(var i = 0; i < Extensions.Length; i++)
            if(ext == Extensions[i]) return true;
        return false;
    }

    static string Full(string path)
    {
        try
        {
            return Path.GetFullPath(path).Replace('\\', '/').ToLowerInvariant();
        }
        catch(System.Exception)
        {
            return path;
        }
    }

    static bool TryObject(Object obj, out Image image)
    {
        image = null;
        var sprite = obj as Sprite;
        var texture = sprite != null ? sprite.texture : obj as Texture2D;
        if(texture == null) return false;

        Color32[] pixels;
        int w, h;
        if(!ReadTexture(texture, out pixels, out w, out h)) return false;

        if(sprite != null)
        {
            var sx = texture.width > 0 ? w / (float)texture.width : 1f;
            var sy = texture.height > 0 ? h / (float)texture.height : 1f;
            var r = sprite.rect;
            var x0 = Mathf.Clamp(Mathf.RoundToInt(r.x * sx), 0, w);
            var y0 = Mathf.Clamp(Mathf.RoundToInt(r.y * sy), 0, h);
            var cw = Mathf.Clamp(Mathf.RoundToInt(r.width * sx), 0, w - x0);
            var ch = Mathf.Clamp(Mathf.RoundToInt(r.height * sy), 0, h - y0);
            if(cw <= 0 || ch <= 0) return false;
            var crop = new Color32[cw * ch];
            for(var y = 0; y < ch; y++)
                System.Array.Copy(pixels, (y0 + y) * w + x0, crop, y * cw, cw);
            pixels = crop;
            w = cw;
            h = ch;
        }

        image = new Image { name = obj.name, pixels = pixels, width = w, height = h };
        return true;
    }

    static bool ReadTexture(Texture2D texture, out Color32[] pixels, out int w, out int h)
    {
        pixels = null;
        w = 0;
        h = 0;
        var path = AssetDatabase.GetAssetPath(texture);
        if(!string.IsNullOrEmpty(path) && IsImagePath(path) && File.Exists(path))
        {
            Image fromFile;
            if(TryFile(path, out fromFile))
            {
                pixels = fromFile.pixels;
                w = fromFile.width;
                h = fromFile.height;
                return true;
            }
        }
        return Blit(texture, out pixels, out w, out h);
    }

    static bool Blit(Texture2D texture, out Color32[] pixels, out int w, out int h)
    {
        w = texture.width;
        h = texture.height;
        pixels = null;
        if(w <= 0 || h <= 0) return false;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply(false);
            pixels = copy.GetPixels32();
            return true;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(copy);
        }
    }

    static bool TryFile(string path, out Image image)
    {
        image = null;
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch(System.Exception)
        {
            return false;
        }
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if(!tex.LoadImage(bytes)) return false;
            image = new Image { name = Path.GetFileNameWithoutExtension(path), pixels = tex.GetPixels32(), width = tex.width, height = tex.height };
            return image.width > 0 && image.height > 0;
        }
        finally
        {
            Object.DestroyImmediate(tex);
        }
    }
}
#endif
