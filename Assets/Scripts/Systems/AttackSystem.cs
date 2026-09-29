
using System;
using std;


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

            _attackComponent.AttackCondition = AllowAttack;
            
            owner.OnFixedUpdate += Update;
        }
        
        public void ForceStopAttack()
        {
            _attackComponent.isAttackFrame = false;
            _attackComponent.isAttackFrameThisFrame = false;
            _attackComponent.isAttackAnim = false;
            _attackComponent.AttackForceStopped?.Invoke();
        }

        public virtual bool AllowAttack()
        {
            if(!isActive)
                return false;
            
            return _slideComponent.SlideProcess == null 
                   && _wallRunComponent.wallRunProcess == null 
                   && _wallEdgeClimbComponent.EdgeStuckProcess == null 
                   && !_hookComponent.isHooked
                   && !_itemThrow.isCharging 
                   && !_itemThrow.isThrowing 
                   && !_attackComponent.isAttackAnim 
                   && _fsm.currentState != nameof(TakeHitState);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            ForceStopAttack();
        }
        public void Dispose()
        {
            _attackComponent.AttackCondition = null;
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
                if(value)
                    OnAttackStart?.Invoke();
                else
                {
                    OnAttackEnd?.Invoke();
                }
            }
        }

        public Func<bool> AttackCondition;
        public bool canAttack { get => AttackCondition.Invoke();}
        public bool isAttackFrameThisFrame;

        public bool isAttackAnim;

        public Action OnAttackStart;
        public Action OnAttackEnd;
        public Action AttackForceStopped;

        public Action OnPlayerTakeControlOfHand;
        public Action OnPlayerReleseControlOfHand;

        public bool IsPogo { get; set; }
        public ObservableList<IntPtr> damageModifire = new();
        public bool HandAutorative { get; private set; } = false;

        public void SetPlayersTakeControl(bool value)
        {
            if (HandAutorative == value)
                return;

            HandAutorative = value;

            if (value)
                OnPlayerTakeControlOfHand?.Invoke();
            else
                OnPlayerReleseControlOfHand?.Invoke();
        }

        public void SetAttackFrame(bool val)
        {
            isAttackFrame = val;
        }
    }
}
