using UnityEngine;

public class TimeManagerWrapper : MonoBehaviour
{
    public float duration, slowdownFactor;
    public void StartHitStop() => TimeManager.StartHitStop(duration, slowdownFactor);
}
