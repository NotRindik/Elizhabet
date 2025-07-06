using Controllers;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts;
using States;
using Systems;
using UnityEngine;

namespace Systems {
    
    public class OneHandedWeapon : MeleeWeapon
    {
        
        public override void SelectItem(Controller owner)
        {
            base.SelectItem(owner);
            meleeWeaponSystem = new OneHandAttackSystem();
            meleeWeaponSystem.Initialize(this);
            inputComponent.input.GetState().Attack.started += AttackHandle;
        }

        public virtual void AttackHandle(bool started)
        {
            if (attackComponent.canAttack)
            {
                animationComponent.CrossFade("OneArmed_AttackForward",0.1f);
                fsmSystem.SetState(new AttackState(itemComponent.currentOwner));
                meleeWeaponSystem.Attack();
            }
        }

        protected override void ReferenceClean()
        {
            if(isSelected)
                inputComponent.input.GetState().Attack.started -= AttackHandle;
            base.ReferenceClean();
            fsmSystem = null;
        }
    }
}

public class OneHandAttackSystem : MeleeWeaponSystem
{
    protected ItemComponent _itemComponent;
    protected AnimationComponent _animationComponent;
    public override void Initialize(Controller owner)
    {
        base.Initialize(owner);
        _itemComponent = owner.GetControllerComponent<ItemComponent>();
        _animationComponent = _itemComponent.currentOwner.GetControllerComponent<AnimationComponent>();
    }
    protected override IEnumerator AttackProcess() 
    {
        _animationComponent.SetAnimationSpeed(_meleeComponent.attackSpeed);
        bool firsHit = false;

        yield return new WaitUntil(() => _attackComponent.isAttackFrame);
        string animationTemp = _animationComponent.currentState;
        _meleeComponent.trail.gameObject.SetActive(true);
        AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}Замах", volume:0.8f);
        while (_attackComponent.isAttackFrame && animationTemp == _animationComponent.currentState)
        {
            yield return new WaitForFixedUpdate();
            bool oneHitFlag = false;
            UpdateCollider();
            Collider2D[] hits = _meleeComponent.CheckObjectsInsideTrail(out var hitCount,_weaponComponent.attackLayer);
            for (int j = 0; j < hitCount; j++)
            {
                if (hits[j].TryGetComponent(out EntityController controller))
                {
                    if (!hitedList.Contains(controller.gameObject))
                    {
                        controller.GetControllerSystem<HealthSystem>().TakeHit(_weaponComponent.damage);
                        var targetRb = controller.baseFields.rb;
                        Vector2 dir = (controller.transform.position - owner.transform.position).normalized;
                        var totalForce = (dir.normalized * _meleeComponent.pushbackForce) + (Vector2.up * _meleeComponent.liftForce);
                        targetRb.AddForce(totalForce, ForceMode2D.Impulse);
                        
                        var selfRb = _itemComponent.currentOwner.baseFields.rb;
                        
                        hitedList.Add(controller.gameObject);
                            
                        if (!oneHitFlag)
                        {
                            AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}hitHurt{Random.Range(1,4)}",volume:0.5f);
                            oneHitFlag = true;
                        }
                        if (!firsHit)
                        {
                            selfRb.AddForce(-dir * _meleeComponent.pushbackForce * 0.25f, ForceMode2D.Impulse);
                            var healthComponent = controller.GetControllerComponent<HealthComponent>();
                            float damage = _weaponComponent.damage;
                            float ratio = Mathf.Clamp01(damage / (healthComponent.maxHealth + 1e-5f));
                            float hitStopDuration = Mathf.Lerp(0.03f, 0.08f, Mathf.Sqrt(ratio)); // √ делает прирост мягче
                            float slowdownFactor = Mathf.Lerp(0.95f, 0.4f, ratio);
                            
                            TimeManager.StartHitStop(hitStopDuration, 0.12f, slowdownFactor, owner);
                            _healthComponent.currHealth--;   
                            firsHit = true;
                        }
                    }
                } 
            }
        }
        yield return null;
        _meleeComponent.trail.gameObject.SetActive(false);
        _animationComponent.SetAnimationSpeed(1);
        UnAttack(); 
    }
        protected void UpdateCollider()
        {
            if (_meleeComponent.trail == null || _meleeComponent.polygonCollider == null)
                return;

            int pointCount = _meleeComponent.trail.positionCount;
            if (pointCount < 2) return;

            _meleeComponent.points.Clear();

            float width = _meleeComponent.trail.startWidth;
            List<Vector2> upperPoints = new List<Vector2>();
            List<Vector2> lowerPoints = new List<Vector2>();

            for (int i = 0; i < pointCount; i++)
            {
                Vector3 worldPoint = _meleeComponent.trail.GetPosition(i);
                Vector2 localPoint = _meleeComponent.polygonCollider.transform.InverseTransformPoint(worldPoint);

                Vector2 offset;
                if (i < pointCount - 1)
                {
                    // Вычисляем направление между текущей и следующей точкой
                    Vector3 nextWorldPoint = _meleeComponent.trail.GetPosition(i + 1);
                    Vector2 direction = ((Vector2)_meleeComponent.polygonCollider.transform.InverseTransformPoint(nextWorldPoint) - localPoint).normalized;

                    // Берем перпендикуляр к направлению
                    offset = new Vector2(-direction.y, direction.x) * (width / 2);
                }
                else
                {
                    // Для последней точки берем направление от предыдущей
                    Vector3 prevWorldPoint = _meleeComponent.trail.GetPosition(i - 1);
                    Vector2 direction = (localPoint - (Vector2)_meleeComponent.polygonCollider.transform.InverseTransformPoint(prevWorldPoint)).normalized;

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

            _meleeComponent.polygonCollider.SetPath(0, colliderPoints);
        }

}