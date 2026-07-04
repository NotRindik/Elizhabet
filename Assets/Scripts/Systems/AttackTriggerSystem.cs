using System;
using UnityEngine;


namespace Systems
{
    public abstract class AttackTriggerSystem : BaseSystem, IDisposable
    {
        protected AttackComponent attackComponent;
        protected AnimationComponentsComposer animationComponent;
        protected FSMSystem fsmSystem;
        protected InputComponent inputComponent;
        protected AttackAnimationSystem animSystem;

        protected Item item;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            item = (Item)owner;
            animSystem = owner.GetControllerSystem<AttackAnimationSystem>();
            item.OnTake += HandleEquip;
        }

        private void HandleEquip()
        {
            var playerOwner = item.itemComponent.currentOwner;
            attackComponent = playerOwner.GetControllerComponent<AttackComponent>();
            animationComponent = playerOwner.GetControllerComponent<AnimationComponentsComposer>();
            fsmSystem = playerOwner.GetControllerSystem<FSMSystem>();
            inputComponent = item.inputComponent;
            OnEquip();
        }

        public void Dispose()
        {
            if (inputComponent == null)
                return;

            OnUnequip();
            attackComponent = null;
            animationComponent = null;
            fsmSystem = null;
            inputComponent = null;
        }

        protected abstract void OnEquip();
        protected abstract void OnUnequip();
    }


    public interface IAttackTriggerPolicy
    {
        bool CanTrigger(AttackComponent attack);
        void OnTriggered(AttackAnimationSystem animSystem);
    }

    public class ComboAttackPolicy : IAttackTriggerPolicy
    {
        public bool CanTrigger(AttackComponent attack) => attack.canAttack;
        public void OnTriggered(AttackAnimationSystem animSystem) => animSystem.PlayNextAttack();
    }
}