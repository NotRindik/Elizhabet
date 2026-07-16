using System;
using Controllers;
using Systems;
using UnityEngine;

public class HitStopManager : MonoBehaviour
{
    private void OnEnable()
    {
        EventBus.OnDamageApplied += OnDamagedApplied;
    }
    private void OnDisable()
    {
        EventBus.OnDamageApplied -= OnDamagedApplied;
    }
    
    public void OnDamagedApplied(HitInfo hitInfo)
    {
        bool isPlayer = hitInfo.Target is PlayerController;

        if (isPlayer)
        {
            TimeManager.StartHitStop(0.3f,0.1f);
        }
    }
}
