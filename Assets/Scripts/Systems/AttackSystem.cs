
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
        private FSMSystem fsm;
        private Animator animator;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            inventoryComponent = owner.GetControllerComponent<InventoryComponent>();
            _attackComponent = owner.GetControllerComponent<AttackComponent>();
            fsm = base.owner.GetControllerSystem<FSMSystem>();
        }

        public override void OnUpdate()
        {
            AttackProcess();
        }

        private void AttackProcess()
        {
            if (_attackComponent.AttackProcess != null)
            {
                return;
            }
            _attackComponent.AttackProcess = owner.StartCoroutine(AttackProcessCO());
        }

        private IEnumerator AttackProcessCO()
        {
            /*while (fsm.currentState is not OneHandAttack)
            {
                yield return null;
            }*/
            _attackComponent.isAttack = true;
            if (inventoryComponent.ActiveItem)
            {
                var weaponData = ((OneHandedWeapon)inventoryComponent.ActiveItem).weaponComponent;
                ((OneHandedWeapon)inventoryComponent.ActiveItem).Attack();
                animator.speed = weaponData.attackSpeed;
            }
            
            /*while (fsm.currentState is OneHandAttack)
            {
                yield return null;
            }*/
            
            return null;
            if (inventoryComponent.ActiveItem)
            {
                ((OneHandedWeapon)inventoryComponent.ActiveItem).UnAttack();
            }
            animator.speed = 1;
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
