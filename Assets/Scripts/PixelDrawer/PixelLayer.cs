using UnityEngine;
using UnityEngine.Serialization;

[SelectionBase, DisallowMultipleComponent]
public class PixelLayer : MonoBehaviour
{
    static int emissionTexId = -1;
    static int emissionIntensityId;
    static MaterialPropertyBlock block;

    public static System.Func<PixelLayer, bool> IsBeingEdited;

    public string id;
    public bool visible = true;
    public bool locked;
    [Range(0, 1)] public float opacity = 1;
    [Min(0), Tooltip("Brightness multiplier for glowing pixels (0 = no glow). Needs a Pixel Art asset layer.")]
    public float emissionIntensity = 1;

    [HideInInspector] public PixelArtAsset art;
    [HideInInspector] public int frame;

    [HideInInspector] public int sortIndex;
    [HideInInspector, FormerlySerializedAs("sortingOrder")] public int legacySortingOrder;
    [HideInInspector, FormerlySerializedAs("sortingLayerName")] public string legacySortingLayer = "Default";

    [Tooltip("Position (in layer pixels) of the sprite's bottom-left corner. Changes automatically when the layer grows or is trimmed.")]
    public Vector2Int origin;

    PixelCanvas root;

    public PixelCanvas Root
    {
        get
        {
            if(root == null) root = GetComponentInParent<PixelCanvas>();
            return root;
        }
    }

    public PixelArtAsset.Frame CurrentFrame
    {
        get
        {
            if(art == null || art.frames.Count == 0) return null;
            return art.frames[Mathf.Clamp(frame, 0, art.frames.Count - 1)];
        }
    }

    public SpriteRenderer Surface(bool create)
    {
        var surface = GetComponentInChildren<PixelSurface>(true);
        if(surface != null) return surface.GetComponent<SpriteRenderer>();
        if(!create) return null;
        var go = new GameObject("Sprite", typeof(SpriteRenderer), typeof(PixelSurface));
        go.hideFlags = HideFlags.HideInHierarchy;
        go.transform.SetParent(transform, false);
        var sr = go.GetComponent<SpriteRenderer>();
        Apply();
        PlaceSurface();
        return sr;
    }

    public void PlaceSurface()
    {
        var sr = Surface(false);
        if(sr == null || Root == null) return;
        sr.transform.localPosition = new Vector3(origin.x, origin.y) / Root.pixelsPerUnit;
    }

    public void Present()
    {
        ShowFrame(frame);
    }

    public void ShowFrame(int index)
    {
        if(art == null || art.frames.Count == 0) return;
        var f = art.frames[Mathf.Clamp(index, 0, art.frames.Count - 1)];
        var sr = Surface(true);
        sr.sprite = f.sprite;
        origin = f.sprite != null ? f.origin : Vector2Int.zero;
        PlaceSurface();
        BindEmission(f.emission, art.material);
    }

    public void BindEmission(Texture2D emission, Material material)
    {
        var sr = Surface(false);
        if(sr == null) return;
        if(material == null && emission == null)
        {
            if(sr.HasPropertyBlock()) sr.SetPropertyBlock(null);
            return;
        }
        if(emissionTexId < 0)
        {
            emissionTexId = Shader.PropertyToID("_EmissionTex");
            emissionIntensityId = Shader.PropertyToID("_EmissionIntensity");
        }
        if(material != null && sr.sharedMaterial != material) sr.sharedMaterial = material;
        if(block == null) block = new MaterialPropertyBlock();
        sr.GetPropertyBlock(block);
        block.SetTexture(emissionTexId, emission != null ? emission : Texture2D.blackTexture);
        block.SetFloat(emissionIntensityId, emissionIntensity);
        sr.SetPropertyBlock(block);
    }

    public void Apply()
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
        var color = new Color(1, 1, 1, opacity);
        for(var i = 0; i < renderers.Length; i++)
        {
            var sr = renderers[i];
            if(sr.GetComponent<PixelTempMarker>() != null) continue;
            sr.enabled = visible;
            sr.color = color;
            sr.sortingOrder = sortIndex;
            if(!sr.HasPropertyBlock()) continue;
            if(block == null) block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);
            if(emissionTexId >= 0) block.SetFloat(emissionIntensityId, emissionIntensity);
            sr.SetPropertyBlock(block);
        }
    }

    void OnEnable()
    {
        if(Application.isPlaying) Present();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if(this == null) return;
            Apply();
            PlaceSurface();
            if(art != null && (IsBeingEdited == null || !IsBeingEdited(this))) Present();
        };
    }
#endif
}
