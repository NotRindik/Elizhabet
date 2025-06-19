using Controllers;
using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts;
using UnityEngine;
using UnityEngine.Serialization;

namespace Systems {
    
    public class OneHandedWeapon : Weapon
    {
        private List<Controller> hitedList = new List<Controller>();
        private Coroutine _attackProcess;
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
            filter.SetLayerMask(weaponComponent.attackLayer);
                
            hitCount = weaponComponent.polygonCollider.Overlap(filter, hits);

            return hits;
        }

        public void Attack()
        {
            hitedList.Clear();
            weaponComponent.trail.gameObject.SetActive(true);
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
            weaponComponent.trail.gameObject.SetActive(false);
            if (durabilityComponent.Durability <= 0)
            {
                DestroyItem();   
            }
        }

        private IEnumerator AttackProcess()
        {
            bool firsHit = false;
            while (true)
            {
                yield return new WaitForFixedUpdate();
                bool oneHitFlag = false;
                UpdateCollider();
                Collider2D[] hits = CheckObjectsInsideTrail(out var hitCount);
                for (int j = 0; j < hitCount; j++)
                {
                    if (hits[j].TryGetComponent(out EntityController controller))
                    {
                        if (!hitedList.Contains(controller))
                        {
                            controller.GetControllerSystem<HealthSystem>().TakeHit(weaponComponent.damage);
                            var targetRb = controller.baseFields.rb;
                            Vector2 dir = controller.transform.position - transform.position;
                            targetRb.AddForce((dir + Vector2.up) * weaponComponent.knockbackForce ,ForceMode2D.Impulse);
                            itemComponent.currentOwner.baseFields.rb.AddForce((-dir) * weaponComponent.knockbackForce/4 ,ForceMode2D.Impulse);
                            hitedList.Add(controller);
                            
                            if (!oneHitFlag)
                            {
                                AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}Разрез");
                                oneHitFlag = true;
                            }
                            if (!firsHit)
                            {
                                StartCoroutine(HitStop(0.1f + weaponComponent.knockbackForce * 0.005f,0.4f));
                                durabilityComponent.Durability--;   
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
            if (weaponComponent.trail == null || weaponComponent.polygonCollider == null)
                return;

            int pointCount = weaponComponent.trail.positionCount;
            if (pointCount < 2) return;

            weaponComponent.points.Clear();

            float width = weaponComponent.trail.startWidth;
            List<Vector2> upperPoints = new List<Vector2>();
            List<Vector2> lowerPoints = new List<Vector2>();

            for (int i = 0; i < pointCount; i++)
            {
                Vector3 worldPoint = weaponComponent.trail.GetPosition(i);
                Vector2 localPoint = weaponComponent.polygonCollider.transform.InverseTransformPoint(worldPoint);

                Vector2 offset;
                if (i < pointCount - 1)
                {
                    // Вычисляем направление между текущей и следующей точкой
                    Vector3 nextWorldPoint = weaponComponent.trail.GetPosition(i + 1);
                    Vector2 direction = ((Vector2)weaponComponent.polygonCollider.transform.InverseTransformPoint(nextWorldPoint) - localPoint).normalized;

                    // Берем перпендикуляр к направлению
                    offset = new Vector2(-direction.y, direction.x) * (width / 2);
                }
                else
                {
                    // Для последней точки берем направление от предыдущей
                    Vector3 prevWorldPoint = weaponComponent.trail.GetPosition(i - 1);
                    Vector2 direction = (localPoint - (Vector2)weaponComponent.polygonCollider.transform.InverseTransformPoint(prevWorldPoint)).normalized;

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

            weaponComponent.polygonCollider.SetPath(0, colliderPoints);
        }


    }
}