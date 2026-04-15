using UnityEngine;
using Cinemachine;

public class CameraPointDynamic : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCamera virtualCamera;

    [Header("Settings")]
    [SerializeField] private Vector2 maxOffset = new Vector2(2f, 1.2f);
    [SerializeField] private float smoothSpeed = 5f;

    private CinemachineFramingTransposer _transposer;
    private Vector3 _currentOffset;
    private Vector3 _baseOffset;
    private Vector3 _velocity;
    float deadzone = 0.1f;
    private void Awake()
    {
        _transposer = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();

        _baseOffset = _transposer.m_TrackedObjectOffset;
    }

    private void LateUpdate()
    {
        Vector2 mouse = InputManager.inputActions.Player.Point.ReadValue<Vector2>();
        
        Vector2 normalized = new Vector2(
            (mouse.x / Screen.width - 0.5f) * 2f,
            (mouse.y / Screen.height - 0.5f) * 2f
        );
        
        float aspect = (float)Screen.width / Screen.height;

        normalized.x *= aspect;
        normalized = Vector2.ClampMagnitude(normalized, 1f);
        normalized.x /= aspect;
        
        if (normalized.magnitude < deadzone)
            normalized = Vector2.zero;
        
        
        Vector3 targetOffset = new Vector3(
            normalized.x * maxOffset.x,
            normalized.y * maxOffset.y,
            0f
        );

        _currentOffset = Vector3.SmoothDamp(
            _currentOffset,
            targetOffset,
            ref _velocity,
            0.2f // время сглаживания
        );

        _transposer.m_TrackedObjectOffset = _baseOffset + _currentOffset;
    }
}