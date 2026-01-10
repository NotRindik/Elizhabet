using Assets.Scripts.Systems;
using States;
using System;
using System.Collections.Generic;
using Systems;
using UnityEngine;
using UnityEngine.Rendering.VirtualTexturing;

public enum AbilityType
{
    LedgeClimb,
    Dash,
    Slide,
    SlideDash,
    WallRun,
    Hook
}


namespace Controllers
{
    public class PlayerController : EntityController
    {
        [SerializeField] public ObservableList<AbilityType> abilitieContainer = new();

        private Dictionary<AbilityType, BaseSystem> _abilities;

        public IInputProvider input = new PlayerSourceInput();
        protected MoveSystem _moveSystem = new();
        private JumpSystem _jumpSystem = new();
        private InventorySystem _inventorySystem = new();
        private SpriteFlipSystem _flipSystem = new();
        private ColorPositioningSystem _colorPositioningSystem = new();
        private LedgeClimbSystem _ledgeClimbSystem = new();
        private FrictionSystem _frictionSystem = new();
        private FSMSystem _fsmSystem = new();
        private DashSystem _dashSystem = new();
        private SlideSystem _slideSystem = new();
        private SlideDashSystem _slideDashSystem = new();
        private WallRunSystem _wallRunSystem = new();
        private HookSystem _hookSystem = new();
        private GroundingSystem _groundingSystem = new();
        private PlatformSystem _platformSystem = new();
        private ArmorSystem _armorSystem = new();
        private AnimationComposerSystem animationComposerSystem = new();
        private StickyHandsSystem _stickyHandsSystem = new();
        private HandsRotatoningSystem handsRotatoningSystem = new();
        private ManaSystem _manaSystem = new();
        private ArmourProtectionSystem _armourProtectionSystem = new();
        private ModificatorsSystem _modsSystem = new();
        private GravityScalerSystem _gravityScalerSystem = new();
        private PlayerTakeDamageSystem _playerTakeDamageSystem = new();
        private StepClimbSystem _stepClimb = new();
        private ItemThrowSystem _itemThrowSystem = new();
        private AnimationEventsUpdater _animationEventUpdaterSys = new();
        private HeadRotSystem _heaRotSystem = new();

        [Header("Moving")]
        public MoveComponent moveComponent;
        public JumpComponent jumpComponent;
        public AttackComponent attackComponent = new AttackComponent();
        public InventoryComponent inventoryComponent = new InventoryComponent();
        public ColorPositioningComponent colorPositioningComponent = new ColorPositioningComponent();
        public WallEdgeClimbComponent wallEdgeClimbComponent = new WallEdgeClimbComponent();
        public DashComponent dashComponent= new DashComponent();
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
        public RendererCollection spriteSynchronizer = new RendererCollection();
        public PetComponent PetComponent = new PetComponent();
        public StepClimbComponent stepClimb = new();
        private AttackSystem _attackSystem = new AttackSystem();
        public ItemThrowComponent itemThrowComponent = new();
        public HandRotatorsComponent handRotatorsComponent = new HandRotatorsComponent();
        public HeadRotComponent headRotComponent = new HeadRotComponent();

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

        protected override void Awake()
        {
            base.Awake();
            SetUpAbilities();
        }

        private void SetUpAbilities()
        {
            _abilities = new()
        {
            { AbilityType.LedgeClimb, _ledgeClimbSystem },
            { AbilityType.Dash, _dashSystem },
            { AbilityType.Slide, _slideSystem },
            { AbilityType.SlideDash, _slideDashSystem },
            { AbilityType.WallRun, _wallRunSystem },
            { AbilityType.Hook, _hookSystem },
        };


            abilitieContainer.OnItemAdded += OnAbility;
            abilitieContainer.OnItemRemoved += OffAbility;
            SyncAll();
        }

        void SyncAll()
        {
            foreach (var a in _abilities.Values)
                a.IsActive = false;

            foreach (var type in abilitieContainer.Raw)
                _abilities[type].IsActive = true;
        }

        void OnAbility(AbilityType type)
        {
            _abilities[type].IsActive = true;
        }
        void OffAbility(AbilityType type)
        {
            _abilities[type].IsActive = false;
        }

        protected unsafe void Start()
        {
            Subscribe();    
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

            input.GetState().Attack.started += c =>
            {
                if(itemThrowComponent.isCharging) 
                    _itemThrowSystem.Throw();
            };

            input.GetState().ThrowItem.started += c =>
            {
                _itemThrowSystem.Update();
            };
            input.GetState().ThrowItem.canceled += c =>
            {
                itemThrowComponent.isCharging = false;
            };

            input.GetState().Move.performed += c => moveDirection = c.ReadValue<Vector2>();
            input.GetState().Move.canceled += c => moveDirection = c.ReadValue<Vector2>();

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
                if (context.ReadValue<Vector2>().y > 0)
                    _inventorySystem.NextItem();
                else if (context.ReadValue<Vector2>().y < 0)
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
                if (c.ReadValue<Vector2>().y < -0.7f)
                {
                    _platformSystem.Update();
                }
            };
        }
        private void Unsubscribe()
        {
            abilitieContainer.OnItemAdded -= OnAbility;
            abilitieContainer.OnItemRemoved -= OffAbility;
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
            .Part("RightHand", "OneHandAttackRightHand"));

            animationComponent.AddState("AttackForward2", s => s
            .Part("LeftHand", "OneHandAttackLeftHand")
            .Part("RightHand", "OneRightHandAttack2"));

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

        public void LockSpriteFlip(bool isLock = false)
        {
            _flipSystem.IsActive = !isLock;
        }
    }
}