#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PixelArtOps
{
    public static void SetCanvasFrame(PixelCanvas canvas, int index, bool commitFloating)
    {
        if(canvas == null) return;
        if(commitFloating) PixelFloating.CommitIfActive();
        PixelCanvasEditing.SaveAll();
        PixelArtPreview.Stop(canvas);
        canvas.ShowFrame(index, true);
        EditorUtility.SetDirty(canvas);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        SceneView.RepaintAll();
    }

    public static void StepFrame(PixelCanvas canvas, int dir)
    {
        if(canvas == null || canvas.FrameCount < 2) return;
        var n = canvas.FrameCount;
        SetCanvasFrame(canvas, ((canvas.frame + dir) % n + n) % n, true);
    }

    public static void AddFrame(PixelCanvas canvas, bool copy)
    {
        if(canvas == null) return;
        Flush();
        var arts = Arts(canvas);
        var count = canvas.FrameCount;
        var current = Mathf.Clamp(canvas.frame, 0, count - 1);
        var index = current + 1;
        for(var i = 0; i < arts.Count; i++)
        {
            var art = arts[i];
            AssetPixelStore.Materialize(art, count);
            var f = copy ? AssetPixelStore.Link(art.frames[current]) : new PixelArtAsset.Frame();
            art.frames.Insert(index, f);
            Save(art);
        }
        canvas.frameDurations.Insert(index, canvas.FrameDuration(current));
        SetCanvasFrame(canvas, index, true);
    }

    public static void DeleteFrame(PixelCanvas canvas)
    {
        if(canvas == null) return;
        if(canvas.FrameCount < 2)
        {
            EditorUtility.DisplayDialog("Pixel Art", "A canvas needs at least one frame. Clear the pixels instead.", "OK");
            return;
        }
        var index = Mathf.Clamp(canvas.frame, 0, canvas.FrameCount - 1);
        if(!EditorUtility.DisplayDialog("Pixel Art", "Delete frame " + (index + 1) + " of " + canvas.FrameCount + " on ALL layers?\n\nThis cannot be undone.", "Delete", "Cancel")) return;
        Flush();
        var arts = Arts(canvas);
        var count = canvas.FrameCount;
        for(var i = 0; i < arts.Count; i++)
        {
            var art = arts[i];
            AssetPixelStore.Materialize(art, count);
            AssetPixelStore.ReleaseFrame(art, art.frames[index]);
            art.frames.RemoveAt(index);
            Save(art);
        }
        canvas.frameDurations.RemoveAt(index);
        SetCanvasFrame(canvas, Mathf.Min(index, canvas.FrameCount - 1), true);
    }

    public static void MoveFrame(PixelCanvas canvas, int dir)
    {
        if(canvas == null) return;
        var index = canvas.frame;
        var target = index + dir;
        if(index < 0 || target < 0 || target >= canvas.FrameCount) return;
        Flush();
        var arts = Arts(canvas);
        var count = canvas.FrameCount;
        for(var i = 0; i < arts.Count; i++)
        {
            var art = arts[i];
            AssetPixelStore.Materialize(art, count);
            var f = art.frames[index];
            art.frames.RemoveAt(index);
            art.frames.Insert(target, f);
            Save(art);
        }
        var d = canvas.frameDurations[index];
        canvas.frameDurations.RemoveAt(index);
        canvas.frameDurations.Insert(target, d);
        SetCanvasFrame(canvas, target, true);
    }

    public static int MaxLayerFrames(PixelCanvas canvas)
    {
        var arts = Arts(canvas);
        var max = 0;
        for(var i = 0; i < arts.Count; i++) max = Mathf.Max(max, arts[i].frames.Count);
        return max;
    }

    public static void AdoptFromLayers(PixelCanvas canvas)
    {
        if(canvas == null) return;
        Flush();
        var arts = Arts(canvas);
        PixelArtAsset best = null;
        for(var i = 0; i < arts.Count; i++)
            if(best == null || arts[i].frames.Count > best.frames.Count) best = arts[i];
        if(best == null || best.frames.Count <= canvas.FrameCount) return;
        canvas.frameDurations.Clear();
        for(var i = 0; i < best.frames.Count; i++) canvas.frameDurations.Add(Mathf.Max(0.01f, best.frames[i].duration));
        canvas.loop = best.loop;
        var count = canvas.FrameCount;
        for(var i = 0; i < arts.Count; i++)
        {
            AssetPixelStore.Materialize(arts[i], count);
            Save(arts[i]);
        }
        SetCanvasFrame(canvas, Mathf.Min(canvas.frame, count - 1), true);
    }

    public static bool ConvertLayer(PixelLayer layer)
    {
        if(layer == null || layer.art != null || layer.Root == null) return false;
        Flush();
        var linked = PixelLayerOps.LinkedLayers(layer);
        if(linked.Count == 0) linked.Add(layer);
        Vector2Int origin;
        int w, h;
        var pixels = PixelCanvasEditing.ReadPixels(layer, out origin, out w, out h);
        var data = new PixelData();
        data.width = w;
        data.height = h;
        data.origin = origin;
        data.pixels = pixels != null ? (Color32[])pixels.Clone() : new Color32[0];
        PixelCanvasEditing.SaveAll();

        var canvas = layer.Root;
        var art = AssetPixelStore.Create(layer, linked, canvas);
        if(art == null) return false;
        AssetPixelStore.Write(art, 0, data, canvas.pixelsPerUnit);
        for(var i = 0; i < linked.Count; i++)
        {
            if(linked[i] == null) continue;
            linked[i].ShowFrame(0);
            EditorUtility.SetDirty(linked[i]);
            EditorSceneManager.MarkSceneDirty(linked[i].gameObject.scene);
        }
        SceneView.RepaintAll();
        return true;
    }

    public static int ConvertCanvas(PixelCanvas canvas)
    {
        if(canvas == null) return 0;
        var layers = canvas.GetComponentsInChildren<PixelLayer>(true);
        var count = 0;
        for(var i = 0; i < layers.Length; i++)
        {
            if(layers[i].art != null) continue;
            if(ConvertLayer(layers[i])) count++;
        }
        Debug.Log("PixelCanvas: converted " + count + " layer(s) of '" + canvas.name + "' to Pixel Art assets. The old PNG files are still in the project; use 'Clean unused PNGs' when you are happy.");
        return count;
    }

    public static void ExportPngs(PixelLayer layer, bool all)
    {
        var art = layer != null ? layer.art : null;
        if(art == null) return;
        Flush();
        var assetPath = AssetDatabase.GetAssetPath(art);
        var folder = Path.GetDirectoryName(assetPath).Replace('\\', '/') + "_Export";
        PixelDrawerAssets.EnsureFolder(folder);
        var name = Path.GetFileNameWithoutExtension(assetPath);
        var from = all ? 0 : Mathf.Clamp(layer.frame, 0, art.frames.Count - 1);
        var to = all ? art.frames.Count - 1 : from;
        var written = 0;
        for(var i = from; i <= to; i++)
        {
            var f = art.frames[i];
            if(f.color == null) continue;
            var tex = new Texture2D(f.color.width, f.color.height, TextureFormat.RGBA32, false);
            tex.SetPixels32(f.color.GetPixels32());
            tex.Apply(false);
            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            var path = folder + "/" + name + "_" + i + ".png";
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            written++;
        }
        Debug.Log("PixelCanvas: exported " + written + " PNG file(s) to " + folder);
    }

    static void Flush()
    {
        PixelFloating.CommitIfActive();
        PixelCanvasEditing.SaveAll();
    }

    static void Save(PixelArtAsset art)
    {
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssetIfDirty(art);
    }

    static List<PixelArtAsset> Arts(PixelCanvas canvas)
    {
        var arts = new List<PixelArtAsset>();
        var layers = canvas.GetComponentsInChildren<PixelLayer>(true);
        for(var i = 0; i < layers.Length; i++)
        {
            var art = layers[i].art;
            if(art != null && !arts.Contains(art)) arts.Add(art);
        }
        var all = PixelCanvasSync.Layers();
        for(var i = 0; i < arts.Count; i++)
        {
            for(var k = 0; k < all.Count; k++)
            {
                if(all[k].art != arts[i] || all[k].Root == canvas) continue;
                Debug.LogWarning("PixelCanvas: layer '" + all[k].name + "' of another canvas uses the same Pixel Art asset as '" + canvas.name + "'. Its frames change too. Use 'Make All Layers Unique' before animating copies separately.", all[k]);
                break;
            }
        }
        return arts;
    }

    static PixelLayer SelectedLayer()
    {
        var go = Selection.activeGameObject;
        return go != null ? go.GetComponentInParent<PixelLayer>() : null;
    }

    [MenuItem("Tools/Pixel Canvas/Convert Selected Layer To Pixel Art Asset")]
    static void MenuConvertLayer()
    {
        var layer = SelectedLayer();
        if(layer == null) Debug.LogWarning("PixelCanvas: select a Pixel Layer first.");
        else ConvertLayer(layer);
    }

    [MenuItem("Tools/Pixel Canvas/Convert Selected Canvas To Pixel Art Assets")]
    static void MenuConvertCanvas()
    {
        var go = Selection.activeGameObject;
        var canvas = go != null ? go.GetComponentInParent<PixelCanvas>() : null;
        if(canvas == null) Debug.LogWarning("PixelCanvas: select a Pixel Canvas or one of its layers first.");
        else ConvertCanvas(canvas);
    }
}

