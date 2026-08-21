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
        public WeaponType weaponType;
        public List<IntPtr> modifiedDamage; //КАРОЧЕ НАДА ПИХАЙТЕ ТОКА DamageComponent указатели а то АТАТА

        public unsafe DamageComponent GetFullDamage()
        {
            DamageComponent result = damage;
            
            if (modifiedDamage == null || modifiedDamage.Count == 0)
                return result;
            
            
            foreach (var damages in modifiedDamage)
            {
                if(damages == IntPtr.Zero)
                    continue;
                
                result += *((DamageComponent*)damages);
            }

            return result;
        }
    }
}