using UnityEngine;

public class HandRotatoning : MonoBehaviour
{
    public Transform shoulderPivot, elbowPivot, handTip;
    public Transform characterRoot;
    
    private IArmGrip _grip = new StraightGrip();

    public void SetGrip(IArmGrip grip)
    {
        _grip = grip;
    }
    
    public void RotateHand(Vector2 targetPos)
    {
        Vector2 shoulderPos = shoulderPivot.position;

        float len1 = Vector2.Distance(shoulderPivot.position, elbowPivot.position);
        float len2 = Vector2.Distance(elbowPivot.position, handTip.position);
        float targetDist = Vector2.Distance(shoulderPos, targetPos);

        var fullLen = len1 + len2;
        var reach = Mathf.Min(_grip.GetReach(targetDist, fullLen), fullLen - 0.001f);
        var minReach = Mathf.Min(Mathf.Abs(len1 - len2) + 0.05f * fullLen, reach);
        float clampedDist = Mathf.Clamp(targetDist, minReach, reach);

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

        var shoulderDeg = shoulderAngle * Mathf.Rad2Deg;
        var elbowDeg = elbowAngle * Mathf.Rad2Deg;
        Apply(shoulderDeg, elbowDeg);

        var forearm = (Vector2)(handTip.position - elbowPivot.position);
        var toTarget = targetPos - shoulderPos;
        var w = Mathf.Clamp01((targetDist - clampedDist) / (fullLen * 0.15f));
        shoulderDeg += Vector2.SignedAngle(forearm, toTarget) * w;
        Apply(shoulderDeg, elbowDeg);
    }
    
    private void Apply(float shoulderDeg, float elbowDeg)
    {
        shoulderPivot.rotation = Quaternion.Euler(0, 0, shoulderDeg);
        elbowPivot.rotation = shoulderPivot.rotation * Quaternion.Euler(0, 0, elbowDeg);
    }
}



public interface IArmGrip
{
    float GetReach(float targetDist, float fullLen);
}

[System.Serializable]
public class StraightGrip : IArmGrip
{
    public float GetReach(float targetDist, float fullLen)
    {
        return fullLen;
    }
}

[System.Serializable]
public class BentGrip : IArmGrip
{
    private float _bend;

    public BentGrip(float bend = 0.8f) { _bend = bend; }

    public float GetReach(float targetDist, float fullLen)
    {
        return fullLen * _bend;
    }
}

[System.Serializable]
public class HybridGrip : IArmGrip
{
    private float _bend, _start, _width;

    public HybridGrip(float bend = 0.8f, float start = 1.3f, float width = 0.3f) { _bend = bend; _start = start; _width = width; }

    public float GetReach(float targetDist, float fullLen)
    {
        var t = Mathf.InverseLerp(fullLen * _start, fullLen * (_start + _width), targetDist);
        t = t * t * (3f - 2f * t);
        return Mathf.Lerp(fullLen * _bend, fullLen, t);
    }
}