
using System.Collections;
using Controllers;
using UnityEngine;

namespace Systems
{
    public class AttackSystem : BaseSystem
    {
        private BackpackComponent _backpackComponent;
        private AttackComponent _attackComponent;
        
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _backpackComponent = owner.GetControllerComponent<BackpackComponent>();
            _attackComponent = owner.GetControllerComponent<AttackComponent>();
        }

        public override void Update()
        {
            AttackProcess();
        }

        private void AttackProcess()
        {
            if (_attackComponent.AttackProcess != null)
            {
                return;
            }
            owner.StartCoroutine(AttackProcessCO());
        }

        private IEnumerator AttackProcessCO()
        {
            Debug.Log("Attack");
            yield return new WaitForSeconds(1);

            _attackComponent = null;
        }
    }

[System.Serializable]
    public class AttackComponent : IComponent
    {
        public Coroutine AttackProcess;
    }
}
