using Controllers;
using System;
using UnityEngine;
namespace Systems {
    public class OneHandedWeapon : Items
    {
        public WeaponData WeaponData = new WeaponData();
        public override void TakeUp(ColorPositioningComponent colorPositioning, Controller owner)
        {
            base.TakeUp(colorPositioning, owner);
        }

        public override void Throw()
        {
            base.Throw();
        }
    }

    [Serializable]
    public class WeaponData : IComponent
    {
        public Type weaponType;
        public float damage;
        public float attackSpeed;
        public LayerMask attackLayer;
        public int durability;
        public TrailRenderer trailRenderer;
    }
}