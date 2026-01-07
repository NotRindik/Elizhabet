using UnityEngine;

public class CameraShakeWtapper : MonoBehaviour
{
    public ShakeData shakeData;
    public float duration, delay;
    public void Shake() => PlayerCamShake.Instance.Shake(shakeData,duration,delay);
}
