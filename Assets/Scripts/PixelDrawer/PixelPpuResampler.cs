#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PixelPpuResampler
{
    const double Eps = 1e-6;

    class Axis
    {
        public int[] start;
        public int[] count;
        public double[][] weights;
        public int maxCount;
    }

    public static float DetectStoredPpu(PixelCanvas canvas)
    {
        if(canvas == null) return -1;
        var layers = canvas.GetComponentsInChildren<PixelLayer>(true);
        var found = -1f;
        for(var i = 0; i < layers.Length; i++)
        {
            var ppu = StoredPpu(layers[i]);
            if(ppu <= 0) continue;
            if(found < 0) found = ppu;
            else if(!Mathf.Approximately(found, ppu)) return -1;
        }
        return found;
    }

    static float StoredPpu(PixelLayer layer)
    {
        if(layer.art != null) return layer.art.pixelsPerUnit;
        var sr = layer.Surface(false);
        return sr != null && sr.sprite != null ? sr.sprite.pixelsPerUnit : -1;
    }

    public static bool Resample(PixelCanvas canvas, float from, float to)
    {
        if(canvas == null || from <= 0 || to <= 0) return false;
        if(Mathf.Approximately(from, to))
        {
            EditorUtility.DisplayDialog("Pixel Canvas", "Source and target PPU are the same.", "OK");
            return false;
        }

        PixelFloating.CommitIfActive();
        PixelArtPreview.StopAll();
        if(!PixelCanvasEditing.SaveAll())
        {
            EditorUtility.DisplayDialog("Pixel Canvas", "Could not save pending edits. Fix the errors in the console first.", "OK");
            return false;
        }

        var canvases = LinkedCanvases(canvas);
        for(var i = 0; i < canvases.Count; i++)
        {
            if(Mathf.Approximately(canvases[i].pixelsPerUnit, canvas.pixelsPerUnit)) continue;
            EditorUtility.DisplayDialog("Pixel Canvas",
                "Canvas '" + canvases[i].name + "' shares assets with '" + canvas.name + "' but has a different PPU (" + canvases[i].pixelsPerUnit + " vs " + canvas.pixelsPerUnit + ").\n\nMake its layers unique first.", "OK");
            return false;
        }

        var layers = new List<PixelLayer>();
        for(var i = 0; i < canvases.Count; i++) layers.AddRange(canvases[i].GetComponentsInChildren<PixelLayer>(true));

        var scale = (double)to / from;
        var arts = new List<PixelArtAsset>();
        var pngs = new List<string>();
        var needed = 0;
        for(var i = 0; i < layers.Count; i++)
        {
            var l = layers[i];
            if(l.art != null)
            {
                if(arts.Contains(l.art)) continue;
                arts.Add(l.art);
                for(var f = 0; f < l.art.frames.Count; f++)
                {
                    var frame = l.art.frames[f];
                    if(frame.color == null) continue;
                    needed = Mathf.Max(needed, NewSize(frame.origin.x, frame.color.width, scale), NewSize(frame.origin.y, frame.color.height, scale));
                }
                continue;
            }
            var sr = l.Surface(false);
            if(sr == null || sr.sprite == null) continue;
            var path = AssetDatabase.GetAssetPath(sr.sprite);
            if(string.IsNullOrEmpty(path) || pngs.Contains(path)) continue;
            pngs.Add(path);
            var rect = sr.sprite.rect;
            needed = Mathf.Max(needed, NewSize(l.origin.x, (int)rect.width, scale), NewSize(l.origin.y, (int)rect.height, scale));
        }

        if(needed > SystemInfo.maxTextureSize)
        {
            EditorUtility.DisplayDialog("Pixel Canvas", "After resampling a layer would be " + needed + " px, the GPU limit is " + SystemInfo.maxTextureSize + " px. Split the layer first.", "OK");
            return false;
        }

        var names = "";
        for(var i = 0; i < canvases.Count; i++) names += "\n  • " + canvases[i].name;
        var message = "Resample PPU " + from + " → " + to + " (x" + scale.ToString("0.###") + ").\n\n" +
                      "Canvases:" + names + "\n\n" +
                      arts.Count + " Pixel Art asset(s) with all frames, " + pngs.Count + " PNG(s).\n" +
                      "Art keeps its size and position in the world, pixel count changes.\n\n" +
                      "This cannot be undone. Commit to git first.";
        if(!EditorUtility.DisplayDialog("Pixel Canvas", message, "Resample", "Cancel")) return false;

        for(var i = 0; i < canvases.Count; i++)
        {
            PixelSelection.Clear(canvases[i]);
            ApplyCanvasSettings(canvases[i], to, scale, needed);
        }
        PixelHistory.Clear();

        try
        {
            var total = arts.Count + pngs.Count;
            var done = 0;
            for(var i = 0; i < arts.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Pixel Canvas", "Resampling " + arts[i].name, (float)done++ / total);
                ResampleArt(arts[i], scale, to);
            }
            var png = new PngPixelStore();
            for(var i = 0; i < pngs.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Pixel Canvas", "Resampling " + pngs[i], (float)done++ / total);
                ResamplePng(png, pngs[i], layers, scale);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        for(var i = 0; i < layers.Count; i++)
        {
            if(layers[i].art != null) layers[i].Present();
            else layers[i].PlaceSurface();
            EditorUtility.SetDirty(layers[i]);
        }

        for(var i = 0; i < canvases.Count; i++)
        {
            var c = canvases[i];
            c.ShowFrame(c.frame, true);
            if(c.baked != null)
            {
                var useBaked = c.useBakedInPlay;
                PixelBaker.Bake(c);
                c.useBakedInPlay = useBaked;
            }
            EditorUtility.SetDirty(c);
            EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
        }
        AssetDatabase.SaveAssets();
        SceneView.RepaintAll();
        Debug.Log("PixelCanvas: resampled " + canvases.Count + " canvas(es) from PPU " + from + " to " + to + ".");
        return true;
    }

    static List<PixelCanvas> LinkedCanvases(PixelCanvas canvas)
    {
        var result = new List<PixelCanvas> { canvas };
        var all = PixelCanvasSync.Layers();
        var changed = true;
        while(changed)
        {
            changed = false;
            var keys = new HashSet<Object>();
            for(var i = 0; i < all.Count; i++)
            {
                if(!result.Contains(all[i].Root)) continue;
                var key = KeyObject(all[i]);
                if(key != null) keys.Add(key);
            }
            for(var i = 0; i < all.Count; i++)
            {
                var root = all[i].Root;
                if(root == null || result.Contains(root)) continue;
                var key = KeyObject(all[i]);
                if(key == null || !keys.Contains(key)) continue;
                result.Add(root);
                changed = true;
            }
        }
        return result;
    }

    static Object KeyObject(PixelLayer layer)
    {
        if(layer.art != null) return layer.art;
        var sr = layer.Surface(false);
        return sr != null && sr.sprite != null ? sr.sprite.texture : null;
    }

    static void ApplyCanvasSettings(PixelCanvas c, float to, double scale, int needed)
    {
        c.pixelsPerUnit = to;
        c.growStep = Mathf.Max(1, (int)System.Math.Round(c.growStep * scale));
        c.tileSize = new Vector2Int(Mathf.Max(1, (int)System.Math.Round(c.tileSize.x * scale)), Mathf.Max(1, (int)System.Math.Round(c.tileSize.y * scale)));
        c.referenceOffset = new Vector2((float)(c.referenceOffset.x * scale), (float)(c.referenceOffset.y * scale));
        c.referenceScale = (float)(c.referenceScale * scale);
        if(needed > c.maxSize) c.maxSize = needed;
    }

    static void ResampleArt(PixelArtAsset art, double scale, float to)
    {
        var n = art.frames.Count;
        var leader = new int[n];
        var durations = new float[n];
        var datas = new PixelData[n];
        for(var i = 0; i < n; i++)
        {
            var f = art.frames[i];
            durations[i] = f.duration;
            leader[i] = i;
            for(var j = 0; j < i; j++)
            {
                var g = art.frames[j];
                if(f.color == null || g.color != f.color || g.origin != f.origin) continue;
                leader[i] = leader[j];
                break;
            }
            if(leader[i] != i || f.color == null) continue;
            var w = f.color.width;
            var h = f.color.height;
            var pixels = f.color.GetPixels32();
            var emission = f.emission != null ? AssetPixelStore.ReadEmission(f.emission, w * h) : null;
            datas[i] = Resample(pixels, emission, w, h, f.origin, scale);
        }

        for(var i = 0; i < n; i++)
            if(leader[i] != i) art.frames[i] = new PixelArtAsset.Frame { duration = durations[i] };

        for(var i = 0; i < n; i++)
            if(leader[i] == i) AssetPixelStore.Write(art, i, datas[i], to);

        for(var i = 0; i < n; i++)
        {
            if(leader[i] == i) continue;
            var link = AssetPixelStore.Link(art.frames[leader[i]]);
            link.duration = durations[i];
            art.frames[i] = link;
        }
        art.pixelsPerUnit = to;
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssetIfDirty(art);
    }

    static void ResamplePng(PngPixelStore store, string path, List<PixelLayer> layers, double scale)
    {
        var users = new List<PixelLayer>();
        for(var i = 0; i < layers.Count; i++)
        {
            if(layers[i].art != null) continue;
            var sr = layers[i].Surface(false);
            if(sr != null && sr.sprite != null && AssetDatabase.GetAssetPath(sr.sprite) == path) users.Add(layers[i]);
        }
        if(users.Count == 0) return;
        var first = users[0];
        PixelData data;
        if(!store.Load(first, out data) || data.width == 0 || data.height == 0) return;
        var result = Resample(data.pixels, null, data.width, data.height, data.origin, scale);
        PixelSaved saved;
        if(!store.Save(first, users, path, result, first.Root, out saved)) return;
        for(var i = 0; i < users.Count; i++)
        {
            var u = users[i];
            var sr = u.Surface(true);
            sr.sprite = saved.sprite;
            u.origin = result.origin;
            u.PlaceSurface();
            EditorUtility.SetDirty(sr);
            EditorUtility.SetDirty(u);
        }
    }

    static int NewSize(int origin, int size, double scale)
    {
        return CeilEps((origin + size) * scale) - FloorEps(origin * scale);
    }

    public static PixelData Resample(Color32[] src, byte[] emission, int w, int h, Vector2Int origin, double scale)
    {
        var x0 = FloorEps(origin.x * scale);
        var y0 = FloorEps(origin.y * scale);
        var dw = CeilEps((origin.x + w) * scale) - x0;
        var dh = CeilEps((origin.y + h) * scale) - y0;
        var inv = 1.0 / scale;
        var ax = BuildAxis(x0, dw, origin.x, w, inv);
        var ay = BuildAxis(y0, dh, origin.y, h, inv);
        var half = inv * inv * 0.5 - Eps;

        var cap = ax.maxCount * ay.maxCount;
        var keys = new uint[cap];
        var weights = new double[cap];
        var glows = new byte[cap];
        var colors = new Color32[cap];

        var dst = new Color32[dw * dh];
        var dstGlow = emission != null ? new byte[dw * dh] : null;

        for(var y = 0; y < dh; y++)
        {
            var sy = ay.start[y];
            var cy = ay.count[y];
            var wy = ay.weights[y];
            for(var x = 0; x < dw; x++)
            {
                var sx = ax.start[x];
                var cx = ax.count[x];
                var wx = ax.weights[x];
                var n = 0;
                var opaque = 0.0;
                for(var j = 0; j < cy; j++)
                {
                    var row = (sy + j) * w;
                    for(var i = 0; i < cx; i++)
                    {
                        var index = row + sx + i;
                        var c = src[index];
                        if(c.a == 0) continue;
                        var weight = wy[j] * wx[i];
                        opaque += weight;
                        var key = (uint)(c.r << 24 | c.g << 16 | c.b << 8 | c.a);
                        var g = emission != null ? emission[index] : (byte)0;
                        var k = 0;
                        while(k < n && keys[k] != key) k++;
                        if(k == n)
                        {
                            keys[n] = key;
                            colors[n] = c;
                            weights[n] = weight;
                            glows[n] = g;
                            n++;
                        }
                        else
                        {
                            weights[k] += weight;
                            if(g > glows[k]) glows[k] = g;
                        }
                    }
                }
                if(n == 0 || opaque < half) continue;
                var best = 0;
                for(var k = 1; k < n; k++)
                    if(weights[k] > weights[best] + Eps) best = k;
                dst[y * dw + x] = colors[best];
                if(dstGlow != null) dstGlow[y * dw + x] = glows[best];
            }
        }

        var data = new PixelData();
        data.pixels = dst;
        data.emission = dstGlow;
        data.width = dw;
        data.height = dh;
        data.origin = new Vector2Int(x0, y0);
        return data;
    }

    static Axis BuildAxis(int dstMin, int dstSize, int srcMin, int srcSize, double inv)
    {
        var axis = new Axis
        {
            start = new int[dstSize],
            count = new int[dstSize],
            weights = new double[dstSize][]
        };
        for(var d = 0; d < dstSize; d++)
        {
            var a = (dstMin + d) * inv - srcMin;
            var b = (dstMin + d + 1) * inv - srcMin;
            var s0 = Mathf.Max(0, FloorEps(a));
            var s1 = Mathf.Min(srcSize, CeilEps(b));
            var count = Mathf.Max(0, s1 - s0);
            var w = new double[count];
            for(var i = 0; i < count; i++)
            {
                var p = s0 + i;
                w[i] = System.Math.Min(b, p + 1) - System.Math.Max(a, p);
            }
            axis.start[d] = s0;
            axis.count[d] = count;
            axis.weights[d] = w;
            if(count > axis.maxCount) axis.maxCount = count;
        }
        axis.maxCount = Mathf.Max(1, axis.maxCount);
        return axis;
    }

    static int FloorEps(double v)
    {
        return (int)System.Math.Floor(v + Eps);
    }

    static int CeilEps(double v)
    {
        return (int)System.Math.Ceiling(v - Eps);
    }
}
#endif
