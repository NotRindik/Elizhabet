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
        public SpriteFlipSystem flipSystem;
        
        private OnDemandAimSystem _aim;
        private GroundingComponent groundingComponent;
        
        private MouseAimSystem mouseAimSystem;
        
        

        protected override void OnEquip()
        {
            WeaponSystem = owner.GetControllerSystem<MeleeWeaponSystem>();
            mouseAimSystem = owner.GetControllerSystem<MouseAimSystem>();
            MeleeComponent = owner.GetControllerComponent<MeleeComponent>();
            groundingComponent = itemComponent._currentOwner.GetControllerComponent<GroundingComponent>();
            _aim = owner.GetControllerSystem<OnDemandAimSystem>();
            LungeAttackSystem = owner.GetControllerSystem<LungeAttackSystem>();
            flipSystem = itemComponent._currentOwner.GetControllerSystem<SpriteFlipSystem>();

            animSystem.OnAnimEnd += HandleAttackEnd;
            
            item.itemComponent.DestroyCondition = () => MeleeComponent.IsDamageState == false;
            
            inputComponent.input.GetState().Attack.started += OnAttackTriggered;

            attackComponent.AttackForceStopped += ForceStopped;
        }

        private void OnAttackTriggered(InputContext ctx)
        {
            if (!policy.CanTrigger(owner))
                    return;

            if(mouseAimSystem != null) mouseAimSystem.IsActive = false;
            
            if (!IsDownAttack())
            {
                if (!animSystem.BeginAttack())
                    return;
            }
            else
            {
                animSystem.BeginPogoAttack();
            }

            if (LungeAttackSystem != null && !attackComponent.IsPogo)
            {
                if (!LungeAttackSystem.TryLungeAttack(target => 
                    {
                        flipSystem.SetFacing(target.x > itemComponent.currentOwner.transform.position.x ? 1 : -1);
                        flipSystem.IsActive = false;
                            
                        _aim?.StartAimToPoint(target);

                        WeaponSystem.BeginDamage();

                        attackComponent.isAttackFrameThisFrame = true;
                        attackComponent.isAttackFrame = true;
                    }, HandleAttackEnd))
                {
                        

                    Vector2 mouseScreenPos = inputComponent.input.GetState().Point.ReadValue<Vector2>();

                    Vector2 mouseWorldPos = ContextManager.Instance.mainCamera.ScreenToWorldPoint(mouseScreenPos);
                        
                    Vector2 playerPos = itemComponent.currentOwner.transform.position;
                    flipSystem.SetFacing(mouseWorldPos.x >= playerPos.x ? 1 : -1);
                    flipSystem.IsActive = false;
                    var dir = mouseWorldPos - playerPos;
                    dir.x = Mathf.Abs(dir.x);
                        
                    _aim?.StartAimDirection(dir);

                    owner.StartCoroutine(std.Utilities.Invoke(() => WeaponSystem.BeginDamage(), 0.1f));

                    attackComponent.isAttackFrameThisFrame = true;
                    attackComponent.isAttackFrame = true;
                }
            }
            else
            {
                Vector2 mouseScreenPos = inputComponent.input.GetState().Point.ReadValue<Vector2>();

                Vector2 mouseWorldPos = ContextManager.Instance.mainCamera.ScreenToWorldPoint(mouseScreenPos);

                Vector2 playerBefore = itemComponent.currentOwner.transform.position;
                    
                flipSystem.SetFacing(mouseWorldPos.x >= playerBefore.x ? 1 : -1);
                
                
                _aim?.StartAimToCursor();
                
                owner.StartCoroutine(std.Utilities.Invoke(() => WeaponSystem.BeginDamage(), 0.04f));
            }

            fsmSystem.SetState(new AttackState(item.itemComponent.currentOwner));
        }

        public void ForceStopped()
        {
            animSystem.EndAttack();
            WeaponSystem.EndDamage();
            _aim?.StopAim();
            flipSystem.IsActive = true;
        }
        

        public bool IsDownAttack()
        {
            Vector2 mouseScreenPos = inputComponent.input.GetState().Point.ReadValue<Vector2>();
            Camera cam = ContextManager.Instance.mainCamera;

            float playerBottomY = groundingComponent.rayOrigins[1].y;

            float playerScreenY = cam.WorldToScreenPoint(new Vector3(0f, playerBottomY, 0f)).y;

            const float playerThreshold = 0.15f;
            const float cursorThreshold = 0.08f;

            if (playerScreenY < Screen.height * playerThreshold)
                return mouseScreenPos.y < Screen.height * cursorThreshold;

            Vector3 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);

            float dx = Mathf.Abs(mouseWorldPos.x - groundingComponent.rayOrigins[1].x);
            float dy = playerBottomY - mouseWorldPos.y;

            const float downThreshold = 0.1f;
            const float verticalBias = 1.2f;

            return dy > downThreshold && dy > dx * verticalBias;
        }
        
        private void HandleAttackEnd()
        {
            animSystem.EndAttack();
            WeaponSystem.EndDamage();
            _aim?.StopAim();
            
            if(mouseAimSystem != null) mouseAimSystem.IsActive = true;
            
            flipSystem.IsActive = true;
            attackComponent.isAttackFrame = true; 
            attackComponent.isAttackAnim = false;
        }

        protected override void OnUnequip()
        {
            inputComponent.input.GetState().Attack.started -= OnAttackTriggered;
            groundingComponent = null;
            animSystem.OnAnimEnd -= HandleAttackEnd;
            attackComponent.OnAttackEnd -= HandleAttackEnd;
            attackComponent.AttackForceStopped -= ForceStopped;;
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
            
            private bool _isAiming;
            private float _angleOffset;
            
            private Vector2 _aimPoint;
            private Vector2 _aimDirection;
            private bool _aimAtPoint;
            private bool _aimAtCursor;
            
            public override void Initialize(AbstractEntity owner)
            {
                base.Initialize(owner);
                _item = (Item)owner;
                _item.OnTake += HandleEquip;
                _item.OnReferenceClean += OnRefClean;
                _item.OnLateUpdate += Update;
            }
            
            public override void OnUpdate()
            {
                if (_isAiming)
                    ApplyCurrentAim();
            }
            
            public void StartAimToCursor(float angleOffset = 0f)
            {
                _angleOffset = angleOffset;

                _isAiming = true;
                _aimAtPoint = false;
                _aimAtCursor = true;

                ApplyCurrentAim();
            }
            
            public void StartAimDirection(Vector2 worldDir, float angleOffset = 0f)
            {
                
                _aimDirection = worldDir;
                _angleOffset = angleOffset;

                _isAiming = true;
                _aimAtPoint = false;
                _aimAtCursor = false;

                ApplyCurrentAim();
            }
            
            public void StartAimToPoint(Vector2 point, float angleOffset = 0f)
            {
                _aimPoint = point;
                _angleOffset = angleOffset;

                _isAiming = true;
                _aimAtPoint = true;
                _aimAtCursor = false;

                ApplyCurrentAim();
            }
            
            public void StopAim()
            {
                _isAiming = false;
                _aimAtPoint = false;
                _aimAtCursor = false;

                ResetAngle();
            }
            
            private void ApplyCurrentAim()
            {
                if (_aimAtPoint)
                {
                    Vector2 dir = _aimPoint - (Vector2)_player.mono.transform.position;
                    dir.x = Mathf.Abs(dir.x);
                    ApplyAngleToDirection(dir, _angleOffset);
                }
                else if (_aimAtCursor)
                {
                    Vector3 screenPos = _pointPos;

                    Camera cam = ContextManager.Instance.mainCamera;

                    screenPos.z = Mathf.Abs(cam.transform.position.z - _player.mono.transform.position.z);

                    Vector3 worldPos = cam.ScreenToWorldPoint(screenPos);

                    Vector2 dir = worldPos - _player.mono.transform.position;
                    dir.x = Mathf.Abs(dir.x);
                    ApplyAngleToDirection(dir, _angleOffset);
                }
                else if (_isAiming)
                {
                    ApplyAngleToDirection(_aimDirection, _angleOffset);
                }
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

                float parentZ = _hands.right.parent.eulerAngles.z;

                float localAngle = worldAngle - parentZ;
                

                _hands.right.localRotation = Quaternion.Euler(0f, 0f, localAngle + angleOffset);
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
                _item.OnLateUpdate -= Update;
            }
        }
}