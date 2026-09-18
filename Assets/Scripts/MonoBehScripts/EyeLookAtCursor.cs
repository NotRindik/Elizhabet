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

    private Camera cam;
    private AbstractEntity _entity;
    private IInputProvider _provider;

    private void Awake()
    {
        cam = Camera.main;
        _entity = GetComponent<AbstractEntity>();
    }

    private void Start()
    {
        _provider = _entity.GetControllerSystem<IInputProvider>();
    }

    private void LateUpdate()
    {
        Vector3 mouse = _provider.GetState().Point.ReadValue<Vector2>();
        mouse.z = Mathf.Abs(cam.transform.position.z - transform.position.z);
        var mouseWorld = cam.ScreenToWorldPoint(mouse);

        var t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        Look(leftEye, mouseWorld, t);
        Look(rightEye, mouseWorld, t);
    }

    private void Look(Transform eye, Vector3 mouseWorld, float t)
    {
        var socket = eye.parent;
        var local = socket.InverseTransformPoint(mouseWorld);

        var halfHeight = cam.orthographicSize;
        var halfWidth = halfHeight * cam.aspect;
        var normalized = new Vector2(local.x / halfWidth, local.y / halfHeight);
        normalized = Vector2.ClampMagnitude(normalized, 1f);

        var offset = new Vector3(Mathf.Lerp(minOffset.x, maxOffset.x, normalized.x * 0.5f + 0.5f), Mathf.Lerp(minOffset.y, maxOffset.y, normalized.y * 0.5f + 0.5f), 0f);
        eye.localPosition = Vector3.Lerp(eye.localPosition, offset, t);
    }
}