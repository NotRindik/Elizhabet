using Controllers;
using UnityEngine;

namespace Systems
{
    public class VelocityImpactComponent : IComponent
    {
        public float VelocityToDmg;
        public float bounceForce = 6f;
    }
    
    public class VelocityImpactSystem : BaseSystem, System.IDisposable
    {
        private MeleeWeaponSystem _weaponSystem;
        private MeleeComponent _meleeComponent;
        private ControllersBaseFields _baseFields;
        private VelocityImpactComponent velocityImpactComponent;
        private Item _item;

        private bool _isImpacting;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _item = (Item)owner;
            _weaponSystem = owner.GetControllerSystem<MeleeWeaponSystem>();
            _meleeComponent = owner.GetControllerComponent<MeleeComponent>();
            _baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            velocityImpactComponent = owner.GetControllerComponent<VelocityImpactComponent>();

            _meleeComponent.OnFirstHit.AddListener(OnFirstHit);
            owner.OnUpdate += Update;
        }

        public override void OnUpdate()
        {
            if (_item.isSelected)
            {
                if (_isImpacting)
                {
                    _weaponSystem.EndDamage();
                    _isImpacting = false;
                }
                return;
            }

            bool shouldDamage = _baseFields.rb.linearVelocity.magnitude > velocityImpactComponent.VelocityToDmg;

            if (shouldDamage && !_isImpacting)
            {
                _weaponSystem.BeginDamage();
                _isImpacting = true;
            }
            else if (!shouldDamage && _isImpacting)
            {
                _weaponSystem.EndDamage();
                _isImpacting = false;
            }
        }

        private void OnFirstHit(HitInfo hit)
        {
            if (_item.itemComponent.currentOwner != null)
                return;

            if (hit.hitPosition != null)
            {
                Vector2 dir = ((Vector2)_baseFields.rb.transform.position - hit.hitPosition.Value).normalized;
                _baseFields.rb.linearVelocity = Vector2.zero;
                _baseFields.rb.AddForce(dir * velocityImpactComponent.bounceForce, ForceMode2D.Impulse);
            }
        }

        public void Dispose()
        {
            owner.OnUpdate -= OnUpdate;
            _meleeComponent.OnFirstHit.RemoveListener(OnFirstHit);
        }
    }
}