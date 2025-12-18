using UnityEngine;

[ExecuteAlways]
public sealed class ParallaxLayer : MonoBehaviour
{
    [SerializeField] Transform target; // камера

    [Header("Parallax Strength")]
    [Range(0f, 1f)] public float parallaxX = 0.5f;
    [Range(0f, 1f)] public float parallaxY = 0.5f;
     public Vector2 ofset;

    [Header("Pixel Perfect")]
    [SerializeField] float pixelsPerUnit = 32f;

    public Vector3 _startPos;
    Vector3 _startTargetPos;

    void Awake()
    {
        if (target == null && Camera.main != null)
            target = Camera.main.transform;

        if (target == null)
            return;

        _startPos = transform.position;
        _startTargetPos = target.position;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 delta = target.position - _startTargetPos;

        Vector3 pos = _startPos;
        pos.x += delta.x * parallaxX;
        pos.y += delta.y * parallaxY;

        // Pixel snap
        pos.x = Mathf.Round(pos.x * pixelsPerUnit) / pixelsPerUnit;
        pos.y = Mathf.Round(pos.y * pixelsPerUnit) / pixelsPerUnit;

        transform.position = pos + (Vector3)ofset;
    }
}
