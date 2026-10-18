using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PixelCanvasEditing
{
    const int ImporterMaxSize = 16384;

    class Edit
    {
        public Color32[] pixels;
        public int width;
        public int height;
        public Vector2Int origin;
        public Texture2D texture;
        public Sprite sprite;
        public Sprite original;
        public string path;
        public bool dirty;
        public bool hasRect;
        public int minX, minY, maxX, maxY;
    }

    static readonly Dictionary<PixelLayer, Edit> edits = new Dictionary<PixelLayer, Edit>();
    static readonly HashSet<Edit> touched = new HashSet<Edit>();

    // true if the last stroke tried to paint outside the canvas limit
    public static bool LimitHit { get; set; }

    // (layer, x, y, oldColor, newColor): fired for every pixel that actually changes. Used by PixelHistory.
    public static System.Action<PixelLayer, int, int, Color32, Color32> PixelChanged;

    static PixelLayer plotLayer;
    static int sinceDab;
    static readonly System.Action<int, int, Color32> plot = (x, y, c) => SetPixel(plotLayer, x, y, c);

    static PixelCanvasEditing()
    {
        AssemblyReloadEvents.beforeAssemblyReload += () => SaveAll();
        EditorSceneManager.sceneSaving += (scene, path) =>
        {
            if(!SaveAll()) EditorApplication.delayCall += () => SaveAll();
        };
        EditorApplication.playModeStateChanged += state =>
        {
            if(state == PlayModeStateChange.ExitingEditMode) SaveAll();
        };
    }

    // ---------- Drawing ----------

    // Walks a Bresenham line and dabs the brush along it, honoring brush.spacing.
    // includeStart: dab at 'a' too (true for the first point of a stroke, false for continuation segments).
    public static void Line(PixelLayer layer, Vector2Int a, Vector2Int b, PixelBrush brush, int size, Color32 color, bool includeStart)
    {
        plotLayer = layer;
        var spacing = Mathf.Max(1, brush.spacing);
        var dx = Mathf.Abs(b.x - a.x);
        var dy = -Mathf.Abs(b.y - a.y);
        var sx = a.x < b.x ? 1 : -1;
        var sy = a.y < b.y ? 1 : -1;
        var err = dx + dy;
        var x = a.x;
        var y = a.y;
        var first = true;
        while(true)
        {
            if(first && includeStart)
            {
                brush.Dab(x, y, size, color, plot);
                sinceDab = 0;
            }
            else if(!first && ++sinceDab >= spacing)
            {
                brush.Dab(x, y, size, color, plot);
                sinceDab = 0;
            }
            first = false;
            if(x == b.x && y == b.y) break;
            var e2 = 2 * err;
            if(e2 >= dy)
            {
                err += dy;
                x += sx;
            }
            if(e2 <= dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

    public static void SetPixel(PixelLayer layer, int x, int y, Color32 color)
    {
        var canvas = layer.Root;
        var edit = GetEdit(layer, canvas);
        var tx = x - edit.origin.x;
        var ty = y - edit.origin.y;

        if(tx < 0 || ty < 0 || tx >= edit.width || ty >= edit.height)
        {
            if(color.a == 0) return; // erasing outside the sprite changes nothing
            if(!Grow(layer, edit, canvas, x, y))
            {
                if(!LimitHit)
                {
                    LimitHit = true;
                    Debug.LogWarning("PixelCanvas: canvas limit reached (" + canvas.EffectiveMaxSize + "x" + canvas.EffectiveMaxSize +
                                     "). Raise 'Max Size' on the Pixel Canvas, or continue in a new layer/canvas.");
                }
                return;
            }
            tx = x - edit.origin.x;
            ty = y - edit.origin.y;
        }

        var index = ty * edit.width + tx;
        var previous = edit.pixels[index];
        if(Same(previous, color)) return;
        edit.pixels[index] = color;
        if(PixelChanged != null) PixelChanged(layer, x, y, previous, color);

        if(!edit.dirty)
        {
            edit.dirty = true;
            EditorSceneManager.MarkSceneDirty(layer.gameObject.scene);
        }
        if(!edit.hasRect)
        {
            edit.hasRect = true;
            edit.minX = edit.maxX = tx;
            edit.minY = edit.maxY = ty;
        }
        else
        {
            if(tx < edit.minX) edit.minX = tx;
            if(tx > edit.maxX) edit.maxX = tx;
            if(ty < edit.minY) edit.minY = ty;
            if(ty > edit.maxY) edit.maxY = ty;
        }
        touched.Add(edit);
    }

    // Uploads only the changed rectangle of each touched layer.
    public static void Apply()
    {
        foreach(var edit in touched)
        {
            if(edit.texture == null || !edit.hasRect) continue;
            var w = edit.maxX - edit.minX + 1;
            var h = edit.maxY - edit.minY + 1;
            if(w == edit.width && h == edit.height)
            {
                edit.texture.SetPixels32(edit.pixels);
            }
            else
            {
                var block = new Color32[w * h];
                for(var row = 0; row < h; row++)
                    System.Array.Copy(edit.pixels, (edit.minY + row) * edit.width + edit.minX, block, row * w, w);
                edit.texture.SetPixels32(edit.minX, edit.minY, w, h, block);
            }
            edit.texture.Apply(false);
            edit.hasRect = false;
        }
        touched.Clear();
    }

    // ---------- Size info ----------

    public static Vector2Int Size(PixelLayer layer)
    {
        if(edits.TryGetValue(layer, out var edit)) return new Vector2Int(edit.width, edit.height);
        var sr = layer.Surface(false);
        if(sr != null && sr.sprite != null)
        {
            var rect = sr.sprite.rect;
            return new Vector2Int((int)rect.width, (int)rect.height);
        }
        return Vector2Int.zero;
    }

    // ---------- Growing ----------

    static bool Grow(PixelLayer layer, Edit edit, PixelCanvas canvas, int x, int y)
    {
        var max = canvas.EffectiveMaxSize;
        var step = Mathf.Clamp(canvas.growStep, 1, max);
        int nx, nw, ny, nh;
        var empty = edit.width == 0 || edit.height == 0;
        if(!GrowAxis(edit.origin.x, empty ? 0 : edit.width, x, step, max, out nx, out nw)) return false;
        if(!GrowAxis(edit.origin.y, empty ? 0 : edit.height, y, step, max, out ny, out nh)) return false;

        var pixels = new Color32[nw * nh];
        if(!empty)
        {
            var ox = edit.origin.x - nx;
            var oy = edit.origin.y - ny;
            for(var row = 0; row < edit.height; row++)
                System.Array.Copy(edit.pixels, row * edit.width, pixels, (row + oy) * nw + ox, edit.width);
        }

        edit.pixels = pixels;
        edit.width = nw;
        edit.height = nh;
        edit.origin = new Vector2Int(nx, ny);
        edit.hasRect = false;
        Commit(layer, edit, canvas);
        return true;
    }

    // Grows one axis so that it contains p. New edges snap to a grid of 'step' pixels, total size never exceeds max.
    static bool GrowAxis(int curMin, int curSize, int p, int step, int max, out int newMin, out int newSize)
    {
        if(curSize == 0)
        {
            newMin = FloorDiv(p, step) * step;
            newSize = step;
            return true;
        }
        var curMax = curMin + curSize;
        var nMin = curMin;
        var nMax = curMax;
        newMin = curMin;
        newSize = curSize;
        if(p < curMin)
        {
            if(curMax - p > max) return false;
            nMin = Mathf.Max(FloorDiv(p, step) * step, curMax - max);
        }
        else if(p >= curMax)
        {
            if(p + 1 - curMin > max) return false;
            nMax = Mathf.Min((FloorDiv(p, step) + 1) * step, curMin + max);
        }
        newMin = nMin;
        newSize = nMax - nMin;
        return true;
    }

    // ---------- Trim / Shrink ----------

    // Exact trim of all empty borders (manual command).
    public static bool Trim(PixelLayer layer)
    {
        var canvas = layer.Root;
        var edit = GetEdit(layer, canvas);
        if(edit.width == 0 || edit.height == 0) return false;
        int minX, minY, maxX, maxY;
        if(!ContentBounds(edit, out minX, out minY, out maxX, out maxY))
        {
            Debug.Log("PixelCanvas: layer is empty, nothing to trim.");
            return false;
        }
        Crop(layer, edit, canvas, edit.origin.x + minX, edit.origin.y + minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }

    // Automatic counterpart of Grow: shrinks the sprite to the painted area, snapped outward to the grow-step grid
    // (same grid Grow uses, so the canvas doesn't flip-flop). Never grows. A fully empty layer becomes 0x0 (PNG removed on save).
    public static void Shrink(PixelLayer layer)
    {
        var canvas = layer != null ? layer.Root : null;
        if(canvas == null) return;
        if(!edits.TryGetValue(layer, out var edit)) return;
        if(edit.width == 0 || edit.height == 0) return;

        int minX, minY, maxX, maxY;
        if(!ContentBounds(edit, out minX, out minY, out maxX, out maxY))
        {
            Clear(layer, edit);
            return;
        }

        var step = Mathf.Clamp(canvas.growStep, 1, canvas.EffectiveMaxSize);
        var x0 = Mathf.Max(FloorDiv(edit.origin.x + minX, step) * step, edit.origin.x);
        var y0 = Mathf.Max(FloorDiv(edit.origin.y + minY, step) * step, edit.origin.y);
        var x1 = Mathf.Min(FloorDiv(edit.origin.x + maxX + 1 + step - 1, step) * step, edit.origin.x + edit.width);
        var y1 = Mathf.Min(FloorDiv(edit.origin.y + maxY + 1 + step - 1, step) * step, edit.origin.y + edit.height);
        Crop(layer, edit, canvas, x0, y0, x1 - x0, y1 - y0);
    }

    // Reads a pixel in layer coordinates. Returns false if it is outside the sprite or fully transparent.
    public static bool TryGetPixel(PixelLayer layer, int x, int y, out Color32 color)
    {
        color = default(Color32);
        if(layer == null || layer.Root == null) return false;
        if(!edits.ContainsKey(layer) && layer.Surface(false) == null) return false; // never painted: nothing to read
        var edit = GetEdit(layer, layer.Root);
        var tx = x - edit.origin.x;
        var ty = y - edit.origin.y;
        if(tx < 0 || ty < 0 || tx >= edit.width || ty >= edit.height) return false;
        color = edit.pixels[ty * edit.width + tx];
        return color.a > 0;
    }

    // Bounds (inclusive, texture coords) of non-transparent pixels. Scans from the edges inward.
    static bool ContentBounds(Edit edit, out int minX, out int minY, out int maxX, out int maxY)
    {
        var w = edit.width;
        var h = edit.height;
        minX = w;
        maxX = -1;
        minY = -1;
        maxY = -1;

        for(var y = 0; y < h && minY < 0; y++)
            if(RowHasContent(edit, y)) minY = y;
        if(minY < 0) return false;
        for(var y = h - 1; y >= minY && maxY < 0; y--)
            if(RowHasContent(edit, y)) maxY = y;

        for(var y = minY; y <= maxY; y++)
        {
            var row = y * w;
            for(var x = 0; x < minX; x++)
            {
                if(edit.pixels[row + x].a == 0) continue;
                minX = x;
                break;
            }
            for(var x = w - 1; x > maxX; x--)
            {
                if(edit.pixels[row + x].a == 0) continue;
                maxX = x;
                break;
            }
        }
        return maxX >= minX;
    }

    static bool RowHasContent(Edit edit, int y)
    {
        var row = y * edit.width;
        for(var x = 0; x < edit.width; x++)
            if(edit.pixels[row + x].a != 0) return true;
        return false;
    }

    // Crops to an absolute rectangle (must lie inside the current sprite).
    static void Crop(PixelLayer layer, Edit edit, PixelCanvas canvas, int cropX, int cropY, int w, int h)
    {
        if(w == edit.width && h == edit.height && cropX == edit.origin.x && cropY == edit.origin.y) return;
        var sx = cropX - edit.origin.x;
        var sy = cropY - edit.origin.y;
        var pixels = new Color32[w * h];
        for(var row = 0; row < h; row++)
            System.Array.Copy(edit.pixels, (sy + row) * edit.width + sx, pixels, row * w, w);

        edit.pixels = pixels;
        edit.width = w;
        edit.height = h;
        edit.origin = new Vector2Int(cropX, cropY);
        edit.hasRect = false;
        Commit(layer, edit, canvas);
        edit.dirty = true;
        EditorSceneManager.MarkSceneDirty(layer.gameObject.scene);
    }

    static void Clear(PixelLayer layer, Edit edit)
    {
        Release(edit);
        edit.sprite = null;
        edit.texture = null;
        edit.pixels = new Color32[0];
        edit.width = 0;
        edit.height = 0;
        edit.hasRect = false;
        layer.Surface(true).sprite = null;
        edit.dirty = true;
        EditorSceneManager.MarkSceneDirty(layer.gameObject.scene);
    }

    // ---------- Saving ----------

    public static bool SaveAll()
    {
        var failed = new List<KeyValuePair<PixelLayer, Edit>>();
        foreach(var pair in edits)
        {
            if(pair.Key == null)
            {
                Release(pair.Value);
                continue;
            }
            if(Save(pair.Key, pair.Value)) Release(pair.Value);
            else failed.Add(pair);
        }
        edits.Clear();
        touched.Clear();
        for(var i = 0; i < failed.Count; i++) edits[failed[i].Key] = failed[i].Value;
        return failed.Count == 0;
    }

    static bool Save(PixelLayer layer, Edit edit)
    {
        var sr = layer.Surface(true);
        if(!edit.dirty)
        {
            sr.sprite = edit.original;
            return true;
        }
        if(edit.width == 0 || edit.texture == null)
        {
            // everything was erased: no sprite, no PNG
            if(!string.IsNullOrEmpty(edit.path) && File.Exists(edit.path)) AssetDatabase.DeleteAsset(edit.path);
            sr.sprite = null;
            layer.origin = Vector2Int.zero;
            layer.PlaceSurface();
            EditorUtility.SetDirty(sr);
            EditorUtility.SetDirty(layer);
            return true;
        }
        var canvas = layer.Root;
        var path = edit.path;
        if(string.IsNullOrEmpty(path)) path = Folder(canvas) + "/" + layer.id + ".png";
        File.WriteAllBytes(path, edit.texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        SetupImporter(path, canvas.pixelsPerUnit);
        var saved = LoadSprite(path);
        if(saved == null)
        {
            Debug.LogError("PixelCanvas: sprite not found after import: " + path + " | file exists: " + File.Exists(path) + " | importer: " + (AssetImporter.GetAtPath(path) != null));
            return false;
        }
        layer.origin = edit.origin;
        layer.PlaceSurface();
        sr.sprite = saved;
        EditorUtility.SetDirty(sr);
        EditorUtility.SetDirty(layer);
        return true;
    }

    // ---------- Internals ----------

    static Edit GetEdit(PixelLayer layer, PixelCanvas canvas)
    {
        if(edits.TryGetValue(layer, out var edit)) return edit;
        var sr = layer.Surface(true);
        edit = new Edit();
        edit.original = sr.sprite;
        edit.origin = layer.origin;
        edit.path = sr.sprite != null ? AssetDatabase.GetAssetPath(sr.sprite) : null;
        if(!string.IsNullOrEmpty(edit.path) && File.Exists(edit.path))
        {
            var temp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            temp.LoadImage(File.ReadAllBytes(edit.path));
            edit.width = temp.width;
            edit.height = temp.height;
            edit.pixels = temp.GetPixels32();
            Object.DestroyImmediate(temp);
            BuildTexture(edit, canvas.pixelsPerUnit);
            sr.sprite = edit.sprite;
        }
        else
        {
            edit.path = null;
            edit.pixels = new Color32[0];
        }
        edits[layer] = edit;
        return edit;
    }

    // Applies new pixels/size/origin: rebuilds texture + sprite and repositions the renderer so drawn pixels stay in place.
    static void Commit(PixelLayer layer, Edit edit, PixelCanvas canvas)
    {
        BuildTexture(edit, canvas.pixelsPerUnit);
        layer.origin = edit.origin;
        layer.Surface(true).sprite = edit.sprite;
        layer.PlaceSurface();
        EditorUtility.SetDirty(layer);
    }

    static void BuildTexture(Edit edit, float ppu)
    {
        if(edit.sprite != null) Object.DestroyImmediate(edit.sprite);
        if(edit.texture != null) Object.DestroyImmediate(edit.texture);
        edit.texture = new Texture2D(edit.width, edit.height, TextureFormat.RGBA32, false);
        edit.texture.hideFlags = HideFlags.HideAndDontSave;
        edit.texture.filterMode = FilterMode.Point;
        edit.texture.wrapMode = TextureWrapMode.Clamp;
        edit.texture.SetPixels32(edit.pixels);
        edit.texture.Apply(false);
        edit.sprite = Sprite.Create(edit.texture, new Rect(0, 0, edit.width, edit.height), Vector2.zero, ppu, 0, SpriteMeshType.FullRect);
        edit.sprite.hideFlags = HideFlags.HideAndDontSave;
    }

    static bool Same(Color32 a, Color32 b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }

    static int FloorDiv(int value, int size)
    {
        var result = value / size;
        if(value % size < 0) result--;
        return result;
    }

    static Sprite LoadSprite(string path)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for(var i = 0; i < assets.Length; i++)
            if(assets[i] is Sprite sprite) return sprite;
        return null;
    }

    static void Release(Edit edit)
    {
        if(edit.sprite != null) Object.DestroyImmediate(edit.sprite);
        if(edit.texture != null) Object.DestroyImmediate(edit.texture);
    }

    static string Folder(PixelCanvas canvas)
    {
        if(string.IsNullOrEmpty(canvas.assetFolder))
        {
            var scene = canvas.gameObject.scene;
            var dir = string.IsNullOrEmpty(scene.path) ? "Assets" : Path.GetDirectoryName(scene.path).Replace('\\', '/');
            var sceneName = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;
            canvas.assetFolder = dir + "/" + sceneName + "_" + canvas.name;
            EditorUtility.SetDirty(canvas);
        }
        EnsureFolder(canvas.assetFolder);
        return canvas.assetFolder;
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

    static void SetupImporter(string path, float ppu)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer == null) return;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        var ok = settings.textureType == TextureImporterType.Sprite
            && settings.spriteMode == (int)SpriteImportMode.Single
            && Mathf.Approximately(settings.spritePixelsPerUnit, ppu)
            && settings.filterMode == FilterMode.Point
            && !settings.mipmapEnabled
            && !settings.readable
            && settings.npotScale == TextureImporterNPOTScale.None
            && settings.spriteAlignment == (int)SpriteAlignment.BottomLeft
            && settings.spriteMeshType == SpriteMeshType.FullRect
            && importer.maxTextureSize == ImporterMaxSize
            && importer.textureCompression == TextureImporterCompression.Uncompressed;
        if(ok) return;
        settings.textureType = TextureImporterType.Sprite;
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spritePixelsPerUnit = ppu;
        settings.filterMode = FilterMode.Point;
        settings.mipmapEnabled = false;
        settings.alphaIsTransparency = true;
        settings.readable = false;
        settings.npotScale = TextureImporterNPOTScale.None;
        settings.spriteAlignment = (int)SpriteAlignment.BottomLeft;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.maxTextureSize = ImporterMaxSize;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
}
