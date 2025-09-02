using Controllers;
using System.Collections.Generic;
using Systems;
using UnityEngine;

public class ParticleDamage : MonoBehaviour
{
    public BaseAttackComponent attackComponent;
    private ParticleSystem ps;
    private List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>();
    private ParticleSystem.Particle[] particles;

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        particles = new ParticleSystem.Particle[ps.main.maxParticles];
    }

    void OnParticleCollision(GameObject other)
    {
        if (attackComponent == null) return;

        if (!BaseAttackComponent.IsInLayerMask(other, attackComponent.attackLayer))
            return;

        // Обработка урона
        if (other.TryGetComponent(out Controller controller))
        {
            var hp = controller.GetControllerSystem<HealthSystem>();
            if (hp != null)
            {
                new Damage(attackComponent.damage, controller.GetControllerComponent<ProtectionComponent>()).ApplyDamage(hp,Vector2.zero);
            }
        }
    }
}
