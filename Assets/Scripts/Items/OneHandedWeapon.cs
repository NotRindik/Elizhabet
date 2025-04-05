using Controllers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DefaultNamespace;
using UnityEngine;
using UnityEngine.Serialization;

namespace Systems {
    
    public class OneHandedWeapon : Items
    {
        public WeaponData weaponData = new WeaponData();
        private List<Controller> hitedList = new List<Controller>();
        private Coroutine _attackProcess;
        
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
        Collider2D[] CheckObjectsInsideTrail(out int hitCount)
        {
            Collider2D[] hits = new Collider2D[10];
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(weaponData.attackLayer);
                
            hitCount = weaponData.polygonCollider.Overlap(filter, hits);

            return hits;
        }

        public void Attack()
        {
            hitedList.Clear();
            weaponData.trail.gameObject.SetActive(true);

            if (_attackProcess == null)
            {
                _attackProcess = StartCoroutine(AttackProcess());
            }
        }

        public void UnAttack()
        {
            if(_attackProcess != null)
                StopCoroutine(_attackProcess);
            _attackProcess = null;
            weaponData.trail.gameObject.SetActive(false);

            if (itemComponent.durability <= 0)
            {
                var inventorySystem = itemComponent.currentOwner.GetControllerSystem<InventorySystem>();
                var inventoryComponent = itemComponent.currentOwner.GetControllerComponent<InventoryComponent>();
                inventoryComponent.ActiveItem = null;
                int oldIndex = inventoryComponent.currentActiveIndex;
                var stack = inventoryComponent.items.FirstOrDefault(stack => stack.itemName == itemComponent.itemPrefab.name);
                
                if(stack.items.Count == 0)
                    inventoryComponent.items.Remove(stack);
                
                int newIndex = Mathf.Clamp(oldIndex, 0, inventoryComponent.items.Count - 1);

                if (inventoryComponent.items.Count > 0)
                {
                    inventorySystem.SetActiveWeaponWithoutDestroy(newIndex);
                }

                Destroy(gameObject);
            }
        }

        private IEnumerator AttackProcess()
        {
            bool firsHit = false;
            while (true)
            {
                yield return new WaitForFixedUpdate();
                UpdateCollider();
                Collider2D[] hits = CheckObjectsInsideTrail(out var hitCount);
                for (int j = 0; j < hitCount; j++)
                {
                    if (hits[j].TryGetComponent(out EntityController controller))
                    {
                        if (!hitedList.Contains(controller))
                        {
                            controller.GetControllerSystem<HealthSystem>().TakeHit(weaponData.damage);
                            var targetRb = controller.baseFields.rb;
                            Vector2 dir = controller.transform.position - transform.position;
                            targetRb.AddForce((dir + Vector2.up) * weaponData.knockbackForce ,ForceMode2D.Impulse);
                            itemComponent.currentOwner.baseFields.rb.AddForce((-dir) * weaponData.knockbackForce/3 ,ForceMode2D.Impulse);
                            hitedList.Add(controller);
                            
                            if (!firsHit)
                            {
                                StartCoroutine(HitStop(0.1f + weaponData.knockbackForce * 0.005f,0.4f));
                                itemComponent.durability--;   
                                firsHit = true;
                            }
                        }
                    } 
                }
            }
        }
        IEnumerator HitStop(float duration, float slowdownFactor)
        {
            Time.timeScale = slowdownFactor;
            yield return new WaitForSecondsRealtime(Mathf.Min(duration,0.3f));
            Time.timeScale = 1f;
        }

        void UpdateCollider()
        {
            if (weaponData.trail == null || weaponData.polygonCollider == null)
                return;

            int pointCount = weaponData.trail.positionCount;
            if (pointCount < 2) return;

            weaponData.points.Clear();

            float width = weaponData.trail.startWidth;
            List<Vector2> upperPoints = new List<Vector2>();
            List<Vector2> lowerPoints = new List<Vector2>();

            for (int i = 0; i < pointCount; i++)
            {
                Vector3 worldPoint = weaponData.trail.GetPosition(i);
                Vector2 localPoint = weaponData.polygonCollider.transform.InverseTransformPoint(worldPoint);

                Vector2 offset;
                if (i < pointCount - 1)
                {
                    // Вычисляем направление между текущей и следующей точкой
                    Vector3 nextWorldPoint = weaponData.trail.GetPosition(i + 1);
                    Vector2 direction = ((Vector2)weaponData.polygonCollider.transform.InverseTransformPoint(nextWorldPoint) - localPoint).normalized;

                    // Берем перпендикуляр к направлению
                    offset = new Vector2(-direction.y, direction.x) * (width / 2);
                }
                else
                {
                    // Для последней точки берем направление от предыдущей
                    Vector3 prevWorldPoint = weaponData.trail.GetPosition(i - 1);
                    Vector2 direction = (localPoint - (Vector2)weaponData.polygonCollider.transform.InverseTransformPoint(prevWorldPoint)).normalized;

                    // Берем перпендикуляр к направлению
                    offset = new Vector2(-direction.y, direction.x) * (width / 2);
                }

                upperPoints.Add(localPoint + offset);
                lowerPoints.Add(localPoint - offset);
            }

            // Переворачиваем нижнюю часть, чтобы соединить контур правильно
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
        public float knockbackForce;
        public LayerMask attackLayer;
        public TrailRenderer trail;
        
        public PolygonCollider2D polygonCollider;
        public List<Vector2> points = new List<Vector2>();

    }
}