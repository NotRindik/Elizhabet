#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PixelCanvasEditing
{
    const int ImporterMaxSize = 16384;
    const string OriginPrefix = "pixeldrawer:";

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
        public readonly List<PixelLayer> users = new List<PixelLayer>();
    }

    static readonly Dictionary<PixelLayer, Edit> edits = new Dictionary<PixelLayer, Edit>();
    static readonly List<Edit> all = new List<Edit>();
    static readonly HashSet<Edit> touched = new HashSet<Edit>();

    public static bool LimitHit { get; set; }

    public static System.Action<PixelLayer, int, int, Color32, Color32> PixelChanged;

    static PixelCanvasEditing()
    {
        AssemblyReloadEvents.beforeAssemblyReload += () =>
        {
            PixelFloating.CommitIfActive();
            SaveAll();
        };
        EditorSceneManager.sceneSaving += (scene, path) =>
        {
            PixelFloating.CommitIfActive();
            if(!SaveAll()) EditorApplication.delayCall += () => SaveAll();
        };
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += RefreshOrigins;
        EditorApplication.playModeStateChanged += state =>
        {
            if(state != PlayModeStateChange.ExitingEditMode) return;
            PixelFloating.CommitIfActive();
            SaveAll();
            RefreshOrigins();
        };
        EditorApplication.delayCall += RefreshOrigins;
    }

    public static void SetPixel(PixelLayer layer, int x, int y, Color32 color)
    {
        var canvas = layer.Root;
        var edit = GetEdit(layer, canvas);
        var tx = x - edit.origin.x;
        var ty = y - edit.origin.y;

        if(tx < 0 || ty < 0 || tx >= edit.width || ty >= edit.height)
        {
            if(color.a == 0) return;
            if(!Grow(edit, canvas, x, y))
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
            MarkScenes(edit);
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

    public static Vector2Int Size(PixelLayer layer)
    {
        Edit edit;
        if(edits.TryGetValue(layer, out edit)) return new Vector2Int(edit.width, edit.height);
        var sr = layer.Surface(false);
        if(sr != null && sr.sprite != null)
        {
            var rect = sr.sprite.rect;
            return new Vector2Int((int)rect.width, (int)rect.height);
        }
        return Vector2Int.zero;
    }

    public static RectInt Bounds(PixelLayer layer)
    {
        Edit edit;
        if(edits.TryGetValue(layer, out edit)) return new RectInt(edit.origin.x, edit.origin.y, edit.width, edit.height);
        var sr = layer.Surface(false);
        if(sr == null || sr.sprite == null) return new RectInt(0, 0, 0, 0);
        var rect = sr.sprite.rect;
        return new RectInt(layer.origin.x, layer.origin.y, (int)rect.width, (int)rect.height);
    }

    public static RectInt CanvasBounds(PixelCanvas canvas)
    {
        var layers = canvas.GetComponentsInChildren<PixelLayer>(true);
        var has = false;
        int x0 = 0, y0 = 0, x1 = 0, y1 = 0;
        for(var i = 0; i < layers.Length; i++)
        {
            var b = Bounds(layers[i]);
            if(b.width <= 0 || b.height <= 0) continue;
            if(!has)
            {
                has = true;
                x0 = b.xMin;
                y0 = b.yMin;
                x1 = b.xMax;
                y1 = b.yMax;
            }
            else
            {
                x0 = Mathf.Min(x0, b.xMin);
                y0 = Mathf.Min(y0, b.yMin);
                x1 = Mathf.Max(x1, b.xMax);
                y1 = Mathf.Max(y1, b.yMax);
            }
        }
        return has ? new RectInt(x0, y0, x1 - x0, y1 - y0) : new RectInt(0, 0, 0, 0);
    }

    public static Color32[] ReadPixels(PixelLayer layer, out Vector2Int origin, out int w, out int h)
    {
        origin = Vector2Int.zero;
        w = 0;
        h = 0;
        if(layer == null || layer.Root == null) return null;
        if(!edits.ContainsKey(layer) && layer.Surface(false) == null) return null;
        var edit = GetEdit(layer, layer.Root);
        origin = edit.origin;
        w = edit.width;
        h = edit.height;
        return edit.pixels;
    }

    public static bool TryGetPixel(PixelLayer layer, int x, int y, out Color32 color)
    {
        color = default(Color32);
        if(layer == null || layer.Root == null) return false;
        if(!edits.ContainsKey(layer) && layer.Surface(false) == null) return false;
        var edit = GetEdit(layer, layer.Root);
        var tx = x - edit.origin.x;
        var ty = y - edit.origin.y;
        if(tx < 0 || ty < 0 || tx >= edit.width || ty >= edit.height) return false;
        color = edit.pixels[ty * edit.width + tx];
        return color.a > 0;
    }

    public static Color32 GetColor(PixelLayer layer, int x, int y)
    {
        Color32 c;
        TryGetPixel(layer, x, y, out c);
        return c;
    }

    public static string PathOf(PixelLayer layer)
    {
        Edit edit;
        if(edits.TryGetValue(layer, out edit)) return edit.path;
        var sr = layer.Surface(false);
        return sr != null && sr.sprite != null ? AssetDatabase.GetAssetPath(sr.sprite) : null;
    }

    public static void ShiftOrigin(PixelLayer layer, int dx, int dy)
    {
        if(layer == null || layer.Root == null) return;
        var edit = GetEdit(layer, layer.Root);
        if(edit.width == 0 || edit.height == 0) return;
        edit.origin = new Vector2Int(edit.origin.x + dx, edit.origin.y + dy);
        PlaceUsers(edit);
        edit.dirty = true;
        MarkScenes(edit);
    }

    public static void Expand(PixelLayer layer, RectInt rect)
    {
        if(rect.width <= 0 || rect.height <= 0 || layer.Root == null) return;
        var canvas = layer.Root;
        var edit = GetEdit(layer, canvas);
        if(!Contains(edit, rect.xMin, rect.yMin)) Grow(edit, canvas, rect.xMin, rect.yMin);
        if(!Contains(edit, rect.xMax - 1, rect.yMax - 1)) Grow(edit, canvas, rect.xMax - 1, rect.yMax - 1);
    }

    static bool Contains(Edit edit, int x, int y)
    {
        var tx = x - edit.origin.x;
        var ty = y - edit.origin.y;
        return tx >= 0 && ty >= 0 && tx < edit.width && ty < edit.height;
    }

    static bool Grow(Edit edit, PixelCanvas canvas, int x, int y)
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
        Commit(edit, canvas);
        return true;
    }

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
        Crop(edit, canvas, edit.origin.x + minX, edit.origin.y + minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }

    public static void Shrink(PixelLayer layer)
    {
        var canvas = layer != null ? layer.Root : null;
        if(canvas == null) return;
        Edit edit;
        if(!edits.TryGetValue(layer, out edit)) return;
        if(edit.width == 0 || edit.height == 0) return;

        int minX, minY, maxX, maxY;
        if(!ContentBounds(edit, out minX, out minY, out maxX, out maxY))
        {
            Clear(edit);
            return;
        }

        var step = Mathf.Clamp(canvas.growStep, 1, canvas.EffectiveMaxSize);
        var x0 = Mathf.Max(FloorDiv(edit.origin.x + minX, step) * step, edit.origin.x);
        var y0 = Mathf.Max(FloorDiv(edit.origin.y + minY, step) * step, edit.origin.y);
        var x1 = Mathf.Min(FloorDiv(edit.origin.x + maxX + 1 + step - 1, step) * step, edit.origin.x + edit.width);
        var y1 = Mathf.Min(FloorDiv(edit.origin.y + maxY + 1 + step - 1, step) * step, edit.origin.y + edit.height);
        Crop(edit, canvas, x0, y0, x1 - x0, y1 - y0);
    }

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

    static void Crop(Edit edit, PixelCanvas canvas, int cropX, int cropY, int w, int h)
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
        Commit(edit, canvas);
        edit.dirty = true;
        MarkScenes(edit);
    }

    static void Clear(Edit edit)
    {
        Release(edit);
        edit.sprite = null;
        edit.texture = null;
        edit.pixels = new Color32[0];
        edit.width = 0;
        edit.height = 0;
        edit.hasRect = false;
        for(var i = 0; i < edit.users.Count; i++)
        {
            var u = edit.users[i];
            if(u != null) u.Surface(true).sprite = null;
        }
        edit.dirty = true;
        MarkScenes(edit);
    }

    public static bool SaveAll()
    {
        var failed = new List<Edit>();
        for(var i = 0; i < all.Count; i++)
        {
            var edit = all[i];
            if(Save(edit)) Release(edit);
            else failed.Add(edit);
        }
        all.Clear();
        edits.Clear();
        touched.Clear();
        for(var i = 0; i < failed.Count; i++)
        {
            all.Add(failed[i]);
            for(var k = 0; k < failed[i].users.Count; k++)
                if(failed[i].users[k] != null) edits[failed[i].users[k]] = failed[i];
        }
        return failed.Count == 0;
    }

    static bool Save(Edit edit)
    {
        PixelLayer first = null;
        for(var i = 0; i < edit.users.Count; i++)
        {
            if(edit.users[i] == null) continue;
            first = edit.users[i];
            break;
        }
        if(first == null || first.Root == null) return true;
        var canvas = first.Root;

        if(!edit.dirty)
        {
            for(var i = 0; i < edit.users.Count; i++)
            {
                var u = edit.users[i];
                if(u == null) continue;
                u.Surface(true).sprite = edit.original;
            }
            return true;
        }

        if(edit.width == 0 || edit.texture == null)
        {
            if(!string.IsNullOrEmpty(edit.path) && File.Exists(edit.path)) AssetDatabase.DeleteAsset(edit.path);
            for(var i = 0; i < edit.users.Count; i++)
            {
                var u = edit.users[i];
                if(u == null) continue;
                var sr = u.Surface(true);
                sr.sprite = null;
                u.origin = Vector2Int.zero;
                u.PlaceSurface();
                EditorUtility.SetDirty(sr);
                EditorUtility.SetDirty(u);
            }
            return true;
        }

        var path = edit.path;
        if(string.IsNullOrEmpty(path))
        {
            if(string.IsNullOrEmpty(first.id)) first.id = NewId();
            path = AssetDatabase.GenerateUniqueAssetPath(FolderOf(canvas) + "/" + first.id + ".png");
        }
        File.WriteAllBytes(path, edit.texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        Configure(path, canvas.pixelsPerUnit, edit.origin);
        var saved = LoadSprite(path);
        if(saved == null)
        {
            Debug.LogError("PixelCanvas: sprite not found after import: " + path + " | file exists: " + File.Exists(path) + " | importer: " + (AssetImporter.GetAtPath(path) != null));
            return false;
        }
        for(var i = 0; i < edit.users.Count; i++)
        {
            var u = edit.users[i];
            if(u == null) continue;
            var sr = u.Surface(true);
            u.origin = edit.origin;
            u.PlaceSurface();
            sr.sprite = saved;
            EditorUtility.SetDirty(sr);
            EditorUtility.SetDirty(u);
        }
        return true;
    }

    static Edit GetEdit(PixelLayer layer, PixelCanvas canvas)
    {
        Edit edit;
        if(edits.TryGetValue(layer, out edit)) return edit;
        var sr = layer.Surface(true);
        var path = sr.sprite != null ? AssetDatabase.GetAssetPath(sr.sprite) : null;

        if(!string.IsNullOrEmpty(path))
        {
            for(var i = 0; i < all.Count; i++)
            {
                if(all[i].path != path) continue;
                edit = all[i];
                edit.users.Add(layer);
                edits[layer] = edit;
                layer.origin = edit.origin;
                sr.sprite = edit.sprite != null ? edit.sprite : edit.original;
                layer.PlaceSurface();
                return edit;
            }
        }

        edit = new Edit();
        edit.original = sr.sprite;
        edit.origin = layer.origin;
        edit.users.Add(layer);
        if(!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            edit.path = path;
            edit.origin = ReadOrigin(path, layer.origin);
            var temp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            temp.LoadImage(File.ReadAllBytes(path));
            edit.width = temp.width;
            edit.height = temp.height;
            edit.pixels = temp.GetPixels32();
            Object.DestroyImmediate(temp);
            BuildTexture(edit, canvas.pixelsPerUnit);
            sr.sprite = edit.sprite;
            layer.origin = edit.origin;
            layer.PlaceSurface();
        }
        else
        {
            edit.path = null;
            edit.pixels = new Color32[0];
        }
        all.Add(edit);
        edits[layer] = edit;
        return edit;
    }

    static void Commit(Edit edit, PixelCanvas canvas)
    {
        BuildTexture(edit, canvas.pixelsPerUnit);
        for(var i = 0; i < edit.users.Count; i++)
        {
            var u = edit.users[i];
            if(u == null) continue;
            u.origin = edit.origin;
            u.Surface(true).sprite = edit.sprite;
            u.PlaceSurface();
            EditorUtility.SetDirty(u);
        }
    }

    static void PlaceUsers(Edit edit)
    {
        for(var i = 0; i < edit.users.Count; i++)
        {
            var u = edit.users[i];
            if(u == null) continue;
            u.origin = edit.origin;
            u.PlaceSurface();
            EditorUtility.SetDirty(u);
        }
    }

    static void MarkScenes(Edit edit)
    {
        for(var i = 0; i < edit.users.Count; i++)
        {
            var u = edit.users[i];
            if(u != null) EditorSceneManager.MarkSceneDirty(u.gameObject.scene);
        }
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

    public static Sprite LoadSprite(string path)
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

    public static string NewId()
    {
        return System.Guid.NewGuid().ToString("N").Substring(0, 8);
    }

    public static string FolderOf(PixelCanvas canvas)
    {
        if(string.IsNullOrEmpty(canvas.assetFolder))
        {
            canvas.assetFolder = DefaultFolder(canvas);
            EditorUtility.SetDirty(canvas);
        }
        PixelDrawerAssets.EnsureFolder(canvas.assetFolder);
        return canvas.assetFolder;
    }

    public static string NewFolderFor(PixelCanvas canvas)
    {
        var folder = AssetDatabase.GenerateUniqueAssetPath(DefaultFolder(canvas));
        PixelDrawerAssets.EnsureFolder(folder);
        return folder;
    }

    static string DefaultFolder(PixelCanvas canvas)
    {
        var scene = canvas.gameObject.scene;
        var dir = string.IsNullOrEmpty(scene.path) ? "Assets" : Path.GetDirectoryName(scene.path).Replace('\\', '/');
        var sceneName = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;
        return dir + "/" + sceneName + "_" + canvas.name;
    }

    public static void Configure(string path, float ppu, Vector2Int origin)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer == null) return;
        var tag = OriginPrefix + origin.x + "," + origin.y;
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
            && importer.textureCompression == TextureImporterCompression.Uncompressed
            && importer.userData == tag;
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
        importer.userData = tag;
        importer.SaveAndReimport();
    }

    static Vector2Int ReadOrigin(string path, Vector2Int fallback)
    {
        var importer = AssetImporter.GetAtPath(path);
        if(importer == null) return fallback;
        var data = importer.userData;
        if(string.IsNullOrEmpty(data) || !data.StartsWith(OriginPrefix)) return fallback;
        var parts = data.Substring(OriginPrefix.Length).Split(',');
        int x, y;
        if(parts.Length != 2 || !int.TryParse(parts[0], out x) || !int.TryParse(parts[1], out y)) return fallback;
        return new Vector2Int(x, y);
    }

    public static void RefreshOrigins()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        var layers = PixelCanvasSync.Layers();
        for(var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            if(edits.ContainsKey(layer)) continue;
            var sr = layer.Surface(false);
            if(sr == null || sr.sprite == null) continue;
            var path = AssetDatabase.GetAssetPath(sr.sprite);
            if(string.IsNullOrEmpty(path)) continue;
            var o = ReadOrigin(path, layer.origin);
            if(o == layer.origin) continue;
            layer.origin = o;
            layer.PlaceSurface();
            EditorUtility.SetDirty(layer);
        }
    }
}
#endif
