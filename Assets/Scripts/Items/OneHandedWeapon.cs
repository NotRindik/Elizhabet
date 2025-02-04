using Controllers;
using System;
using UnityEngine;
using static Items;
namespace Systems {
    public class OneHandedWeapon : Items
    {

        private void Reset()
        {
            itemComponent = new WeaponData();
        }
        public override ItemComponent ItemComponent => itemComponent;

        public override void TakeUp(ColorPositioningComponent colorPositioning, Controller owner)
        {
            base.TakeUp(colorPositioning, owner);
        }

        public override void Throw()
        {
            base.Throw();
        }
    }

    [System.Serializable]
    public class WeaponData : ItemComponent
    {
        public Type weaponType;
        public float damage;
        public float attackSpeed;
        public LayerMask attackLayer;
        public int durability;
    }
}