using Systems;
using UnityEngine;

public class ElizhaEyeSync : MonoBehaviour
{
    public AbstractEntity entity;

    private FlyingMoveComponent _movC;

    [Header("Eye Settings")]
    public Transform eye;

    public Vector2 centerOffset = new Vector2(0.0323f, 0f);

    public Vector2 clampX = new Vector2(-0.0606f, 0.0933f);
    public Vector2 clampY = new Vector2(-0.0625f, 0.0611f);

    [Header("Tuning")]
    public float eyeStrength = 0.03f;
    public float smooth = 12f;

    private void Start()
    {
        _movC = entity.GetControllerComponent<FlyingMoveComponent>();
    }

    private void LateUpdate()
    {
        Vector2 moveDir = _movC.MoveDir;

        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        // 1. target offset от движения
        Vector2 targetOffset = moveDir * eyeStrength;

        // 2. добавляем центр
        Vector2 final = centerOffset + targetOffset;

        // 3. clamp внутри "глазницы"
        final.x = Mathf.Clamp(final.x, clampX.x, clampX.y);
        final.y = Mathf.Clamp(final.y, clampY.x, clampY.y);

        // 4. smooth
        Vector2 current = eye.localPosition;
        eye.localPosition = Vector2.Lerp(current, final, Time.deltaTime * smooth);
    }
}