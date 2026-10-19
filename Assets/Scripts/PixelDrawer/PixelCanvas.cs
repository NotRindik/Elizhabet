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

    public string assetFolder;

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

    [Header("Bake")]
    public SpriteRenderer baked;
    [Tooltip("In play mode and in builds the layers are hidden and the single baked sprite is shown instead (fewer draw calls).")]
    public bool useBakedInPlay;

    public int EffectiveMaxSize
    {
        get { return Mathf.Min(maxSize, SystemInfo.maxTextureSize); }
    }

    public bool TileX
    {
        get { return tileMode == TileMode.X || tileMode == TileMode.XY; }
    }

    public bool TileY
    {
        get { return tileMode == TileMode.Y || tileMode == TileMode.XY; }
    }

    void Awake()
    {
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
