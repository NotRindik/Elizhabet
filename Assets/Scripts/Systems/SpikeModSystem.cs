using Assets.Scripts.Systems;
using Controllers;
using UnityEngine;

namespace Systems
{
    public class SpikeModSystem : BaseModificator
    {
        private HealthComponent _healthComponent;
        private SpikeModComponent _spikeModComponent;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _healthComponent = owner.GetControllerComponent<HealthComponent>();
            _spikeModComponent = owner.GetControllerComponent<SpikeModComponent>();
            _healthComponent.OnTakeHit += SpikeDamage;
        }

        public void SpikeDamage(HitInfo hitInfo)
        {
            if(!IsActive)
                return;

            var damagerOwner = hitInfo.Attacker;
            var hp = damagerOwner.GetControllerSystem<HealthSystem>();

            new Damage(new DamageComponent(hitInfo.dmg * _spikeModComponent.damageCoeficient, 0, 0, 0, ElementType.None), damagerOwner.GetControllerComponent<ProtectionComponent>()).ApplyDamage(hp,new HitInfo(owner));
        }
    }

    public struct SpikeModComponent : IComponent
    {
        public float damageCoeficient;
    }
}