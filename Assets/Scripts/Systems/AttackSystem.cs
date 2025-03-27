
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
            var weaponData = ((OneHandedWeapon)inventoryComponent.ActiveItem).WeaponData;
            weaponData.trailRenderer.gameObject.SetActive(true);

            Collider2D attaclCol = Physics2D.OverlapCircle((inventoryComponent.ActiveItem).transform.position, weaponData.attackDistance, weaponData.attackLayer);
            if (attaclCol != null)
            {
                
            }
            _animationStateComponent.animator.speed = weaponData.attackSpeed;
            
            while (_animationStateComponent.currentState is OneHandAttack)
            {
                yield return null;
            }
            _animationStateComponent.animator.speed = 1;
            _attackComponent.isAttack = false;
            weaponData.trailRenderer.gameObject.SetActive(false);
            _attackComponent.AttackProcess = null;
        }
        
        public void OnDrawGizmos()
        {
            if(_attackComponent.isAttack)
                Gizmos.DrawWireSphere((inventoryComponent.ActiveItem).transform.position, ((OneHandedWeapon)inventoryComponent.ActiveItem).WeaponData.attackDistance);
        }
    }
    

[System.Serializable]
    public class AttackComponent : IComponent
    {
        public Coroutine AttackProcess;
        public bool isAttack;
    }
}
