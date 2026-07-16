
using System;
using std;
using UnityEngine;
using UnityEngine.Events;

namespace Systems
{
    public class AttackSystem : BaseSystem,IDisposable
    {
        protected AttackComponent _attackComponent;
        protected ItemThrowComponent _itemThrow;

        protected AnimationComponentsComposer _composer;

        private SlideComponent _slideComponent;
        private WallRunComponent _wallRunComponent;
        private WallEdgeClimbComponent _wallEdgeClimbComponent;
        private HookComponent _hookComponent;
        private FsmComponent _fsm;
        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _attackComponent = owner.GetControllerComponent<AttackComponent>();
            
            _slideComponent = owner.GetControllerComponent<SlideComponent>();
            _wallRunComponent = owner.GetControllerComponent<WallRunComponent>();
            _wallEdgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
            _hookComponent = owner.GetControllerComponent<HookComponent>();
            _itemThrow = owner.GetControllerComponent<ItemThrowComponent>();
            _fsm = owner.GetControllerComponent<FsmComponent>();
            
            
            base.owner.OnUpdate += AllowAttack;
            owner.OnFixedUpdate += Update;
        }
        
        public void ForceStopAttack()
        {
            _attackComponent.isAttackFrame = false;
            _attackComponent.isAttackFrameThisFrame = false;
            _attackComponent.isAttackAnim = false;
        }

        public virtual void AllowAttack()
        {
            if(!isActive)
                return;
            
            _attackComponent.canAttack = _slideComponent.SlideProcess == null &&
                                         _wallRunComponent.wallRunProcess == null &&
                                         _wallEdgeClimbComponent.EdgeStuckProcess == null && !_hookComponent.isHooked
                                          && !_itemThrow.isCharging && !_attackComponent.isAttackAnim && _fsm.currentState != nameof(TakeHitState);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            _attackComponent.canAttack = false;
            ForceStopAttack();
        }
        public void Dispose()
        {
            base.owner.OnUpdate -= AllowAttack;
            owner.OnFixedUpdate -= Update;
            ActiveStateChange = null;
        }
    }
    

[System.Serializable]
    public class AttackComponent : IComponent
    {
        private bool _isAttackFrame;
        public bool isAttackFrame
        {
            get => _isAttackFrame;
            set
            {
                _isAttackFrame = value;
                if(value == true)
                    OnAttackStart?.Invoke();
                else
                {
                    OnAttackEnd?.Invoke();
                }
            }
        }
        public bool canAttack;
        public bool isAttackFrameThisFrame;

        public bool isAttackAnim;

        public Action OnAttackStart;
        public Action OnAttackEnd;
        public bool IsPogo { get; set; }
        public ObservableList<IntPtr> damageModifire = new(); //Пока что не работает в будущем поправлю

        public void SetAttackFrame(bool val)
        {
            isAttackFrame = val;
        }
    }
}
