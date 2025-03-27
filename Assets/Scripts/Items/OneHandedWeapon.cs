using Controllers;
using System;
using System.Collections.Generic;
using DefaultNamespace;
using UnityEngine;
using UnityEngine.Serialization;

namespace Systems {
    
    public class OneHandedWeapon : Items
    {
        public WeaponData weaponData = new WeaponData();
        private List<Controller> hitedList = new List<Controller>();
        private bool _isAttack;

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                if (!weaponData.trail)
                    weaponData.trail = transform.GetComponentInChildren<TrailRenderer>(true);

                if (!weaponData.polygonCollider)
                    weaponData.polygonCollider = transform.GetComponentInChildren<PolygonCollider2D>(true);
            }
        }
        public override void TakeUp(ColorPositioningComponent colorPositioning, Controller owner)
        {
            base.TakeUp(colorPositioning, owner);
        }

        public override void Throw()
        {
            base.Throw();
        }

        public void Update()
        {
            if (_isAttack)
            {
                UpdateCollider();
                CheckObjectsInsideTrail();
            }
        }
        void CheckObjectsInsideTrail()
        {
            
            for (int i = 0; i < weaponData.trail.positionCount; i++)
            {
                Collider2D[] hits = new Collider2D[10];
                ContactFilter2D filter = new ContactFilter2D();
                filter.SetLayerMask(weaponData.attackLayer);
                int hitCount = weaponData.polygonCollider.Overlap(filter, hits);

                for (int j = 0; j < hitCount; j++)
                {
                    if (hits[j].TryGetComponent(out Controller controller))
                    {
                        if (!hitedList.Contains(controller))
                        {
                            controller.GetControllerSystem<HealthSystem>().TakeHit(weaponData.damage);
                            hitedList.Add(controller);
                        }
                    } 
                }
            }
        }

        public void Attack()
        {
            _isAttack = true;
            hitedList.Clear();
            weaponData.trail.gameObject.SetActive(true);
        }

        public void UnAttack()
        {
            _isAttack = false;
            weaponData.trail.gameObject.SetActive(false);
        }
        
        void UpdateCollider()
        {
            if (weaponData.trail == null || weaponData.polygonCollider == null)
                return;

            if (weaponData.trail.positionCount < 2) return;

            weaponData.points.Clear();
    
            float width = weaponData.trail.startWidth;
            Vector3 normal = Vector3.forward;

            List<Vector2> upperPoints = new List<Vector2>();
            List<Vector2> lowerPoints = new List<Vector2>();

            for (int i = 0; i < weaponData.trail.positionCount; i++)
            {
                Vector3 worldPoint = weaponData.trail.GetPosition(i);
                Vector2 localPoint = weaponData.polygonCollider.transform.InverseTransformPoint(worldPoint);
            
                Vector2 offset = Vector2.Perpendicular(localPoint).normalized * (width / 2);
                upperPoints.Add(localPoint + offset);
                lowerPoints.Add(localPoint - offset);
            }

            lowerPoints.Reverse();
        
            List<Vector2> colliderPoints = new List<Vector2>(upperPoints);
            colliderPoints.AddRange(lowerPoints);
    
            weaponData.polygonCollider.SetPath(0, colliderPoints);
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
        public TrailRenderer trail;
        
        public PolygonCollider2D polygonCollider;
        public List<Vector2> points = new List<Vector2>();

    }
}