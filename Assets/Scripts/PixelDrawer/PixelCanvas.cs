using UnityEngine;

[DisallowMultipleComponent]
public class PixelCanvas : MonoBehaviour
{
    [Min(1)] public float pixelsPerUnit = 16;

    [Header("Canvas limit")]
    [Tooltip("Canvas limit: max width and height (in pixels) of one layer sprite. The layer grows automatically up to this size. " +
             "Need more? Raise this value manually (the real cap is the GPU max texture size), " +
             "or create another layer/canvas that continues the old one by hand.")]
    [Min(16)] public int maxSize = 4096;

    [Tooltip("The layer grows in steps of this many pixels (aligned to a grid), so resizing is rare.")]
    [Min(1)] public int growStep = 64;

    public string assetFolder;

    public int EffectiveMaxSize
    {
        get { return Mathf.Min(maxSize, SystemInfo.maxTextureSize); }
    }
}
