using States;
using Systems;
using UnityEngine;

namespace Controllers
{
    public class PlayerController : EntityController
    {
        public IInputProvider input;
        protected MoveSystem _moveSystem = new MoveSystem();
        private readonly JumpSystem _jumpSystem = new JumpSystem();
        private readonly InventorySystem _inventorySystem = new InventorySystem();
        private readonly SpriteFlipSystem _flipSystem = new SpriteFlipSystem();
        private readonly ColorPositioningSystem _colorPositioningSystem = new ColorPositioningSystem();
        private readonly LedgeClimbSystem _ledgeClimbSystem = new LedgeClimbSystem();
        private readonly FrictionSystem _frictionSystem = new FrictionSystem();
        private readonly FSMSystem _fsmSystem = new FSMSystem();
        private readonly DashSystem _dashSystem = new DashSystem();
        private readonly SlideSystem _slideSystem = new SlideSystem();
        private readonly SlideDashSystem _slideDashSystem = new SlideDashSystem();
        private readonly WallRunSystem _wallRunSystem = new WallRunSystem();
        private readonly HookSystem _hookSystem = new HookSystem();
        
        [SerializeField] private MoveComponent moveComponent;
        [SerializeField] private JumpComponent jumpComponent;
        [SerializeField] private AttackComponent attackComponent = new AttackComponent();
        [SerializeField] private InventoryComponent inventoryComponent = new InventoryComponent(); 
        [SerializeField] private ColorPositioningComponent colorPositioningComponent = new ColorPositioningComponent();
        [SerializeField] public WallEdgeClimbComponent wallEdgeClimbComponent = new WallEdgeClimbComponent();
        [SerializeField] public  DashComponent dashComponent= new DashComponent();
        [SerializeField] public  FsmComponent fsmComponent = new FsmComponent();
        [SerializeField] public  AnimationComponent animationComponent = new AnimationComponent();
        private readonly SpriteFlipComponent _flipComponent = new SpriteFlipComponent();
        [SerializeField] public SlideComponent slideComponent = new SlideComponent();
        [SerializeField] public WallRunComponent wallRunComponent = new WallRunComponent();
        [SerializeField] public HookComponent hookComponent = new HookComponent();
        
        public PlayerCustomizer playerCustomizer;

        private  AttackSystem _attackSystem = new AttackSystem();
        private Vector2 cachedVelocity;
        private Vector2 LateVelocity;
        
