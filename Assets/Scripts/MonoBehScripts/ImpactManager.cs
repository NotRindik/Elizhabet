using Controllers;
using Systems;
using UnityEngine;

public class ImpactManager : MonoBehaviour
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

        if(hitInfo.Attacker == null)
            return;
        
        
        if (isPlayer)
        {
            var rb = hitInfo.Target.GetControllerComponent<ControllersBaseFields>().rb;

            Vector2 dir = hitInfo.Target.transform.position - hitInfo.Attacker.transform.position;

            dir.x = Mathf.Sign(dir.x);

            dir.Normalize();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = dir * 25;
        }
    }
}