[InitializeOnLoad]
public static class PixelArtPreview
{
    static readonly Dictionary<PixelCanvas, double> playing = new Dictionary<PixelCanvas, double>();
    static readonly List<PixelCanvas> buffer = new List<PixelCanvas>();
    static double last;

    static PixelArtPreview()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state => StopAll();
        EditorSceneManager.sceneSaving += (scene, path) => StopAll();
        AssemblyReloadEvents.beforeAssemblyReload += StopAll;
    }

    public static bool IsPlaying(PixelCanvas canvas)
    {
        return canvas != null && playing.ContainsKey(canvas);
    }

    public static void Toggle(PixelCanvas canvas)
    {
        if(canvas == null) return;
        if(IsPlaying(canvas))
        {
            Pause(canvas);
            return;
        }
        if(canvas.FrameCount < 2) return;
        if(playing.Count == 0) last = EditorApplication.timeSinceStartup;
        playing[canvas] = 0;
    }

    public static void Pause(PixelCanvas canvas)
    {
        double time;
        if(canvas == null || !playing.TryGetValue(canvas, out time)) return;
        var index = canvas.FrameAt((float)time);
        playing.Remove(canvas);
        PixelArtOps.SetCanvasFrame(canvas, index, false);
    }

    public static void Stop(PixelCanvas canvas)
    {
        if(canvas == null || !playing.Remove(canvas)) return;
        canvas.ShowFrame(canvas.frame, false);
        SceneView.RepaintAll();
    }

    public static void StopAll()
    {
        buffer.Clear();
        foreach(var pair in playing) buffer.Add(pair.Key);
        for(var i = 0; i < buffer.Count; i++) Stop(buffer[i]);
        playing.Clear();
    }

    static void Tick()
    {
        if(playing.Count == 0) return;
        var now = EditorApplication.timeSinceStartup;
        var dt = now - last;
        last = now;
        buffer.Clear();
        foreach(var pair in playing) buffer.Add(pair.Key);
        for(var i = 0; i < buffer.Count; i++)
        {
            var canvas = buffer[i];
            if(canvas == null)
            {
                playing.Remove(canvas);
                continue;
            }
            var time = playing[canvas] + dt * canvas.playSpeed;
            playing[canvas] = time;
            canvas.ShowFrame(canvas.FrameAt((float)time), false);
        }
        SceneView.RepaintAll();
    }
}
#endif
