using System;
using UnityEngine;
namespace Systems {
    public class OneHandedWeapon : Items
    {
        public WeaponData weaponData;
        public override void TakeUp()
        {
            base.TakeUp();
        }

        public override void Throw()
        {
            base.Throw();
        }
    }

    [System.Serializable]
    public class WeaponData : IComponent
    {
        Type weaponType;
        float damage;
        float attackSpeed;
        LayerMask attackLayer;
        public int durability;
    }
}