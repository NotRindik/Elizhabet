using System;
using States;
using UnityEngine;

namespace Systems
{
    [Serializable]
    public class MeleeAttackTriggerSystem : AttackTriggerSystem
    {
        [SerializeReference, SubclassSelector]
        public IAttackTriggerPolicy policy;

        private Action<InputContext> _handler;

        protected override void OnEquip()
        {
            _handler = _ =>
            {
                if (!policy.CanTrigger(attackComponent))
                    return;

                policy.OnTriggered(animSystem);

                fsmSystem.SetState(new AttackState(item.itemComponent.currentOwner));
                attackComponent.isAttackAnim = true;
            };

            inputComponent.input.GetState().Attack.started += _handler;
        }

        protected override void OnUnequip()
        {
            inputComponent.input.GetState().Attack.started -= _handler;
        }
    }
}