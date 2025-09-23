using Assets.Scripts.Systems;
using States;
using Systems;
using UnityEngine;

namespace Controllers
{
    public class PlayerController : EntityController
    {
        public IInputProvider input = new PlayerSourceInput();
        protected MoveSystem _moveSystem = new MoveSystem();
        private JumpSystem _jumpSystem = new JumpSystem();
        private InventorySystem _inventorySystem = new InventorySystem();
        private SpriteFlipSystem _flipSystem = new SpriteFlipSystem();
        private ColorPositioningSystem _colorPositioningSystem = new ColorPositioningSystem();
        private LedgeClimbSystem _ledgeClimbSystem = new LedgeClimbSystem();
        private FrictionSystem _frictionSystem = new FrictionSystem();
        private FSMSystem _fsmSystem = new FSMSystem();
        private DashSystem _dashSystem = new DashSystem();
        private SlideSystem _slideSystem = new SlideSystem();
        private SlideDashSystem _slideDashSystem = new SlideDashSystem();
        private WallRunSystem _wallRunSystem = new WallRunSystem();
        private HookSystem _hookSystem = new HookSystem();
        private GroundingSystem _groundingSystem = new GroundingSystem();
        private PlatformSystem _platformSystem = new PlatformSystem();
        private ArmorSystem _armorSystem = new ArmorSystem();
        private AnimationComposerSystem animationComposerSystem = new AnimationComposerSystem();
        private StickyHandsSystem _stickyHandsSystem = new StickyHandsSystem();
        private HandsRotatoningSystem handsRotatoningSystem = new HandsRotatoningSystem();
        private ManaSystem _manaSystem = new ManaSystem();
        private ArmourProtectionSystem _armourProtectionSystem = new ArmourProtectionSystem();
        private ModificatorsSystem _modsSystem = new ModificatorsSystem();
        private GravityScalerSystem _gravityScalerSystem = new GravityScalerSystem();

        [Header("Moving")]
        public MoveComponent moveComponent;
        public JumpComponent jumpComponent;
        [Space]
        public AttackComponent attackComponent = new AttackComponent();
        public InventoryComponent inventoryComponent = new InventoryComponent();
        [Space]
        public ColorPositioningComponent colorPositioningComponent = new ColorPositioningComponent();
        [Space]
        public WallEdgeClimbComponent wallEdgeClimbComponent = new WallEdgeClimbComponent();
        public DashComponent dashComponent= new DashComponent();
        [Space]
        public FsmComponent fsmComponent = new FsmComponent();
        public AnimationComponentsComposer animationComponent = new AnimationComponentsComposer();
        public SpriteFlipComponent _flipComponent = new SpriteFlipComponent();
        public SlideComponent slideComponent = new SlideComponent();
        public WallRunComponent wallRunComponent = new WallRunComponent();
        public HookComponent hookComponent = new HookComponent();
        public GroundingComponent groundingComponent;
        public PlatformComponent platformComponent;
        public ParticleComponent particleComponent;
        public ArmourComponent armourComponent = new ArmourComponent();
        public StickyHandsComponent stickyHandsComponent = new StickyHandsComponent();
        public HandsRotatoningComponent handsRotatoningComponent = new HandsRotatoningComponent();
        public ManaComponent manaComponent = new ManaComponent();
        public ProtectionComponent protectionComponent = new ProtectionComponent();
        public ModificatorsComponent modsComponent = new ModificatorsComponent();
        public GravityScalerComponent gravityScalerComponent = new GravityScalerComponent();
        [Space]
        public RendererCollection spriteSynchronizer = new RendererCollection();
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
            States();
            ActiveSkills();
        }

        public void ActiveSkills()
        {
            _ledgeClimbSystem.IsActive = true;
            _dashSystem.IsActive = true;
            _slideSystem.IsActive = true;
            _slideDashSystem.IsActive = true;
            _wallRunSystem.IsActive = false;
            _hookSystem.IsActive = true;
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
                
                if(slideComponent.isCeilOpen && (groundingComponent.isGround || jumpComponent.coyotTime > 0)
                   && wallEdgeClimbComponent.EdgeStuckProcess == null)
                    _fsmSystem.SetState(new JumpState(this));
                else
                {
                    _jumpSystem.StartJumpBuffer();
                }
            };
            input.GetState().Jump.canceled += c =>
            {
                if(slideComponent.isCeilOpen && wallRunComponent.wallRunProcess == null && wallRunComponent.isJumped == false && wallEdgeClimbComponent.EdgeStuckProcess == null)
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
                if (attackComponent.isAttackAnim == false && wallRunComponent.wallRunProcess == null && wallEdgeClimbComponent.EdgeStuckProcess == null) 
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
                                                               && moveComponent.direction.x == transform.localScale.x && attackComponent.isAttackAnim == false && slideComponent.SlideProcess == null  
                                                               && dashComponent.isDash == false && !hookComponent.isHooked&& attackComponent.isAttackAnim == false 
                                                               && wallEdgeClimbComponent.EdgeStuckProcess == null);
            _fsmSystem.AddAnyTransition(fall, () => !groundingComponent.isGround && cachedVelocity.y < -1 && wallRunComponent.wallRunProcess == null && wallEdgeClimbComponent.EdgeStuckProcess == null 
                                                    && !hookComponent.isHooked && slideComponent.SlideProcess == null);
            _fsmSystem.AddAnyTransition(fallUp, () => !groundingComponent.isGround && cachedVelocity.y > 1 && wallRunComponent.wallRunProcess == null && wallEdgeClimbComponent.EdgeStuckProcess == null 
                                                      && !hookComponent.isHooked&& slideComponent.SlideProcess == null );

