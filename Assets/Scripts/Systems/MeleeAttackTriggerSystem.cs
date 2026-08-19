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
        public LungeAttackSystem LungeAttackSystem;

        private Action<InputContext> _handler;
        private OnDemandAimSystem _aim;
        

        protected override void OnEquip()
        {
            WeaponSystem = owner.GetControllerSystem<MeleeWeaponSystem>();
            MeleeComponent = owner.GetControllerComponent<MeleeComponent>();
            _aim = owner.GetControllerSystem<OnDemandAimSystem>();
            LungeAttackSystem = owner.GetControllerSystem<LungeAttackSystem>();
            var flipSystem = itemComponent._currentOwner.GetControllerSystem<SpriteFlipSystem>();
            
            _handler = _ =>
            {
                if (!policy.CanTrigger(owner))
                    return;

                if (!IsDownAttack())
                {
                    if (!animSystem.BeginAttack())
                    {
                        return;
                    }
                }
                else
                {
                    animSystem.BeginPogoAttack();
                }
                
                if (LungeAttackSystem != null)
                {
                    if (!LungeAttackSystem.TryLungeAttack(
                            (target) =>
                            {
                                flipSystem.SetFacing(target.position.x  > itemComponent.currentOwner.transform.position.x ? 1 : -1);
                                _aim?.ApplyAngleToPoint(target.position);
                                WeaponSystem.BeginDamage();
                                attackComponent.isAttackFrameThisFrame = true; 
                                attackComponent.isAttackFrame = true; 
                            }, HandleAttackEnd))
                    {
                        Vector2 mouseScreenPos = inputComponent.input.GetState().Point.ReadValue<Vector2>();
                        Vector2 mouseWorldPos = ContextManager.Instance.mainCamera.ScreenToWorldPoint(mouseScreenPos);
                        flipSystem.SetFacing( mouseWorldPos.x >= itemComponent.currentOwner.transform.position.x ? 1 : -1);
                        _aim?.ApplyAngleToCursor();
                        owner.StartCoroutine(std.Utilities.Invoke(() => WeaponSystem.BeginDamage(),0.1f));
                        attackComponent.isAttackFrameThisFrame = true; 
                        attackComponent.isAttackFrame = true; 
                    }
                }
                else
                {
                    Vector2 mouseScreenPos = inputComponent.input.GetState().Point.ReadValue<Vector2>();
                    Vector2 mouseWorldPos = ContextManager.Instance.mainCamera.ScreenToWorldPoint(mouseScreenPos);

                    flipSystem.SetFacing( mouseWorldPos.x >= itemComponent.currentOwner.transform.position.x ? 1 : -1 );
                    _aim?.ApplyAngleToCursor();
                    owner.StartCoroutine(std.Utilities.Invoke(() => WeaponSystem.BeginDamage(),0.1f));
                }
                fsmSystem.SetState(new AttackState(item.itemComponent.currentOwner));
                
            };

            animSystem.OnAnimEnd += HandleAttackEnd;
            
            item.itemComponent.DestroyCondition = () => MeleeComponent.IsDamageState == false;
            
            inputComponent.input.GetState().Attack.started += _handler;
        }

        public bool IsDownAttack()
        {
            Vector2 mouseScreenPos = inputComponent.input.GetState().Point.ReadValue<Vector2>();
            Camera cam = ContextManager.Instance.mainCamera;

            var grounding = itemComponent._currentOwner
                .GetControllerComponent<GroundingComponent>();

            float playerBottomY = grounding.origin.y;

            float playerScreenY = cam.WorldToScreenPoint(
                new Vector3(0f, playerBottomY, 0f)
            ).y;

            const float playerThreshold = 0.15f;
            const float cursorThreshold = 0.08f;

            if (playerScreenY < Screen.height * playerThreshold)
                return mouseScreenPos.y < Screen.height * cursorThreshold;

            Vector3 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);

            float dx = Mathf.Abs(mouseWorldPos.x - grounding.origin.x);
            float dy = playerBottomY - mouseWorldPos.y;

            const float downThreshold = 0.1f;
            const float verticalBias = 1.2f;

            return dy > downThreshold && dy > dx * verticalBias;
        }

        private void HandleAttackEnd()
        {
            Debug.Log("END");
            animSystem.EndAttack();
            WeaponSystem.EndDamage();
            _aim?.ResetAngle();
            
            attackComponent.isAttackFrame = true; 
            attackComponent.isAttackAnim = false;
        }

        protected override void OnUnequip()
        {
            inputComponent.input.GetState().Attack.started -= _handler;
            _handler = null;
            animSystem.OnAnimEnd -= HandleAttackEnd;
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
            
            public void ApplyAngleToDirection(Vector2 worldDir, float angleOffset = 0f)
            {
                if (_hands == null || worldDir.sqrMagnitude < 0.0001f)
                    return;

                float worldAngle = Mathf.Atan2(worldDir.y, worldDir.x) * Mathf.Rad2Deg;

                bool flipped = _player.mono.transform.IsFacingLeft();
                float localAngle = flipped ? 180f - worldAngle : worldAngle;
                float signedOffset = flipped ? -angleOffset : angleOffset;

                _hands.right.localRotation = Quaternion.Euler(0f, 0f, localAngle + signedOffset);
            }
            
            public void ApplyAngleToPoint(Vector2 worldPoint, float angleOffset = 0f)
            {
                if (_player == null) return;
                Vector2 dir = worldPoint - (Vector2)_player.mono.transform.position;
                ApplyAngleToDirection(dir, angleOffset);
            }
            
            public void ApplyAngleToCursor(float angleOffset = 0f)
            {
                if (_hands == null)
                    return;

                Vector3 screenPos = _pointPos;
                screenPos.z = Mathf.Abs(ContextManager.Instance.mainCamera.transform.position.z);

                Vector3 worldPos = ContextManager.Instance.mainCamera.ScreenToWorldPoint(screenPos);
                Vector2 dir = worldPos - _player.mono.transform.position;

                ApplyAngleToDirection(dir, angleOffset);
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