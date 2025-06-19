using System;
using System.Collections.Generic;
using Systems;
using UnityEngine;

namespace Controllers
{
    public class Weapon : Item
    {
        public DurabilityComponent durabilityComponent;
        public WeaponComponent weaponComponent;
        protected override void Start()
        {
            base.Start();
            if (InitAfterInventory)
            {
                durabilityComponent.Durability = durabilityComponent.maxDurability;
            }
        }
        
        protected override void OnValidate()
        {
            if (!Application.isPlaying)
            {
                if (!weaponComponent.trail)
                    weaponComponent.trail = transform.GetComponentInChildren<TrailRenderer>(true);

                if (!weaponComponent.polygonCollider)
                    weaponComponent.polygonCollider = transform.GetComponentInChildren<PolygonCollider2D>(true);
            }
        }
    }
    
    
    [Serializable]
    public class WeaponComponent : IComponent
    {
        public Type weaponType;
        public float damage;
        public float attackSpeed;
        public float knockbackForce;
        public LayerMask attackLayer;
        public TrailRenderer trail;
        
        public PolygonCollider2D polygonCollider;
        public List<Vector2> points = new List<Vector2>();
    }
}