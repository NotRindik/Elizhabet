using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent, RequireComponent(typeof(SortingGroup))]
public class PixelCanvas : MonoBehaviour
{
    public enum TileMode { Off, X, Y, XY }

    [HideInInspector] public string id;
    [HideInInspector] public int orderVersion;

    [Min(1)] public float pixelsPerUnit = 16;

    [Header("Canvas limit")]
    [Tooltip("Canvas limit: max width and height (in pixels) of one layer sprite. The layer grows automatically up to this size. " +
             "Need more? Raise this value manually (the real cap is the GPU max texture size), " +
             "or create another layer/canvas that continues the old one by hand.")]
    [Min(16)] public int maxSize = 4096;

    [Tooltip("The layer grows in steps of this many pixels (aligned to a grid), so resizing is rare.")]
    [Min(1)] public int growStep = 64;

    public string assetFolder = "Assets/Resources/Sprites/WorldArt";

    [Header("Tiling (seamless textures)")]
    [Tooltip("Painting wraps around the tile rectangle (0,0)-(tile size) on the chosen axes, copies of the layer are previewed around it.")]
    public TileMode tileMode = TileMode.Off;
    public Vector2Int tileSize = new Vector2Int(32, 32);

    [Header("Reference image (editor only, never saved into layers)")]
    public Texture2D reference;
    public Vector2 referenceOffset;
    [Min(0.01f)] public float referenceScale = 1;
    [Range(0, 1)] public float referenceOpacity = 0.5f;
    public bool referenceVisible = true;

    [Header("Animation (one timeline for the whole canvas, all layers follow it)")]
    public PixelArtAsset.LoopMode loop;
    [Tooltip("Plays the timeline by itself in play mode and in builds when the canvas has more than one frame.")]
    public bool autoPlay = true;
    [Min(0)] public float playSpeed = 1;
    public bool unscaledTime;
    [Tooltip("Starts at a random point so repeated decorations do not blink in sync.")]
    public bool randomStart;
    [Tooltip("Editor only: shows the previous frame in red and the next frame in green behind the current one.")]
    public bool onionSkin;
    [Range(0, 1)] public float onionOpacity = 0.35f;
    [Range(1, 3), Tooltip("How many frames before and after are shown.")]
    public int onionRange = 1;
    [HideInInspector] public int frame;
    [HideInInspector] public List<float> frameDurations = new List<float> { 0.1f };

    [Header("Bake")]
    public SpriteRenderer baked;
    [Tooltip("In play mode and in builds the layers are hidden and the single baked sprite is shown instead (fewer draw calls).")]
    public bool useBakedInPlay;

    public int EffectiveMaxSize
    {
        get { return Mathf.Min(maxSize, SystemInfo.maxTextureSize); }
    }

    public int FrameCount
    {
        get { return Mathf.Max(1, frameDurations.Count); }
    }

    public float FrameDuration(int index)
    {
        return index >= 0 && index < frameDurations.Count ? Mathf.Max(0.01f, frameDurations[index]) : 0.1f;
    }

    public float TotalDuration
    {
        get
        {
            var total = 0f;
            for(var i = 0; i < FrameCount; i++) total += FrameDuration(i);
            return total;
        }
    }

    public int FrameAt(float time)
    {
        var n = FrameCount;
        if(n <= 1) return 0;
        var total = TotalDuration;
        float t;
        if(loop == PixelArtAsset.LoopMode.Once) t = Mathf.Min(time, total - 0.0001f);
        else if(loop == PixelArtAsset.LoopMode.Loop) t = Mathf.Repeat(time, total);
        else
        {
            var m = Mathf.Repeat(time, total * 2);
            t = Mathf.Min(m < total ? m : total * 2 - m, total - 0.0001f);
        }
        if(t < 0) t = 0;
        var acc = 0f;
        for(var i = 0; i < n; i++)
        {
            acc += FrameDuration(i);
            if(t < acc) return i;
        }
        return n - 1;
    }

    public void ShowFrame(int index, bool persist)
    {
        index = Mathf.Clamp(index, 0, FrameCount - 1);
        var layers = GetComponentsInChildren<PixelLayer>(true);
        for(var i = 0; i < layers.Length; i++)
        {
            var l = layers[i];
            if(l.art == null || l.art.frames.Count == 0) continue;
            if(PixelLayer.IsBeingEdited != null && PixelLayer.IsBeingEdited(l)) continue;
            var f = Mathf.Min(index, l.art.frames.Count - 1);
            if(persist) l.frame = f;
            l.ShowFrame(f);
        }
        if(persist) frame = index;
    }

    public bool TileX
    {
        get { return tileMode == TileMode.X || tileMode == TileMode.XY; }
    }

    public bool TileY
    {
        get { return tileMode == TileMode.Y || tileMode == TileMode.XY; }
    }

    float playTime;
    int shown = -1;
    bool playing;

    public bool IsPlaying
    {
        get { return playing; }
    }

    public void Play()
    {
        playing = true;
    }

    public void Stop()
    {
        playing = false;
    }

    public void Restart()
    {
        playTime = 0;
        shown = -1;
        playing = true;
    }

    public void SetFrame(int index)
    {
        playing = false;
        shown = index;
        ShowFrame(index, false);
    }

    void Update()
    {
        if(!playing) return;
        playTime += (unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) * playSpeed;
        var index = FrameAt(playTime);
        if(index == shown) return;
        shown = index;
        ShowFrame(index, false);
    }

    void Awake()
    {
        if(Application.isPlaying)
        {
            playing = autoPlay && FrameCount > 1 && !(useBakedInPlay && baked != null);
            if(playing && randomStart) playTime = Random.value * TotalDuration;
        }
        if(!Application.isPlaying || !useBakedInPlay || baked == null) return;
        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for(var i = 0; i < renderers.Length; i++) renderers[i].enabled = false;
        baked.gameObject.SetActive(true);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        PixelCanvasSync.Request();
    }
#endif
}
