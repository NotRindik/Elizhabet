using UnityEngine;
using UnityEngine.Serialization;

[SelectionBase, DisallowMultipleComponent]
public class PixelLayer : MonoBehaviour
{
    public string id;
    public bool visible = true;
    public bool locked;
    [Range(0, 1)] public float opacity = 1;

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
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if(this != null)
            {
                Apply();
                PlaceSurface();
            }
        };
    }
#endif
}
