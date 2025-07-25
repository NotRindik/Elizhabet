using States;
using Systems;
using UnityEngine;
using UnityEngine.Serialization;

namespace Controllers
{
    public class PlayerController : EntityController
    {
        public IInputProvider input = new PlayerSourceInput();
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
        private readonly GroundingSystem _groundingSystem = new GroundingSystem();
        private readonly PlatformSystem _platformSystem = new PlatformSystem();
        private readonly ArmorSystem _armorSystem = new ArmorSystem();
        
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
        [SerializeField] public GroundingComponent groundingComponent;
        [SerializeField] public PlatformComponent platformComponent;
        [SerializeField] public ParticleComponent particleComponent;
        [SerializeField] public ArmourComponent armourComponent = new ArmourComponent();


        public SpriteSynchronizer spriteSynchronizer;

        private  AttackSystem _attackSystem = new AttackSystem();
        private Vector2 cachedVelocity;
        private Vector2 LateVelocity;

        private Vector2 moveDirection;
        
        private Vector2 MoveDirection
        {
            get
            {
                Vector2 raw = moveDirection;
                Vector2 result = Vector2.zero;

                result.x = Mathf.Abs(raw.x) < 0.2f ? 0f : Mathf.Sign(raw.x);
                result.y = Mathf.Abs(raw.y) < 0.2f ? 0f : Mathf.Sign(raw.y);

                return result;
            }
        }
        
        protected void Start()
        {
            Subscribe();    
            print("spawned");
            States();
        }
        private void Subscribe()
        {

            input.GetState().Interact.started += c =>
            {
                if (attackComponent.isAttackAnim == false)
                    _inventorySystem.TakeItem();
            };
            input.GetState().OnDrop.started += c =>
            {
                if (attackComponent.isAttackAnim == false)
                    _inventorySystem.ThrowItem();
            };
            input.GetState().Move.performed += c => moveDirection = c;
            input.GetState().Move.canceled += c => moveDirection = c;

            input.GetState().Jump.started += c =>
            {
                
                if(slideComponent.isCeilOpen && (groundingComponent.isGround || jumpComponent.coyotTime > 0) && attackComponent.isAttackAnim == false 
                   && wallEdgeClimbComponent.EdgeStuckProcess == null)
                    _fsmSystem.SetState(new JumpState(this));
                else
                {
                    _jumpSystem.StartJumpBuffer();
                }
            };
            input.GetState().Jump.canceled += c =>
            {
                if(slideComponent.isCeilOpen && wallRunComponent.wallRunProcess == null && wallRunComponent.isJumped == false && attackComponent.isAttackAnim == false && wallEdgeClimbComponent.EdgeStuckProcess == null)
                    _fsmSystem.SetState(new JumpUpState(this));
            };

            input.GetState().WeaponWheel.started += context =>
            {
                if(attackComponent.isAttackAnim != false)
                    return;
                if (context.y > 0)
                    _inventorySystem.NextItem();
                else if (context.y < 0)
                    _inventorySystem.PreviousItem();
            };
            input.GetState().Dash.started += c =>
            {
                if(dashComponent.allowDash && dashComponent.DashProcess == null && wallEdgeClimbComponent.EdgeStuckProcess == null && attackComponent.isAttackAnim == false)
                    _fsmSystem.SetState(new DashState(this));
                
            };
            input.GetState().Slide.started += c =>
            {
                if (attackComponent.isAttackAnim == false) 
                    _fsmSystem.SetState(new SlideState(this));
            };
            
            input.GetState().GrablingHook.started += c =>
            {
                if(!slideComponent.isCeilOpen && slideComponent.SlideProcess != null && attackComponent.isAttackAnim)
                    return;
                _fsmSystem.SetState(new GrablingHookState(this));
            };

            input.GetState().Move.performed += c =>
            {
                if (c.y < -0.7f)
                {
                    _platformSystem.Update();
                }
            };
        }
        private void Unsubscribe()
        {
        }
        private void States()
        {

            var idle = new IdleState(this);
            var walk = new WalkState(this);
            var fall = new FallState(this);
            var wallEdge = new WallLeangeClimb(this);
            var wallRun = new WallRunState(this);
            var fallUp = new FallUpState(this);
            
            _fsmSystem.AddAnyTransition(wallRun, () => _wallRunSystem.CanStartWallRun() && ((cachedVelocity.y >= 2 && Mathf.Abs(LateVelocity.x) >= 4.2f) || !dashComponent.allowDash)  && wallRunComponent.canWallRun && wallRunComponent.wallRunProcess == null 
                                                               && moveComponent.direction.x == transform.localScale.x && slideComponent.SlideProcess == null  && dashComponent.isDash == false && !hookComponent.isHooked&& attackComponent.isAttackAnim == false);
            _fsmSystem.AddAnyTransition(fall, () => !groundingComponent.isGround && cachedVelocity.y < -1 && wallRunComponent.wallRunProcess == null && wallEdgeClimbComponent.EdgeStuckProcess == null 
                                                    && !hookComponent.isHooked&& attackComponent.isAttackAnim == false && slideComponent.SlideProcess == null);
            _fsmSystem.AddAnyTransition(fallUp, () => !groundingComponent.isGround && cachedVelocity.y > 1 && wallRunComponent.wallRunProcess == null && wallEdgeClimbComponent.EdgeStuckProcess == null 
                                                      && !hookComponent.isHooked&& attackComponent.isAttackAnim == false && slideComponent.SlideProcess == null );

            _fsmSystem.AddAnyTransition(walk, () =>Mathf.Abs(cachedVelocity.x) > 1.5f && groundingComponent.isGround && Mathf.Abs(cachedVelocity.y) < 1.5f 
                                                   && !dashComponent.isDash && slideComponent.SlideProcess == null && wallRunComponent.wallRunProcess == null && !hookComponent.isHooked && attackComponent.isAttackAnim == false);
            _fsmSystem.AddTransition(fallUp,wallEdge, () => _ledgeClimbSystem.CanGrabLedge(out var _, out var _) && attackComponent.isAttackAnim == false && slideComponent.SlideProcess == null);
            _fsmSystem.AddTransition(fall,wallEdge, () => _ledgeClimbSystem.CanGrabLedge(out var _, out var _) && attackComponent.isAttackAnim == false && slideComponent.SlideProcess == null);

            _fsmSystem.AddAnyTransition(idle, () => Mathf.Abs(cachedVelocity.x) <= 1.5f  && Mathf.Abs(cachedVelocity.y) < 1.5f
                                                                                         && !dashComponent.isDash && wallEdgeClimbComponent.EdgeStuckProcess == null && groundingComponent.isGround 
                                                                                         && slideComponent.SlideProcess == null && wallRunComponent.wallRunProcess == null && dashComponent.DashProcess == null 
                                                                                         && !hookComponent.isHooked && attackComponent.isAttackAnim == false);
            
            _fsmSystem.SetState(idle);
        }

        public override void Update()
        {
            base.Update();
            _flipComponent.direction = MoveDirection;
            _colorPositioningSystem.OnUpdate();
            moveComponent.direction = new Vector2(MoveDirection.x,moveComponent.direction.y);
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();

            LateVelocity = cachedVelocity;
            cachedVelocity = baseFields.rb.linearVelocity;
        }

        public override void LateUpdate()
        {
            base.LateUpdate();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            Unsubscribe();
        }
        
        public void EnterAttackFrame()
        {
            attackComponent.isAttackFrame = true;
            attackComponent.isAttackFrameThisFrame = true;
        }
        public void ExitAttackFrame()
        {
            attackComponent.isAttackFrame = false;
        }
      
    }
}