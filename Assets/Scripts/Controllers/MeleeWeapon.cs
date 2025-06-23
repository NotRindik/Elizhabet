using System.Collections.Generic;
using Systems;
using UnityEngine;

namespace Controllers
{
    public class MeleeWeapon : Weapon
    {

        /*public void Attack()
        {
            hitedList.Clear();
            weaponComponent.trail.gameObject.SetActive(true);
            if (_attackComponent.AttackProcess == null)
            {
                _attackComponent.AttackProcess = StartCoroutine(AttackProcess());
            }
        }

        public void UnAttack()
        {
            _attackComponent.AttackProcess = null;
            weaponComponent.trail.gameObject.SetActive(false);
            if (durabilityComponent.Durability <= 0)
            {
                DestroyItem();   
            }
        }

        private IEnumerator AttackProcess()
        {
            bool firsHit = false;

            yield return new WaitUntil(() => _attackComponent.isAttackFrame);
            while (_attackComponent.isAttackFrame)
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
                                TimeController.StartHitStop(0.1f + weaponComponent.knockbackForce * 0.005f,0.4f,this);
                                durabilityComponent.Durability--;   
                                firsHit = true;
                            }
                        }
                    } 
                }
            }
            UnAttack(); 
        }*/
    }

    public class MeleeComponent : IComponent
    {
        public float attackSpeed;
        public float knockbackForce;
        
        public TrailRenderer trail;
        
        public PolygonCollider2D polygonCollider;
        public List<Vector2> points = new List<Vector2>();
        
        Collider2D[] CheckObjectsInsideTrail(out int hitCount,LayerMask layerMask)
        {
            Collider2D[] hits = new Collider2D[10];
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(layerMask);
                
            hitCount = polygonCollider.Overlap(filter, hits);

            return hits;
        }
    }
}