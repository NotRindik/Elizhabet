using System;
using System.Collections;
using System.Collections.Generic;
using States;
using Systems;
using UnityEngine;

namespace Controllers
{
    public class TwoHandedMeleeWeapon : OneHandedWeapon
    {
        private int _attackCount = 0;
        private Coroutine _comboTimeProcess;
        private readonly HashSet<string> oneHandAnimations = new() { "VerticalWallRun", "WallEdgeClimb"};
        public override void SelectItem(Controller owner)
        {
            base.SelectItem(owner);
            itemPositioningSystem = new TwoHandPositioning();
            animationComponent.OnAnimationStateChange += OnAnimationStateChange;
            itemPositioningSystem.Initialize(this);
            meleeWeaponSystem = new OneHandAttackSystem();
            meleeWeaponSystem.Initialize(this);
        }
        public override void AttackAnimationHandle(bool started)
        {
            if (attackComponent.canAttack && attackComponent.isAttackAnim == false)
            {
                fsmSystem.SetState(new AttackState(itemComponent.currentOwner));
                if (_attackCount == 0)
                {
                    animationComponent.Play("OneArmed_AttackForward", 0, 0f);
                }
                else
                {
                    animationComponent.Play("TwoHandedWeapon", 0, 0f);
                }
                _attackCount++;
                Debug.Log(_attackCount);
                attackComponent.isAttackAnim = true;
                if (_comboTimeProcess == null)
                {
                    _comboTimeProcess = StartCoroutine(ComboTime());
                }
            }
        }

        public void OnAnimationStateChange(string anim)
        {
            void Switch(ItemPositioningSystem system)
            {
                itemPositioningSystem = system;
                system.Initialize(this);
            }

            if (oneHandAnimations.Contains(anim))
                Switch(new OneHandPositioning());
            else
                Switch(new TwoHandPositioning());
        }
        protected override void ReferenceClean()
        {
            if(isSelected)
                animationComponent.OnAnimationStateChange -= OnAnimationStateChange;
            base.ReferenceClean();
        }

        public IEnumerator ComboTime()
        {
            var temp = _attackCount;
            yield return new WaitUntil(() => attackComponent.isAttackAnim == false);
            yield return new WaitForSeconds(0.2f);
            if(temp == _attackCount || _attackCount > 1)
                _attackCount = 0;
            _comboTimeProcess = null;
            Debug.Log(_attackCount);
        }
    }
}