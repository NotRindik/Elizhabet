using UnityEngine;

public class TimeManagerWrapper : MonoBehaviour
{
    public float duration, maxduration, slowdownFactor;
    public void StartHitStop() => TimeManager.StartHitStop(duration, maxduration, slowdownFactor,this);
}
