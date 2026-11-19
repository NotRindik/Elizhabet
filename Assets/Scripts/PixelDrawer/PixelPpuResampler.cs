#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public enum PixelResampleMode
{
    Blocky,
    Smooth,
    PixelTrue
}

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

    public static bool Resample(PixelCanvas canvas, float from, float to, PixelResampleMode mode = PixelResampleMode.Blocky)
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
        var message = "Resample PPU " + from + " → " + to + " (x" + scale.ToString("0.###") + "), mode: " + mode + ".\n\n" +
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
                ResampleArt(arts[i], scale, to, mode);
            }
            var png = new PngPixelStore();
            for(var i = 0; i < pngs.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Pixel Canvas", "Resampling " + pngs[i], (float)done++ / total);
                ResamplePng(png, pngs[i], layers, scale, mode);
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
        Debug.Log("PixelCanvas: resampled " + canvases.Count + " canvas(es) from PPU " + from + " to " + to + " (" + mode + ").");
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

    static void ResampleArt(PixelArtAsset art, double scale, float to, PixelResampleMode mode)
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
            datas[i] = Resample(pixels, emission, w, h, f.origin, scale, mode);
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

    static void ResamplePng(PngPixelStore store, string path, List<PixelLayer> layers, double scale, PixelResampleMode mode)
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
        var result = Resample(data.pixels, null, data.width, data.height, data.origin, scale, mode);
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

    public static PixelData Resample(Color32[] src, byte[] emission, int w, int h, Vector2Int origin, double scale, PixelResampleMode mode = PixelResampleMode.Blocky)
    {
        if(mode == PixelResampleMode.Blocky || w == 0 || h == 0)
            return ResampleArea(src, emission, w, h, origin, scale, null);

        if(scale < 1 + Eps)
            return ResampleArea(src, emission, w, h, origin, scale, Importance(src, w, h, 1.0 / scale));

        var steps = UpSteps(scale);
        var k = 1;
        for(var i = 0; i < steps.Count; i++) k *= steps[i];

        if((long)w * k * h * k > MaxUpscalePixels)
        {
            Debug.LogWarning("PixelCanvas: layer is too large for " + mode + " resampling, falling back to Blocky.");
            return ResampleArea(src, emission, w, h, origin, scale, null);
        }

        var keys = new uint[src.Length];
        for(var i = 0; i < src.Length; i++) keys[i] = Key(src[i]);

        var idx = new int[w * h];
        for(var i = 0; i < idx.Length; i++) idx[i] = i;
        var cw = w;
        var ch = h;
        for(var i = 0; i < steps.Count; i++)
        {
            idx = steps[i] == 2 ? Scale2x(idx, cw, ch, keys) : Scale3x(idx, cw, ch, keys);
            cw *= steps[i];
            ch *= steps[i];
        }

        if(mode == PixelResampleMode.PixelTrue)
            ThinLines(idx, cw, ch, k, keys, w, h);

        var up = new Color32[idx.Length];
        var upGlow = emission != null ? new byte[idx.Length] : null;
        for(var i = 0; i < idx.Length; i++)
        {
            var s = idx[i];
            if(s < 0) continue;
            up[i] = src[s];
            if(upGlow != null) upGlow[i] = emission[s];
        }

        var rest = scale / k;
        return ResampleArea(up, upGlow, cw, ch, origin * k, rest, Importance(up, cw, ch, 1.0 / rest));
    }

    const long MaxUpscalePixels = 64L * 1024 * 1024;

    static uint Key(Color32 c)
    {
        return c.a == 0 ? 0u : (uint)(c.r << 24 | c.g << 16 | c.b << 8 | c.a);
    }

    static List<int> UpSteps(double scale)
    {
        var best = int.MaxValue;
        int bestA = 1, bestB = 0;
        for(var a = 0; a <= 8; a++)
        {
            for(var b = 0; b <= 5; b++)
            {
                var p = (int)(System.Math.Pow(2, a) * System.Math.Pow(3, b));
                if(p < scale - Eps || p >= best) continue;
                best = p;
                bestA = a;
                bestB = b;
            }
        }
        var steps = new List<int>();
        for(var i = 0; i < bestB; i++) steps.Add(3);
        for(var i = 0; i < bestA; i++) steps.Add(2);
        return steps;
    }

    static int At(int[] idx, int w, int h, int x, int y)
    {
        return x < 0 || y < 0 || x >= w || y >= h ? -1 : idx[y * w + x];
    }

    static uint KeyOf(int i, uint[] keys)
    {
        return i < 0 ? 0u : keys[i];
    }

    static int[] Scale2x(int[] idx, int w, int h, uint[] keys)
    {
        var ow = w * 2;
        var dst = new int[ow * h * 2];
        for(var y = 0; y < h; y++)
        {
            for(var x = 0; x < w; x++)
            {
                var p = idx[y * w + x];
                var a = At(idx, w, h, x, y + 1);
                var b = At(idx, w, h, x + 1, y);
                var c = At(idx, w, h, x - 1, y);
                var d = At(idx, w, h, x, y - 1);
                uint ka = KeyOf(a, keys), kb = KeyOf(b, keys), kc = KeyOf(c, keys), kd = KeyOf(d, keys);

                var e0 = kc == ka && kc != kd && ka != kb ? a : p;
                var e1 = ka == kb && ka != kc && kb != kd ? b : p;
                var e2 = kd == kc && kd != kb && kc != ka ? c : p;
                var e3 = kb == kd && kb != ka && kd != kc ? d : p;

                var top = (y * 2 + 1) * ow + x * 2;
                var bottom = y * 2 * ow + x * 2;
                dst[top] = e0;
                dst[top + 1] = e1;
                dst[bottom] = e2;
                dst[bottom + 1] = e3;
            }
        }
        return dst;
    }

    static int[] Scale3x(int[] idx, int w, int h, uint[] keys)
    {
        var ow = w * 3;
        var dst = new int[ow * h * 3];
        for(var y = 0; y < h; y++)
        {
            for(var x = 0; x < w; x++)
            {
                var e = idx[y * w + x];
                var a = At(idx, w, h, x - 1, y + 1);
                var b = At(idx, w, h, x, y + 1);
                var c = At(idx, w, h, x + 1, y + 1);
                var d = At(idx, w, h, x - 1, y);
                var f = At(idx, w, h, x + 1, y);
                var g = At(idx, w, h, x - 1, y - 1);
                var hh = At(idx, w, h, x, y - 1);
                var i = At(idx, w, h, x + 1, y - 1);
                uint ka = KeyOf(a, keys), kb = KeyOf(b, keys), kc = KeyOf(c, keys), kd = KeyOf(d, keys), ke = KeyOf(e, keys);
                uint kf = KeyOf(f, keys), kg = KeyOf(g, keys), kh = KeyOf(hh, keys), ki = KeyOf(i, keys);

                int e0 = e, e1 = e, e2 = e, e3 = e, e5 = e, e6 = e, e7 = e, e8 = e;
                if(kb != kh && kd != kf)
                {
                    e0 = kd == kb ? d : e;
                    e1 = (kd == kb && ke != kc) || (kb == kf && ke != ka) ? b : e;
                    e2 = kb == kf ? f : e;
                    e3 = (kd == kb && ke != kg) || (kd == kh && ke != ka) ? d : e;
                    e5 = (kb == kf && ke != ki) || (kh == kf && ke != kc) ? f : e;
                    e6 = kd == kh ? d : e;
                    e7 = (kd == kh && ke != ki) || (kh == kf && ke != kg) ? hh : e;
                    e8 = kh == kf ? f : e;
                }

                var r0 = (y * 3 + 2) * ow + x * 3;
                var r1 = (y * 3 + 1) * ow + x * 3;
                var r2 = y * 3 * ow + x * 3;
                dst[r0] = e0; dst[r0 + 1] = e1; dst[r0 + 2] = e2;
                dst[r1] = e3; dst[r1 + 1] = e;  dst[r1 + 2] = e5;
                dst[r2] = e6; dst[r2 + 1] = e7; dst[r2 + 2] = e8;
            }
        }
        return dst;
    }

    static readonly int[] Nx = { 0, 1, 1, 1, 0, -1, -1, -1 };
    static readonly int[] Ny = { 1, 1, 0, -1, -1, -1, 0, 1 };

    static bool IsThin(uint[] keys, int w, int h, int x, int y)
    {
        bool thinH, thinV;
        return IsThin(keys, w, h, x, y, out thinH, out thinV);
    }

    static bool IsThin(uint[] keys, int w, int h, int x, int y, out bool thinH, out bool thinV)
    {
        thinH = thinV = false;
        var c = keys[y * w + x];
        if(c == 0) return false;
        var l = x > 0 ? keys[y * w + x - 1] : 0u;
        var r = x < w - 1 ? keys[y * w + x + 1] : 0u;
        var d = y > 0 ? keys[(y - 1) * w + x] : 0u;
        var u = y < h - 1 ? keys[(y + 1) * w + x] : 0u;
        thinH = l != c && r != c;
        thinV = d != c && u != c;
        return thinH || thinV;
    }

    static void ThinLines(int[] idx, int cw, int ch, int k, uint[] keys, int w, int h)
    {
        var line = new bool[w * h];
        var outline = new bool[w * h];
        var thinX = new bool[w * h];
        var thinY = new bool[w * h];
        var other = new bool[w * h];
        for(var y = 0; y < h; y++)
        {
            for(var x = 0; x < w; x++)
            {
                var p = y * w + x;
                var c = keys[p];
                if(c == 0) continue;
                var nearClear = false;
                for(var n = 0; n < 8; n++)
                {
                    var qx = x + Nx[n];
                    var qy = y + Ny[n];
                    var q = qx < 0 || qy < 0 || qx >= w || qy >= h ? 0u : keys[qy * w + qx];
                    if(q == 0) nearClear |= n % 2 == 0;
                    else if(q != c) other[p] = true;
                }
                outline[p] = nearClear && other[p];
                IsThin(keys, w, h, x, y, out thinX[p], out thinY[p]);
            }
        }

        var rawX = (bool[])thinX.Clone();
        var rawY = (bool[])thinY.Clone();
        for(var y = 0; y < h; y++)
        {
            for(var x = 0; x < w; x++)
            {
                var p = y * w + x;
                var c = keys[p];
                if(rawY[p])
                {
                    if(x > 0 && keys[p - 1] == c && !rawY[p - 1]) thinY[p] = false;
                    if(x < w - 1 && keys[p + 1] == c && !rawY[p + 1]) thinY[p] = false;
                }
                if(rawX[p])
                {
                    if(y > 0 && keys[p - w] == c && !rawX[p - w]) thinX[p] = false;
                    if(y < h - 1 && keys[p + w] == c && !rawX[p + w]) thinX[p] = false;
                }
                line[p] = outline[p] || (other[p] && (thinX[p] || thinY[p]));
            }
        }

        var mask = new bool[idx.Length];
        var anchor = new bool[idx.Length];
        var any = false;
        for(var uy = 0; uy < ch; uy++)
        {
            for(var ux = 0; ux < cw; ux++)
            {
                var u = uy * cw + ux;
                var p = uy / k * w + ux / k;
                if(!line[p] || idx[u] != p) continue;
                mask[u] = true;
                any = true;
            }
        }
        if(!any) return;

        for(var uy = 0; uy < ch; uy++)
        {
            for(var ux = 0; ux < cw; ux++)
            {
                var u = uy * cw + ux;
                if(!mask[u]) continue;
                var p = uy / k * w + ux / k;
                if(outline[p])
                {
                    for(var n = 0; n < 8; n += 2)
                    {
                        if(KeyOf(At(idx, cw, ch, ux + Nx[n], uy + Ny[n]), keys) != 0) continue;
                        anchor[u] = true;
                        break;
                    }
                    continue;
                }
                var leftOpen = ux == 0 || !mask[u - 1];
                var bottomOpen = uy == 0 || !mask[u - cw];
                if(thinX[p] && thinY[p]) anchor[u] = leftOpen && bottomOpen;
                else if(thinX[p]) anchor[u] = leftOpen;
                else if(thinY[p]) anchor[u] = bottomOpen;
            }
        }

        var original = (bool[])mask.Clone();
        ZhangSuen(mask, anchor, cw, ch);

        var filled = new bool[idx.Length];
        var queue = new Queue<int>();
        for(var uy = 0; uy < ch; uy++)
        {
            for(var ux = 0; ux < cw; ux++)
            {
                var u = uy * cw + ux;
                if(!original[u] || mask[u]) continue;
                for(var n = 0; n < 8; n += 2)
                {
                    var vx = ux + Nx[n];
                    var vy = uy + Ny[n];
                    if(vx < 0 || vy < 0 || vx >= cw || vy >= ch) continue;
                    var v = vy * cw + vx;
                    if(original[v] || KeyOf(idx[v], keys) == 0) continue;
                    idx[u] = idx[v];
                    filled[u] = true;
                    queue.Enqueue(u);
                    break;
                }
            }
        }

        while(queue.Count > 0)
        {
            var u = queue.Dequeue();
            var ux = u % cw;
            var uy = u / cw;
            for(var n = 0; n < 8; n += 2)
            {
                var vx = ux + Nx[n];
                var vy = uy + Ny[n];
                if(vx < 0 || vy < 0 || vx >= cw || vy >= ch) continue;
                var v = vy * cw + vx;
                if(filled[v] || !original[v] || mask[v]) continue;
                idx[v] = idx[u];
                filled[v] = true;
                queue.Enqueue(v);
            }
        }
    }

    static void ZhangSuen(bool[] m, bool[] anchor, int w, int h)
    {
        var remove = new List<int>();
        var p = new bool[8];
        var changed = true;
        while(changed)
        {
            changed = false;
            for(var step = 0; step < 2; step++)
            {
                remove.Clear();
                for(var y = 0; y < h; y++)
                {
                    for(var x = 0; x < w; x++)
                    {
                        var i = y * w + x;
                        if(!m[i] || anchor[i]) continue;
                        for(var n = 0; n < 8; n++)
                        {
                            var qx = x + Nx[n];
                            var qy = y + Ny[n];
                            p[n] = qx >= 0 && qy >= 0 && qx < w && qy < h && m[qy * w + qx];
                        }
                        var b = 0;
                        var a = 0;
                        for(var n = 0; n < 8; n++)
                        {
                            if(p[n]) b++;
                            if(!p[n] && p[(n + 1) % 8]) a++;
                        }
                        if(b < 2 || b > 6 || a != 1) continue;
                        bool up = p[0], right = p[2], down = p[4], left = p[6];
                        if(step == 0 ? (up && right && down) || (right && down && left)
                                     : (up && right && left) || (up && down && left))
                            continue;
                        remove.Add(i);
                    }
                }
                for(var r = 0; r < remove.Count; r++) m[remove[r]] = false;
                if(remove.Count > 0) changed = true;
            }
        }
    }

    static double[] Importance(Color32[] img, int w, int h, double cell)
    {
        var keys = new uint[img.Length];
        for(var i = 0; i < img.Length; i++) keys[i] = Key(img[i]);
        var boost = System.Math.Max(1.25, 2 * cell - 0.75);
        var result = new double[img.Length];
        for(var y = 0; y < h; y++)
            for(var x = 0; x < w; x++)
                result[y * w + x] = IsThin(keys, w, h, x, y) ? boost : 1.0;
        return result;
    }

    static PixelData ResampleArea(Color32[] src, byte[] emission, int w, int h, Vector2Int origin, double scale, double[] importance)
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
                        if(importance != null) weight *= importance[index];
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
