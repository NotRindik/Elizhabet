#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PixelOnionSkin
{
    static readonly Dictionary<PixelCanvas, int> previewFrames = new Dictionary<PixelCanvas, int>();
    static readonly List<PixelLayer> layers = new List<PixelLayer>();
    static readonly Color before = new Color(1f, 0.25f, 0.25f, 1f);
    static readonly Color after = new Color(0.25f, 1f, 0.25f, 1f);

    private static Material _material;
    
    static Material material
    {
        get
        {
            if(_material == null)
            {
                _material = new Material(Shader.Find("Hidden/PixelDrawer/OnionSkin"));
            }
            
            return _material;
        }
    }

    static PixelOnionSkin()
    {
        SceneView.duringSceneGui += Draw;
        AssemblyReloadEvents.beforeAssemblyReload += Clear;
    }

    public static void SetFrame(PixelCanvas canvas, int frame)
    {
        if(canvas == null) return;
        previewFrames[canvas] = frame;
        SceneView.RepaintAll();
    }

    public static void ClearFrame(PixelCanvas canvas)
    {
        if(canvas == null) return;
        previewFrames.Remove(canvas);
        SceneView.RepaintAll();
    }

    public static void Clear()
    {
        previewFrames.Clear();
        SceneView.RepaintAll();
    }

    static void Draw(SceneView sceneView)
    {
        if(sceneView == null || Event.current == null || Event.current.type != EventType.Repaint) return;
        if(EditorApplication.isPlaying) return;

        var canvases = Resources.FindObjectsOfTypeAll<PixelCanvas>();
        for(var i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i];
            if(canvas == null || !canvas.onionSkin || canvas.FrameCount < 2) continue;
            if(!canvas.gameObject.scene.IsValid() || !canvas.gameObject.scene.isLoaded || EditorUtility.IsPersistent(canvas)) continue;
            if(PixelArtPreview.IsPlaying(canvas)) continue;
            int frame;
            if(!previewFrames.TryGetValue(canvas, out frame)) frame = canvas.frame;
            DrawCanvas(canvas, Mathf.Clamp(frame, 0, canvas.FrameCount - 1));
        }
    }

    static void DrawCanvas(PixelCanvas canvas, int frame)
    {
        layers.Clear();
        layers.AddRange(canvas.GetComponentsInChildren<PixelLayer>(true));
        layers.Sort((a, b) => a.sortIndex.CompareTo(b.sortIndex));

        Handles.BeginGUI();
        for(var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            if(layer == null || !layer.visible || layer.opacity <= 0 || layer.art == null) continue;
            var count = layer.art.frames.Count;
            if(count < 2) continue;

            var shown = layer.art.frames[Mathf.Min(frame, count - 1)].sprite;
            for(var d = 1; d <= canvas.onionRange; d++)
            {
                var fade = canvas.onionOpacity / d * layer.opacity;
                var back = frame - d;
                var next = frame + d;
                if(back >= 0) DrawFrame(layer, layer.art.frames[Mathf.Min(back, count - 1)], shown, before, fade);
                if(next < canvas.FrameCount) DrawFrame(layer, layer.art.frames[Mathf.Min(next, count - 1)], shown, after, fade);
            }
        }
        Handles.EndGUI();
    }

    static void DrawFrame(PixelLayer layer, PixelArtAsset.Frame frame, Sprite shown, Color tint, float alpha)
    {
        if(frame == null || frame.sprite == null || frame.sprite == shown) return;

        var sprite = frame.sprite;
        var texture = sprite.texture;
        if(texture == null) return;

        var ppu = layer.Root != null ? layer.Root.pixelsPerUnit : sprite.pixelsPerUnit;
        if(ppu <= 0f) return;

        var origin = layer.transform.TransformPoint(new Vector3(frame.origin.x / ppu, frame.origin.y / ppu, 0f));
        var size = sprite.rect.size;
        var pivot = sprite.pivot;
        var bottomLeft = origin + layer.transform.TransformVector(new Vector3(-pivot.x / ppu, -pivot.y / ppu, 0f));
        var topRight = origin + layer.transform.TransformVector(new Vector3((size.x - pivot.x) / ppu, (size.y - pivot.y) / ppu, 0f));

        var a = HandleUtility.WorldToGUIPoint(bottomLeft);
        var b = HandleUtility.WorldToGUIPoint(topRight);
        var rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        if(rect.width <= 0f || rect.height <= 0f) return;

        Rect textureRect;
        try
        {
            textureRect = sprite.textureRect;
        }
        catch
        {
            return;
        }

        var uv = new Rect(textureRect.x / texture.width, textureRect.y / texture.height, textureRect.width / texture.width, textureRect.height / texture.height);

        tint.a = alpha;
        material.SetColor("_Color", tint);

        Graphics.DrawTexture(
            rect,
            texture,
            uv,
            0, 0, 0, 0,
            material
        );
    }
}
#endif
