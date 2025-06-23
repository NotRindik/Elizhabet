using Controllers;
using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts;
using States;
using UnityEngine;

namespace Systems {
    
    public class OneHandedWeapon : MeleeWeapon
    {
        private List<Controller> hitedList = new List<Controller>();
        private SlideComponent slideComponent;
        private WallRunComponent wallRunComponent;
        private WallEdgeClimbComponent wallEdgeClimbComponent;
        private HookComponent hookComponent;
        private AttackComponent _attackComponent;
        private FSMSystem _fsmSystem;
        private Action<bool> AttackHandler;

        private AnimationComponent _animationComponentl;
        public override void SelectItem(Controller owner)
        {
            base.SelectItem(owner);
            slideComponent = owner.GetControllerComponent<SlideComponent>();
            wallRunComponent = owner.GetControllerComponent<WallRunComponent>();
            wallEdgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
            _animationComponentl = owner.GetControllerComponent<AnimationComponent>();
            hookComponent = owner.GetControllerComponent<HookComponent>();
            _attackComponent = owner.GetControllerComponent<AttackComponent>();
            _fsmSystem = owner.GetControllerSystem<FSMSystem>();
            
            AttackHandler = _ =>
            {
                if (slideComponent.SlideProcess == null && wallRunComponent.wallRunProcess == null && wallEdgeClimbComponent.EdgeStuckProcess == null && !hookComponent.isHooked)
                {
                    _animationComponentl.CrossFade("OneArmed_AttackForward",0.1f);
                    _fsmSystem.SetState(new AttackState(owner));
                    //Attack();
                }
            };
            itemComponent.input.GetState().Attack.started += AttackHandler;
        }

        public override void Throw()
        {
            base.Throw();
        }

        public override void OnDestroy()
        {
            if(AttackHandler != null)
                itemComponent.input.GetState().Attack.started -= AttackHandler;
            base.OnDestroy();
        }

        /*void UpdateCollider()
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
        }*/


    }
}

public class TimeController : MonoBehaviour
{
    private static Coroutine hitStopRoutine;

    public static void StartHitStop(float duration, float slowdownFactor, MonoBehaviour context)
    {
        if (hitStopRoutine == null)
        {
            hitStopRoutine = context.StartCoroutine(HitStop(duration, slowdownFactor));
        }
    }

    private static IEnumerator HitStop(float duration, float slowdownFactor)
    {
        Time.timeScale = slowdownFactor;
        yield return new WaitForSecondsRealtime(Mathf.Min(duration, 0.3f));
        Time.timeScale = 1f;
        hitStopRoutine = null;
    }
}
