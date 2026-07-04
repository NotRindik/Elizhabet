using System;
using UnityEngine;


namespace Systems
{
    public abstract class AttackTriggerSystem : BaseSystem
    {
        protected AttackComponent attackComponent;
        protected FSMSystem fsmSystem;
        protected InputComponent inputComponent;
        protected AttackAnimationSystem animSystem;

        protected Item item;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            item = (Item)owner;
            animSystem = owner.GetControllerSystem<AttackAnimationSystem>();
            item.OnReferenceClean += ReferenceClean;
            item.OnTake += HandleEquip;
        }

        private void HandleEquip(AbstractEntity playerOwner)
        {
            attackComponent = playerOwner.GetControllerComponent<AttackComponent>();
            fsmSystem = playerOwner.GetControllerSystem<FSMSystem>();
            inputComponent = item.inputComponent;
            OnEquip();
        }
        
        private void ReferenceClean()
        {
            OnUnequip();
            attackComponent = null;
            fsmSystem = null;
            inputComponent = null;
        }

        protected abstract void OnEquip();
        protected abstract void OnUnequip();
    }


    public interface IAttackTriggerPolicy
    {
        bool CanTrigger(AbstractEntity attackingItem);
    }

    public class ComboAttackPolicy : IAttackTriggerPolicy
    {
        public bool CanTrigger(AbstractEntity attackingItem) => attackingItem.GetControllerComponent<ItemComponent>().currentOwner.GetControllerComponent<AttackComponent>().canAttack;
    }
}