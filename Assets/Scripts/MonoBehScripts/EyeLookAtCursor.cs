using System;
using Systems;
using UnityEngine;

public sealed class EyeLookAtCursor : MonoBehaviour
{
    [SerializeField] private Transform leftEye;
    [SerializeField] private Transform rightEye;

    [Header("Local Offset Range")]
    [SerializeField] private Vector2 minOffset = new(-0.08f, -0.04f);
    [SerializeField] private Vector2 maxOffset = new(0.08f, 0.04f);

    [SerializeField] private float smooth = 15f;

    private Vector3 leftStartPosition => leftEye.parent.position;
    private Vector3 rightStartPosition => rightEye.parent.position;

    private Camera cam;

    private AbstractEntity _entity;
    private IInputProvider _provider;
    private SpriteFlipComponent _spriteFlipComponent;

    private void Awake()
    {
        cam = Camera.main;

        _entity = GetComponent<AbstractEntity>();
    }

    private void Start()
    {
        _provider = _entity.GetControllerSystem<IInputProvider>();
        _spriteFlipComponent = _entity.GetControllerComponent<SpriteFlipComponent>();
    }

    private void LateUpdate()
    {

        Vector3 mouse = _provider.GetState().Point.ReadValue<Vector2>();
        mouse.z = Mathf.Abs(cam.transform.position.z - transform.position.z);

        Vector3 mouseWorld = cam.ScreenToWorldPoint(mouse);
        Vector3 direction = mouseWorld - transform.position;

        float halfWidth = cam.orthographicSize * cam.aspect;
        float halfHeight = cam.orthographicSize;

        Vector2 normalized = new Vector2(
            direction.x / halfWidth,
            direction.y / halfHeight
        );

        normalized = Vector2.ClampMagnitude(normalized, 1f);

        if (_spriteFlipComponent.IsFlip)
            normalized.x = -normalized.x;

        Vector3 offset = new Vector3(
            Mathf.Lerp(minOffset.x, maxOffset.x, normalized.x * 0.5f + 0.5f),
            Mathf.Lerp(minOffset.y, maxOffset.y, normalized.y * 0.5f + 0.5f),
            0f
        );

        Vector3 leftTarget = leftStartPosition + offset;
        Vector3 rightTarget = rightStartPosition + offset;

        leftEye.position = Vector3.Lerp(
            leftEye.position,
            leftTarget,
            smooth * Time.deltaTime
        );

        rightEye.position = Vector3.Lerp(
            rightEye.position,
            rightTarget,
            smooth * Time.deltaTime
        );
    }
}