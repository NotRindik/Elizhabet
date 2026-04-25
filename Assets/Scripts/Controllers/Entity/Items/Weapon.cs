using System;
using System.Collections.Generic;
using System.Linq;
using Systems;
using UnityEngine;

namespace Controllers
{
    public abstract class Weapon : Item
    {
        public WeaponComponent weaponComponent = new WeaponComponent();
        [NonSerialized] public AttackComponent attackComponent;
        protected AnimationComponentsComposer animationComponent;
        protected FSMSystem fsmSystem;

        public override void SelectItem(AbstractEntity owner)
        {
            base.SelectItem(owner);
            attackComponent = owner.GetControllerComponent<AttackComponent>();
            animationComponent = owner.GetControllerComponent<AnimationComponentsComposer>();
            fsmSystem = owner.GetControllerSystem<FSMSystem>();
            AddControllerComponent(attackComponent);
            attackComponent.damageModifire.OnItemChanged += OnDamageDataUpdate;
            weaponComponent.modifiedDamage = weaponComponent.damage;
        }

        public unsafe void OnDamageDataUpdate(IntPtr _)
        {
            weaponComponent.modifiedDamage = weaponComponent.damage;
            foreach (DamageComponent* item in attackComponent.damageModifire.Raw)
            {
                weaponComponent.modifiedDamage *= *item;
            }
        }

        protected override void ReferenceClean()
        {
            base.ReferenceClean();
            if(attackComponent != null)
                attackComponent.damageModifire.OnItemChanged -= OnDamageDataUpdate;
            attackComponent = null;
        }
    }
    
    [Serializable]
    public class WeaponComponent : IComponent
    {
        public LayerMask attackLayer;
        public DamageComponent damage;
        public DamageComponent modifiedDamage;
    }
}