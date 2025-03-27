
using System;
using System.Collections;
using Controllers;
using UnityEngine;

namespace Systems
{
    public class AttackSystem : BaseSystem
    {
        private InventoryComponent inventoryComponent;
        private AttackComponent _attackComponent;
        private AnimationStateComponent _animationStateComponent;
        
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            inventoryComponent = owner.GetControllerComponent<InventoryComponent>();
            _attackComponent = owner.GetControllerComponent<AttackComponent>();
            _animationStateComponent = base.owner.GetControllerComponent<AnimationStateComponent>();
        }

        public override void Update()
        {
            AttackProcess();
        }

        private void AttackProcess()
        {
            if ((inventoryComponent.items.Count > 0))
            {
                if (_attackComponent.AttackProcess != null)
                {
                    return;
                }
                _attackComponent.AttackProcess = owner.StartCoroutine(AttackProcessCO());
            }
        }

        private IEnumerator AttackProcessCO()
        {
            while (_animationStateComponent.currentState is not OneHandAttack)
            {
                yield return null;
            }
            _attackComponent.isAttack = true;
            var weaponData = ((OneHandedWeapon)inventoryComponent.ActiveItem).weaponData;
            ((OneHandedWeapon)inventoryComponent.ActiveItem).Attack();
            _animationStateComponent.animator.speed = weaponData.attackSpeed;
            
            while (_animationStateComponent.currentState is OneHandAttack)
            {
                yield return null;
            }
            ((OneHandedWeapon)inventoryComponent.ActiveItem).UnAttack();
            _animationStateComponent.animator.speed = 1;
            _attackComponent.isAttack = false;
            _attackComponent.AttackProcess = null;
        }
    }
    

[System.Serializable]
    public class AttackComponent : IComponent
    {
        public Coroutine AttackProcess;
        public bool isAttack;
    }
}
