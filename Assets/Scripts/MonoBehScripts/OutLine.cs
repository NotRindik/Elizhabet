using UnityEngine;

public class OutLine : MonoBehaviour
{
    [SerializeField] SpriteRenderer sr;
    [SerializeField] Material outlineMat; // один общий материал с PixelOutline
    [SerializeField] Color color = Color.white;
    [SerializeField] float thickness = 1f;

    static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int ThicknessId = Shader.PropertyToID("_OutlineThickness");

    MaterialPropertyBlock _mpb;
    public bool isOutlineEnable;

    void Awake()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        _mpb = new MaterialPropertyBlock();
        sr.sharedMaterial = outlineMat;
        Apply(0f);
    }

    public void Enable()  { isOutlineEnable = true;  Apply(thickness); }
    public void Disable() { isOutlineEnable = false; Apply(0f); }

    void Apply(float t)
    {
        sr.GetPropertyBlock(_mpb);
        _mpb.SetColor(ColorId, color);
        _mpb.SetFloat(ThicknessId, t);
        sr.SetPropertyBlock(_mpb);
    }
}