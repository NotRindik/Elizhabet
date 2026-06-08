using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class CameraObstacle : MonoBehaviour
{
    [Title("General")]
    [EnumToggleButtons]
    public CameraObstacleType Type =
        CameraObstacleType.Hard;

    [ShowIf(nameof(HasTransition))]
    [EnumToggleButtons]
    public CameraTransitionType Transition =
        CameraTransitionType.Smooth;
    
    [Title("Transition")]

    [ShowIf(nameof(IsSmooth))]
    [MinValue(0f)]
    [SuffixLabel("sec")]
    public float ReleaseTime = 0.3f;

    private Collider2D _collider;

    public Collider2D Collider =>
        _collider;

    /// <summary>
    /// Направление препятствия
    /// transform.right = normal
    /// </summary>
    public Vector2 Normal =>
        transform.right.normalized;

    private bool HasTransition =>
        Type != CameraObstacleType.Hard;

    private bool IsSmooth =>
        Transition == CameraTransitionType.Smooth;

    private void Awake()
    {
        _collider =
            GetComponent<Collider2D>();
    }
}

public enum CameraObstacleType
{
    Hard,
    OneWay,
    TwoWay
}

public enum CameraTransitionType
{
    Smooth,
    Instant
}