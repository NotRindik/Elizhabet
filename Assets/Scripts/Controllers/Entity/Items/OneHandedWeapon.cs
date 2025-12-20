using Controllers;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts;
using States;
using Systems;
using UnityEngine;
using System.Linq;

namespace Systems {
    
    public class OneHandedWeapon : MeleeWeapon
    {
        public override void SelectItem(Controller owner)
        {
            base.SelectItem(owner);
            meleeWeaponSystem = new OneHandAttackSystem();
            meleeWeaponSystem.Initialize(this);
            inputComponent.input.GetState().Attack.started += AttackAnimationHandle;
            attackComponent.OnAttackStart += AttackHandle;

        }

        public virtual void AttackAnimationHandle(InputContext started)
        {
            if (attackComponent.canAttack && attackComponent.AttackProcess == null)
            {
                animationComponent.UnlockParts("LeftHand", "RightHand", "Main");
                animationComponent.PlayState("AttackForward", 0, 0f);
                animationComponent.LockParts("LeftHand", "RightHand", "Main");

                spriteFlipSystem.IsActive = false;

                fsmSystem.SetState(new AttackState(itemComponent.currentOwner));
                attackComponent.isAttackAnim = true;
            }
        }
        public virtual void AttackHandle() => meleeWeaponSystem.Attack();

        protected override void ReferenceClean()
        {
            if (isSelected)
            {
                inputComponent.input.GetState().Attack.started -= AttackAnimationHandle;
                attackComponent.OnAttackStart -= AttackHandle;
            }
            base.ReferenceClean();
            fsmSystem = null;
        }
    }
}

public class OneHandAttackSystem : MeleeWeaponSystem
{
    protected ItemComponent _itemComponent;
    protected AnimationComponentsComposer _animationComponent;
    public override void Initialize(IController owner)
    {
        base.Initialize(owner);
        _itemComponent = owner.GetControllerComponent<ItemComponent>();
        _animationComponent = _itemComponent.currentOwner.GetControllerComponent<AnimationComponentsComposer>();
    }
    protected override IEnumerator AttackProcess() 
    {
        List<Collider2D> hitColliders = new();
        _animationComponent.SetSpeedOfParts(_meleeComponent.attackSpeed, "LeftHand", "RightHand", "Main");
        bool firsHit = false;

        string animationTemp = _animationComponent.CurrentState;
        _meleeComponent.trail.gameObject.SetActive(true);
        AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}Замах", volume:0.8f);
        float t = 0;
        Debug.Log("Start");
        while (t < 0.9f)
        {
            yield return null;
            var stateInfo = _animationComponent.animations["Main"].animator.GetCurrentAnimatorStateInfo(0);
            t = stateInfo.normalizedTime % 1f;
            bool oneHitFlag = false;
            UpdateCollider();
            hitColliders.Clear();
            hitColliders.AddRange(_meleeComponent.CheckObjectsInsideCollider(out var hitCount,_meleeComponent.polygonCollider, _weaponComponent.attackLayer).Where(a => a != null));
            foreach (var collider in _baseFields.collider)
            {
                hitColliders.AddRange(_meleeComponent.CheckObjectsInsideCollider(out var _, collider, _weaponComponent.attackLayer).Where(a => a != null));
            }
            for (int j = 0; j < hitColliders.Count; j++)
            {
                if (hitColliders[j].TryGetComponent(out IController controller))
                {
                    if (!hitedList.Contains(controller.mono.gameObject))
                    {
                        Vector2 hitDir = (controller.mono.transform.position - transform.position).normalized;
                        Vector2 hitPoint = hitColliders[j].ClosestPoint(transform.position);



                        var hs = controller.GetControllerSystem<HealthSystem>();
                        new Damage(_weaponComponent.modifiedDamage, controller.GetControllerComponent<ProtectionComponent>()).ApplyDamage(hs,new HitInfo(hitPoint));

                        var targetRb = controller.GetControllerComponent<ControllersBaseFields>()?.rb;
                        Vector2 dir = (controller.mono.transform.position - transform.position).normalized;
                        var totalForce = (dir.normalized * _meleeComponent.pushbackForce) + (Vector2.up * _meleeComponent.liftForce);
                        targetRb?.AddForce(totalForce, ForceMode2D.Impulse);

                        var selfRb = _itemComponent.currentOwner.baseFields.rb;

                        hitedList.Add(controller.mono.gameObject);

                        if (!oneHitFlag)
                        {
                            AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}hitHurt{Random.Range(1, 4)}", volume: 0.5f);
                            oneHitFlag = true;
                        }
                        if (!firsHit)
                        {
                            firsHit = true;
                            OnFirstHit(selfRb, dir, controller);
                        }
                    }
                }
            }
        }
        Debug.Log("End");
        _meleeComponent.trail.gameObject.SetActive(false);
        _animationComponent.SetSpeedOfParts(1, "LeftHand", "RightHand", "Main");
        UnAttack();
    }

    public override void UnAttack()
    {
        base.UnAttack();
        _animationComponent.UnlockParts("LeftHand", "RightHand", "Main");
    }


    protected virtual void OnFirstHit(Rigidbody2D selfRb, Vector2 dir, IController controller)
    {
        selfRb.AddForce(-dir * _meleeComponent.pushbackForce * 0.25f, ForceMode2D.Impulse);
        var healthComponent = controller.GetControllerComponent<HealthComponent>();
        float damage = _weaponComponent.damage.BaseDamage;
        float ratio = Mathf.Clamp01(damage / (healthComponent.maxHealth + 1e-5f));
        float hitStopDuration = Mathf.Lerp(0.03f, 0.08f, Mathf.Sqrt(ratio)); // √ делает прирост мягче
        float slowdownFactor = Mathf.Lerp(0.95f, 0.4f, ratio);

        TimeManager.StartHitStop(hitStopDuration, 0.12f, slowdownFactor, mono);
        PlayerCamShake.Instance.Shake(new ShakeData(1f,3f), 0.4f);
        _healthComponent.currHealth--;
    }
    public override void StopCoroutineSafely()
    {
        base.StopCoroutineSafely();
        _meleeComponent.trail.gameObject.SetActive(false);
        _animationComponent.SetSpeedAll(1);
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
