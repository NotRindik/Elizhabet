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
        protected AttackComponent attackComponent;
        protected AnimationComponentsComposer animationComponent;
        protected FSMSystem fsmSystem;
        protected SpriteFlipSystem spriteFlipSystem;
        protected SpriteFlipComponent spriteFlipComponent;

        public override void SelectItem(Controller owner)
        {
            base.SelectItem(owner);
            attackComponent = owner.GetControllerComponent<AttackComponent>();
            spriteFlipComponent = owner.GetControllerComponent<SpriteFlipComponent>();
            animationComponent = owner.GetControllerComponent<AnimationComponentsComposer>();
            fsmSystem = owner.GetControllerSystem<FSMSystem>();
            spriteFlipSystem = owner.GetControllerSystem<SpriteFlipSystem>();
            AddControllerComponent(attackComponent);
            AddControllerComponent(spriteFlipComponent);
            AddControllerSystem(spriteFlipSystem);
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
            if (isSelected)
            {
                spriteFlipComponent = null;
            }
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