        private Vector2 MoveDirection
        {
            get
            {
                Vector2 raw = input.GetState().movementDirection;
                Vector2 result = Vector2.zero;

                result.x = Mathf.Abs(raw.x) < 0.5f ? 0f : Mathf.Sign(raw.x);
                result.y = Mathf.Abs(raw.y) < 0.5f ? 0f : Mathf.Sign(raw.y);

                return result;
            }
        }
        protected unsafe override void Awake()
        {
            input = new NavigationSystem();
            base.Awake();

        }
        protected void Start()
        {
            Subscribe();
            States();
        }
        public void EnableAllActions()
        {
            input.GetState().inputActions.Player.Move.Enable();
            input.GetState().inputActions.Player.Jump.Enable();
            input.GetState().inputActions.Player.Interact.Enable();
            input.GetState().inputActions.Player.OnDrop.Enable();
            input.GetState().inputActions.Player.Next.Enable();
            input.GetState().inputActions.Player.Previous.Enable();
            input.GetState().inputActions.Player.Attack.Enable();
            input.GetState().inputActions.Player.Dash.Enable();
            input.GetState().inputActions.Player.Slide.Enable();
            input.GetState().inputActions.Player.GrablingHook.Enable();
            input.GetState().inputActions.Player.WeaponWheel.Enable();
        }
        private void Subscribe()
        {
            EnableAllActions();
            input.GetState().inputActions.Player.Interact.started += _inventorySystem.TakeItem;
            input.GetState().inputActions.Player.OnDrop.started += _inventorySystem.ThrowItem;
            input.GetState().inputActions.Player.Jump.started += c =>
            {
                jumpComponent.isJumpButtonPressed = true;
                if(slideComponent.isCeilOpen && (jumpComponent.isGround || jumpComponent.coyotTime > 0))
                    _fsmSystem.SetState(new JumpState(this));
                else
                {
                    _jumpSystem.StartJumpBuffer();
                }
            };
            input.GetState().inputActions.Player.Jump.canceled += c =>
            {
                jumpComponent.isJumpButtonPressed = false;
                if(slideComponent.isCeilOpen && wallRunComponent.wallRunProcess == null && wallRunComponent.isJumped == false)
                    _fsmSystem.SetState(new JumpUpState(this));
            };

            input.GetState().inputActions.Player.WeaponWheel.performed += context =>
            {
                if (context.ReadValue<Vector2>().y > 0)
                    _inventorySystem.NextItem(context);
            };
            input.GetState().inputActions.Player.WeaponWheel.performed += context =>
            {
                if (context.ReadValue<Vector2>().y < 0)
                    _inventorySystem.PreviousItem(context);
            };
            input.GetState().inputActions.Player.Dash.started += c =>
            {
                if(dashComponent.allowDash && dashComponent.DashProcess == null && wallEdgeClimbComponent.EdgeStuckProcess == null )
                    _fsmSystem.SetState(new DashState(this));
                
            };
            input.GetState().inputActions.Player.Slide.started += c =>
            {
                if (jumpComponent.isGround) 
                    _fsmSystem.SetState(new SlideState(this));
            };
            
            input.GetState().inputActions.Player.GrablingHook.started += c =>
            {
                if(!slideComponent.isCeilOpen && slideComponent.SlideProcess != null)
                    return;
                _fsmSystem.SetState(new GrablingHookState(this));
            };
            /*input.GetState().inputActions.Player.Attack.started += _ => _attackSystem.Update();*/
        }
        private void Unsubscribe()
        {
            input.GetState().inputActions.Player.Interact.started -= _inventorySystem.TakeItem;
            input.GetState().inputActions.Player.OnDrop.started -= _inventorySystem.ThrowItem;
            
            input.GetState().inputActions.Player.Attack.started -= _ => (_attackSystem).OnUpdate();
        }
        private void States()
        {

            var idle = new IdleState(this);
            var walk = new WalkState(this);
            var fall = new FallState(this);
            var wallEdge = new WallLeangeClimb(this);
            var wallRun = new WallRunState(this);
            var fallUp = new FallUpState(this);
            
            _fsmSystem.AddAnyTransition(wallRun, () => _wallRunSystem.CanStartWallRun() && ((cachedVelocity.y >= 2 && Mathf.Abs(LateVelocity.x) >= 5f) || !dashComponent.allowDash)  && wallRunComponent.canWallRun && wallRunComponent.wallRunProcess == null 
                                                       && moveComponent.direction.x == transform.localScale.x && slideComponent.SlideProcess == null  && dashComponent.isDash == false && !hookComponent.IsHooked);
            _fsmSystem.AddAnyTransition(fall, () => !jumpComponent.isGround && cachedVelocity.y < -1 && wallRunComponent.wallRunProcess == null && wallEdgeClimbComponent.EdgeStuckProcess == null 
                                                    && !hookComponent.IsHooked);
            _fsmSystem.AddAnyTransition(fallUp, () => !jumpComponent.isGround && cachedVelocity.y > 1 && wallRunComponent.wallRunProcess == null && wallEdgeClimbComponent.EdgeStuckProcess == null 
                                                      && !hookComponent.IsHooked);
            _fsmSystem.AddAnyTransition(walk, () =>Mathf.Abs(cachedVelocity.x) > 1.5f && jumpComponent.isGround && Mathf.Abs(cachedVelocity.y) < 1.5f 
                                                   && !dashComponent.isDash && slideComponent.SlideProcess == null && wallRunComponent.wallRunProcess == null && !hookComponent.IsHooked);
            _fsmSystem.AddTransition(fall,wallEdge, () => _ledgeClimbSystem.CanGrabLedge(out var _, out var _));
            _fsmSystem.AddAnyTransition(idle, () => Mathf.Abs(cachedVelocity.x) <= 1.5f  && Mathf.Abs(cachedVelocity.y) < 1.5f
                                                                                         && !dashComponent.isDash && wallEdgeClimbComponent.EdgeStuckProcess == null && jumpComponent.isGround 
                                                                                         && slideComponent.SlideProcess == null && wallRunComponent.wallRunProcess == null && dashComponent.DashProcess == null 
                                                                                         && !hookComponent.IsHooked);
            
            _fsmSystem.SetState(idle);
        }

        public override void Update()
        {
            base.Update();
            _flipComponent.direction = MoveDirection;
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            moveComponent.direction = new Vector2(MoveDirection.x,moveComponent.direction.y);

            LateVelocity = cachedVelocity;
            cachedVelocity = baseFields.rb.linearVelocity;
        }

        public void LateUpdate()
        {
            _colorPositioningSystem.OnUpdate();
        }

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube((Vector2)baseFields.collider.bounds.center + Vector2.down * baseFields.collider.bounds.extents.y, jumpComponent.groundCheackSize);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
      
    }
}