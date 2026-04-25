using UnityEngine;

[ExecuteAlways]
public sealed class ParallaxLayer : MonoBehaviour
{
    [SerializeField] Transform target; // ������

    [Header("Parallax Strength")]
    [Range(-1f, 1f)] public float parallaxX = 0.5f;
    [Range(-1f, 1f)] public float parallaxY = 0.5f;
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

    Vector3 targetPos;
    Vector3 lastCameraPos;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 delta = target.position - _startTargetPos;

        targetPos = _startPos;
        targetPos.x += delta.x * parallaxX;
        targetPos.y += delta.y * parallaxY;

        // мягкое сглаживание
        transform.position = targetPos + (Vector3)ofset;
    }
}
