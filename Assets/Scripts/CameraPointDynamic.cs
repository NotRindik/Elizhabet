using Cinemachine;
using UnityEngine;

[ExecuteAlways]
[SaveDuringPlay]
[AddComponentMenu("")]
public class CameraPointExtension : MonoBehaviour, ICameraExtension
{
    [Header("Settings")]
    [SerializeField] private Vector2 maxOffset = new(2f, 1.2f);
    [SerializeField] private float smoothTime = 0.2f;
    [SerializeField] private float deadzone = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color radiusColor = Color.cyan;
    [SerializeField] private Color currentOffsetColor = Color.green;
    [SerializeField] private Color deadzoneColor = Color.yellow;

    private Vector3 _currentOffset;
    private Vector3 _velocity;
    private Vector3 _debugCameraPosition;
    private Vector2 _debugNormalized;

    private float _cachedAspect;
    private float _cachedInvWidth;
    private float _cachedInvHeight;
    private int _cachedScreenWidth;
    private int _cachedScreenHeight;

    public int priority;
    public int Priority => priority;

    private void RefreshScreenCache()
    {
        int w = Screen.width;
        int h = Screen.height;
        if (w == _cachedScreenWidth && h == _cachedScreenHeight) return;

        _cachedScreenWidth  = w;
        _cachedScreenHeight = h;
        _cachedInvWidth     = 1f / w;
        _cachedInvHeight    = 1f / h;
        _cachedAspect       = (float)w / h;
    }

    public void Execute(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body) return;
        if (InputManager.inputActions == null) return;
        if(vcam.Follow == null) return;
        if(!vcam.Follow.gameObject.activeInHierarchy) return;

        RefreshScreenCache();

        Vector2 mouse = InputManager.inputActions.Player.Point.ReadValue<Vector2>();
        Vector2 normalized = GetNormalizedMouse(mouse);

        _debugNormalized = normalized;

        Vector3 targetOffset = new Vector3(normalized.x * maxOffset.x, normalized.y * maxOffset.y, 0f);

        _currentOffset = Vector3.SmoothDamp(_currentOffset, targetOffset, ref _velocity, smoothTime, Mathf.Infinity, deltaTime);

        state.RawPosition += _currentOffset;

        _debugCameraPosition = state.RawPosition - _currentOffset;
    }

    private Vector2 GetNormalizedMouse(Vector2 mouse)
    {
        Vector2 normalized = new Vector2(
            (mouse.x * _cachedInvWidth  - 0.5f) * 2f,
            (mouse.y * _cachedInvHeight - 0.5f) * 2f);

        normalized.x *= _cachedAspect;
        normalized = Vector2.ClampMagnitude(normalized, 1f);
        normalized.x /= _cachedAspect;

        if (normalized.magnitude < deadzone) return Vector2.zero;

        return normalized;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Vector3 center = _debugCameraPosition;
        DrawRadius(center);
        DrawDeadzone(center);
        DrawCurrentOffset(center);
    }

    private void DrawRadius(Vector3 center)
    {
        Gizmos.color = radiusColor;
        Gizmos.DrawWireCube(center, new Vector3(maxOffset.x * 2f, maxOffset.y * 2f, 0f));
    }

    private void DrawDeadzone(Vector3 center)
    {
        Gizmos.color = deadzoneColor;
        Gizmos.DrawWireCube(center, new Vector3(maxOffset.x * deadzone * 2f, maxOffset.y * deadzone * 2f, 0f));
    }

    private void DrawCurrentOffset(Vector3 center)
    {
        Gizmos.color = currentOffsetColor;
        Vector3 current = center + _currentOffset;
        Gizmos.DrawLine(center, current);
        Gizmos.DrawSphere(current, 0.15f);
    }
#endif
}