using UnityEngine;

public class HandRotatoning : MonoBehaviour
{
    public Transform shoulderPivot, elbowPivot, handTip;
    public Transform characterRoot;

    public void RotateHand(Vector2 targetPos)
    {
        Vector2 shoulderPos = shoulderPivot.position;

        float len1 = Vector2.Distance(shoulderPivot.position, elbowPivot.position);
        float len2 = Vector2.Distance(elbowPivot.position, handTip.position);
        float targetDist = Vector2.Distance(shoulderPos, targetPos);

        float clampedDist = Mathf.Min(targetDist, len1 + len2 - 0.001f);

        float facingSign = Mathf.Sign(characterRoot.right.x);

        float angleA = Mathf.Acos(Mathf.Clamp(
            (len1 * len1 + clampedDist * clampedDist - len2 * len2) / (2 * len1 * clampedDist),
            -1f, 1f));
        float baseAngle = Mathf.Atan2(targetPos.y - shoulderPos.y, targetPos.x - shoulderPos.x);
        float shoulderAngle = baseAngle - facingSign * angleA + 90f * Mathf.Deg2Rad;

        float angleB = Mathf.Acos(Mathf.Clamp(
            (len1 * len1 + len2 * len2 - clampedDist * clampedDist) / (2 * len1 * len2),
            -1f, 1f));
        float elbowAngle = facingSign * (Mathf.PI - angleB);

        shoulderPivot.rotation = Quaternion.Euler(0, 0, shoulderAngle * Mathf.Rad2Deg);
        elbowPivot.rotation = shoulderPivot.rotation * Quaternion.Euler(0, 0, elbowAngle * Mathf.Rad2Deg);
    }
}