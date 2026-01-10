using Controllers;
using States;
using System;
using UnityEngine;
namespace Systems {

    [System.Serializable]
    public struct ComboComponent : IComponent
    {
        public float timeToResetCombo;
        private int _index;
        public int CurrCombo { get => _index;  set 
            {
                _index = value;
                if(_index >= animationCombo.Length)
                    _index = 0;
            } }
        public string[] animationCombo;
    }

    [System.Serializable]
    public class HandRotatorsComponent : IComponent
    {
        public Transform left, right;
    }

    public class OneHandedWeapon : MeleeWeapon
    {
        private HandRotatorsComponent handRotatorsComponent;
        public Vector3 PointPos;
        public float angleOffset;
        public Vector3 rotTemp;
        protected Camera cams;

        public Action<InputContext> rotContext;

        public Coroutine comboTimerProcess;
        public override void SelectItem(AbstractEntity owner)
        {
            base.SelectItem(owner);
            handRotatorsComponent = owner.GetControllerComponent<HandRotatorsComponent>();
            inputComponent.input.GetState().Attack.started += AttackAnimationHandle;
            attackComponent.OnAttackStart += AttackHandle;
            attackComponent.OnAttackEnd += EndAttack;
            cams = Camera.main;
            rotContext = с =>
            {
                UpdateSreenPos(с.ReadValue<Vector2>());
            };

            UpdateSreenPos(inputComponent.input.GetState().Point.ReadValue<Vector2>());
            inputComponent.input.GetState().Point.performed += rotContext;
        }

        private void UpdateSreenPos(Vector2 value)
        {
            PointPos = value;
            PointPos.z = Mathf.Abs(cams.transform.position.z);
        }

        public void ApplyAngle()
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(PointPos);

            Vector2 dir = (worldPos - itemComponent.currentOwner.transform.position);

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            rotTemp = handRotatorsComponent.right.rotation.eulerAngles;

            if (itemComponent.currentOwner.transform.localScale.x < 0)
            {
                angle = 180f - angle;
            }
            handRotatorsComponent.right.localRotation = Quaternion.Euler(0, 0, (angle + angleOffset));
        }

        public virtual void AttackAnimationHandle(InputContext started)
        {
            if (attackComponent.canAttack)
            {
                animationComponent.UnlockParts("LeftHand", "RightHand", "Main");

                if (comboComponent.animationCombo != null)
                {
                    if(comboTimerProcess != null)
                        StopCoroutine(comboTimerProcess);

                    comboTimerProcess = StartCoroutine(std.Utilities.Invoke(() => comboComponent.CurrCombo = 0, comboComponent.timeToResetCombo));
                    animationComponent.PlayState(comboComponent.animationCombo[comboComponent.CurrCombo], 0, 0f);
                    comboComponent.CurrCombo++;
                }
                animationComponent.LockParts("LeftHand", "RightHand", "Main");
                ApplyAngle();
                fsmSystem.SetState(new AttackState(itemComponent.currentOwner));
                attackComponent.isAttackAnim = true;
            }
        }
        public virtual void AttackHandle()
        {
            meleeComponent.trail.emitting = true;
            meleeWeaponSystem.BeginDamage();
        }
        public virtual void EndAttack()
        {
            attackComponent.isAttackAnim = false;
            meleeComponent.trail.Clear();
            meleeComponent.trail.emitting = false;
            handRotatorsComponent.right.localRotation = Quaternion.Euler(Vector3.zero);
            animationComponent.UnlockParts("LeftHand", "RightHand", "Main");
            meleeWeaponSystem.EndDamage();
            animationComponent.PlayState("Idle", 0, 0f);
        }
        protected override void ReferenceClean()
        {
            if (isSelected)
            {
                inputComponent.input.GetState().Point.performed -= rotContext;
                inputComponent.input.GetState().Attack.started -= AttackAnimationHandle;
                attackComponent.OnAttackStart -= AttackHandle;
                attackComponent.OnAttackEnd -= EndAttack;
            }
            base.ReferenceClean();
            fsmSystem = null;
        }
    }
}