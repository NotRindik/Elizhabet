using System.Collections;
using Controllers;
using Systems;
using UnityEngine;

namespace DefaultNamespace
{
    public class CameraShakeManager : MonoBehaviour
    {
        private void OnEnable()  => EventBus.OnDamageApplied += Shake;
        private void OnDisable() => EventBus.OnDamageApplied -= Shake;
        
        
        private void Shake(HitInfo hitInfo)
        {
            bool isPlayer = hitInfo.Target is PlayerController;

            if (isPlayer)
            {
                PlayerCamShake.Instance.LockedShake(new ShakeData(){amplitude = 4,frequency = 1f},0.3f);
            }
        }
    }
}