
using System.Collections;
using Controllers;
using UnityEngine;

namespace Systems
{
    public class AttackSystem : BaseSystem
    {
        private InventoryComponent inventoryComponent;
        private AttackComponent _attackComponent;
        
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            inventoryComponent = owner.GetControllerComponent<InventoryComponent>();
            _attackComponent = owner.GetControllerComponent<AttackComponent>();
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
            Debug.Log("Attack");
            ((OneHandedWeapon)inventoryComponent.ActiveItem).WeaponData.trailRenderer.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.8f);
            ((OneHandedWeapon)inventoryComponent.ActiveItem).WeaponData.trailRenderer.gameObject.SetActive(false);
            _attackComponent.AttackProcess = null;
        }
    }

[System.Serializable]
    public class AttackComponent : IComponent
    {
        public Coroutine AttackProcess;
    }
}
