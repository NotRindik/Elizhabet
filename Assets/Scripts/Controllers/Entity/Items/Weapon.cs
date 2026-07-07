using System;
using System.Collections.Generic;
using System.Linq;
using Systems;
using UnityEngine;

namespace Controllers
{
    using UnityEngine;

    public class Weapon : Item
    {

        protected override IComponent[] DefaultComponents =>
            base.DefaultComponents
                .Concat(new IComponent[]
                {
                    new WeaponComponent
                    {
                        attackLayer = LayerMask.GetMask("Enemy", "Prop", "PropEnemy","PlayerNoInteract"),
                        damage = new DamageComponent(1,0.1f,1,1)
                    }
                })
                .ToArray();
    }
    
    
    [Serializable]
    public class WeaponComponent : IComponent
    {
        public LayerMask attackLayer;
        public DamageComponent damage;
        public DamageComponent modifiedDamage;
    }
}