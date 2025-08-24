using System;
using System.Collections.Generic;
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

        public override void SelectItem(Controller owner)
        {
            base.SelectItem(owner);
            attackComponent = owner.GetControllerComponent<AttackComponent>();
            animationComponent = owner.GetControllerComponent<AnimationComponentsComposer>();
            fsmSystem = owner.GetControllerSystem<FSMSystem>();
            AddControllerComponent(attackComponent);
        }

        protected override void ReferenceClean()
        {
            base.ReferenceClean();
            attackComponent = null;
        }
    }
    
    [Serializable]
    public class WeaponComponent : IComponent
    {
        public LayerMask attackLayer;
        public float damage;
    }
}