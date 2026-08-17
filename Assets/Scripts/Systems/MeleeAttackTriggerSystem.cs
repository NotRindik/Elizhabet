using System;
using Controllers;
using States;
using UnityEngine;

namespace Systems
{
    public class MeleeAttackTriggerSystem : AttackTriggerSystem
    {
        [SerializeReference, SubclassSelector]
        public IAttackTriggerPolicy policy = new ComboAttackPolicy();

        public MeleeWeaponSystem WeaponSystem;
        public MeleeComponent MeleeComponent;

        private Action<InputContext> _handler;
        private OnDemandAimSystem _aim;
        
        protected override void OnEquip()
        {
            WeaponSystem = owner.GetControllerSystem<MeleeWeaponSystem>();
            MeleeComponent = owner.GetControllerComponent<MeleeComponent>();
            _aim = owner.GetControllerSystem<OnDemandAimSystem>();

            _handler = _ =>
            {
                if (!policy.CanTrigger(owner))
                    return;

                if (!animSystem.BeginAttack())
                    return;

                _aim?.ApplyAngleToCursor();

                owner.StartCoroutine(std.Utilities.Invoke(() => WeaponSystem.BeginDamage(),0.1f));
                fsmSystem.SetState(new AttackState(item.itemComponent.currentOwner));
                attackComponent.isAttackAnim = true;
            };
            
            item.itemComponent.DestroyCondition = () => MeleeComponent.IsDamageState == false;
            
            inputComponent.input.GetState().Attack.started += _handler;
            attackComponent.OnAttackEnd += HandleAttackEnd;
        }

        private void HandleAttackEnd()
        {
            animSystem.EndAttack();
            WeaponSystem.EndDamage();
            _aim?.ResetAngle();
            attackComponent.isAttackAnim = false;
        }

        protected override void OnUnequip()
        {
            inputComponent.input.GetState().Attack.started -= _handler;
            _handler = null;
            attackComponent.OnAttackEnd -= HandleAttackEnd;
        }
    }
    
    
public class OnDemandAimSystem : BaseSystem, System.IDisposable
        {
            private HandRotatorsComponent _hands;
            private AbstractEntity _player;
            private Item _item;

            private Vector2 _pointPos;
            private Action<InputContext> _pointHandler;
            private Quaternion _restRotation;

            public override void Initialize(AbstractEntity owner)
            {
                base.Initialize(owner);
                _item = (Item)owner;
                _item.OnTake += HandleEquip;
                _item.OnReferenceClean += OnRefClean;
            }

            private void HandleEquip(AbstractEntity playerOwner)
            {
                _player = playerOwner;
                _hands = playerOwner.GetControllerComponent<HandRotatorsComponent>();
                _restRotation = _hands.right.localRotation; 

                _pointPos = _item.inputComponent.input.GetState().Point.ReadValue<Vector2>();
                _pointHandler = c => _pointPos = c.ReadValue<Vector2>();
                _item.inputComponent.input.GetState().Point.performed += _pointHandler;
            }

            public void ApplyAngleToCursor(float angleOffset = 0f)
            {
                if (_hands == null) 
                    return;

                Vector3 screenPos = _pointPos;
                screenPos.z = Mathf.Abs(ContextManager.Instance.mainCamera.transform.position.z);

                Vector3 worldPos = ContextManager.Instance.mainCamera.ScreenToWorldPoint(screenPos);

                Vector2 dir = worldPos - _player.mono.transform.position;

                float worldAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                bool flipped = _player.mono.transform.IsFacingLeft();
                float localAngle = flipped ? 180f - worldAngle : worldAngle;
                float signedOffset = flipped ? -angleOffset : angleOffset;

                _hands.right.localRotation = Quaternion.Euler(0f, 0f, localAngle + signedOffset);
            }
            
            public void ResetAngle()
            {
                if (_hands == null) return;
                _hands.right.localRotation = _restRotation;
            }

            public void OnRefClean()
            {
                if (_pointHandler != null)
                    _item.inputComponent.input.GetState().Point.performed -= _pointHandler;
            }

            public void Dispose()
            {
                _item.OnTake -= HandleEquip;
            }
        }
}