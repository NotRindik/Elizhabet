using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class CameraObstacle : MonoBehaviour
{
    [Title("General")]
    [EnumToggleButtons]
    public CameraObstacleType Type = CameraObstacleType.Hard;

    [EnumToggleButtons]
    public ObstacleDirection Direction = ObstacleDirection.Right;

    [ShowIf(nameof(HasTransition))]
    [EnumToggleButtons]
    public CameraTransitionType Transition = CameraTransitionType.Smooth;
    
    [Title("Transition")]

    [ShowIf(nameof(IsSmooth))]
    [MinValue(0f)]
    [SuffixLabel("sec")]
    public float ReleaseTime = 0.3f;

    private Collider2D _collider;

    public Collider2D Collider => _collider;

    public Vector2 Normal => Direction switch
    {
        ObstacleDirection.Right => transform.right,
        ObstacleDirection.Left => transform.right * -1,
        ObstacleDirection.Up => transform.up,
        ObstacleDirection.Down => transform.up * -1,
    };

    private bool HasTransition => Type != CameraObstacleType.Hard;

    private bool IsSmooth => Transition == CameraTransitionType.Smooth;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        gameObject.layer = LayerMask.NameToLayer("CameraObstacle");
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

public enum ObstacleDirection
{
    Right,
    Left,
    Up,
    Down
}