            _fsmSystem.AddAnyTransition(walk, () =>Mathf.Abs(cachedVelocity.x) > 0.8f && groundingComponent.isGround && Mathf.Abs(cachedVelocity.y) < 1.5f 
                                                   && !dashComponent.isDash && slideComponent.SlideProcess == null && wallRunComponent.wallRunProcess == null && !hookComponent.isHooked );
            _fsmSystem.AddTransition(fallUp,wallEdge, () => _ledgeClimbSystem.CanGrabLedge() && attackComponent.isAttackAnim == false && slideComponent.SlideProcess == null && hookComponent.HookGrabProcess == null);
            _fsmSystem.AddTransition(fall,wallEdge, () => _ledgeClimbSystem.CanGrabLedge() && attackComponent.isAttackAnim == false && slideComponent.SlideProcess == null && hookComponent.HookGrabProcess == null);

            _fsmSystem.AddAnyTransition(idle, () => Mathf.Abs(cachedVelocity.x) <= 1.5f  && Mathf.Abs(cachedVelocity.y) < 1.5f
                                                                                         && !dashComponent.isDash && wallEdgeClimbComponent.EdgeStuckProcess == null && groundingComponent.isGround 
                                                                                         && slideComponent.SlideProcess == null && wallRunComponent.wallRunProcess == null && dashComponent.DashProcess == null 
                                                                                         && !hookComponent.isHooked);
           
            _fsmSystem.SetState(idle);

            animationComponent.AddState("WallGlide", s => s
            .Part("LeftHand", "WallGlideLeftHand")
            .Part("RightHand", "WallGlideRightHand"));

            animationComponent.AddState("AttackForward", s => s
            .Part("LeftHand", "OneHandAttackLeftHand")
            .Part("Main", "MainAttackForward")
            .Part("RightHand", "OneHandAttackRightHand"));

            animationComponent.AddState("AttackTwoHandForward", s => s
            .Part("LeftHand", "TwoHandedAttackLeft")
            .Part("Main", "MainAttackForward")
            .Part("RightHand", "TwoHandedAttack"));

            animationComponent.AddState("Idle", s => s
                .Part("Main", "MainIdle")
                .Part("Torso", "IdleTorso")
                .Part("Hair", "IdleHair")
                .Part("LeftHand", "IdleHandLeft")
                .Part("RightHand", "IdleHandRight")
                .Part("Legs", "IdleLegs"));

            // Walk
            animationComponent.AddState("Walk", s => s
                .Part("Main", "MainIdle")
                .Part("Torso", "WalkingTorso")
                .Part("Hair", "WalkingHair")
                .Part("LeftHand", "IdleHandLeft")
                .Part("RightHand", "IdleHandRight")
                .Part("Legs", "WalkingLegs"));

            // FallDown
            animationComponent.AddState("FallDown", s => s.Part("Main", "MainIdle")
                .Part("Torso", "FallTorso")
                .Part("Hair", "FallHairs")
                .Part("LeftHand", "FallLeftHand")
                .Part("RightHand", "FallRightHand")
                .Part("Legs", "FallLegs"));

            // FallUp
            animationComponent.AddState("FallUp", s => s.Part("Main", "MainIdle")
                .Part("Torso", "FallUpTorso")
                .Part("Hair", "FallUpHairs")
                .Part("LeftHand", "FallUpLeftHand")
                .Part("RightHand", "FallUpRigtHand")
                .Part("Legs", "FallUpLegs"));

            // Slide
            animationComponent.AddState("Slide", s => s
                .Part("Main", "MainSlide")
                .Part("Torso", "SlideTorso")
                .Part("Hair", "SlideHair")
                .Part("LeftHand", "SlideLeftHand")
                .Part("RightHand", "SlideRightHand")
                .Part("Legs", "SlideLegs"));

            // WallEdgeClimb
            animationComponent.AddState("WallEdgeClimb", s => s.Part("Main", "MainLengeClimb")
                .Part("Torso", "LengeClimbTorso")
                .Part("Hair", "LengeClimbHair")
                .Part("LeftHand", "LengeClimbLeftHand")
                .Part("RightHand", "LengeClimbRightHand")
                .Part("Legs", "LengeClimbLegs"));

            // WallRun
            animationComponent.AddState("WallRun", s => s
                .Part("Main", "MainWallRun")
                .Part("Torso", "WallRunTorso")
                .Part("Hair", "WallRunHair")
                .Part("LeftHand", "WallRunLeftHand")
                .Part("RightHand", "WallRunRightHand")
                .Part("Legs", "WallRunLegs"));
        }

        public override void Update()
        {
            base.Update();
            _flipComponent.direction = MoveDirection;
            moveComponent.direction = new Vector2(MoveDirection.x,moveComponent.direction.y);

            animationComposerSystem.Update();
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (wallRunComponent.wallRunProcess == null && slideComponent.SlideProcess == null)
                _gravityScalerSystem.Update();

            LateVelocity = cachedVelocity;
            cachedVelocity = baseFields.rb.linearVelocity;
/*
            if(wallEdgeClimbComponent.allowClimb && wallEdgeClimbComponent.EdgeStuckProcess == null && wallRunComponent.wallRunProcess == null)
                _stickyHandsSystem.Update();
            else
                _stickyHandsSystem.ReturnToNormal();*/
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