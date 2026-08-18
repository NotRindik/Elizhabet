using Assets.Scripts.Systems;
using States;
using System;
using System.Collections.Generic;
using std;
using Systems;
using UnityEngine;

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
    [DefaultExecutionOrder(1000)]
    public class PlayerController : EntityController
    {
        [SerializeField] public ObservableList<AbilityType> abilitieContainer = new();

        private Dictionary<AbilityType, BaseSystem> _abilities;

        public ProxyInputState input = new ProxyInputState();
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
        private TextureOverlaySystem _textureOverlaySystem = new();
        private ArmorSystem _armorSystem = new();
        private AnimationComposerSystem animationComposerSystem = new();
        private StickyHandsSystem _stickyHandsSystem = new();
        private HandsRotatoningSystem handsRotatoningSystem = new();
        private ManaSystem _manaSystem = new();
        private ModificatorsSystem _modsSystem = new();
        private GravityScalerSystem _gravityScalerSystem = new();
        private PlayerTakeDamageSystem _playerTakeDamageSystem = new();
        private StepClimbSystem _stepClimb = new();
        private ItemThrowSystem _itemThrowSystem = new();
        private AnimationEventsUpdater _animationEventUpdaterSys = new();
        private HeadRotSystem _heaRotSystem = new();
        private InteractionHandleSystem _interactionHandleSystem = new();
        private TileDetectionSystem _tileDetectionSystem = new();
        private SurfaceObjectDetectionSystem _surfaceObjectDetectionSystem = new();

        private IFrameSystem _iFrameSystem = new();

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
        public PivotsComponent pivotsComponent = new PivotsComponent();
        public InteractionHandleComponent interactionHandleComponent = new InteractionHandleComponent();
        public TileDetectionComponent tileDetectionComponent = new TileDetectionComponent();
        public SurfaceDetectionComponent SurfaceDetectionComponent = new();
        public TextureOverlayComponent TextureOverlayComponent = new();

        public IFrameComponent iframeComponent = new();
        
        private JumpState jumpState;
        private JumpUpState jumpUpState;


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
                
                _flipComponent.direction = (int)result.x;
                
                return result;
            }
        }

        private Action<InputContext> _onInteract;
        private Action<InputContext> _onDrop;
        private Action<InputContext> _onAttack;
        private Action<InputContext> _onThrowStarted;
        private Action<InputContext> _onThrowCanceled;

        private Action<InputContext> _onMovePerformed;
        private Action<InputContext> _onMoveCanceled;
        private Action<InputContext> _onMovePlatformCheck;

        private Action<InputContext> _onJumpStarted;
        private Action<InputContext> _onJumpCanceled;

        private Action<InputContext> _onWeaponWheel;
        private Action<InputContext> _onDash;
        private Action<InputContext> _onSlide;
        private Action<InputContext> _onGrablingHook;

        public static PlayerController Instance;
        protected override void Awake()
        {

            if (Instance == null)
            {
                Instance = this;
                
                Debug.Log("PLAYER INIT");
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                gameObject.SetActive(false);
                ContextManager.Instance.player = Instance;
                return;
            }

            inventoryComponent = PlayerInventory.Instance.InventoryComponent;
            
            base.Awake();
            SetUpAbilities();
            
            input.SetProvider(new PlayerSourceInput());
            
            ContextManager.Instance.player = Instance;
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

        public override void OnEnable()
        {
            SetActiveAllSys(true);
            SyncAll();
        }

        public override void OnDisable()
        {
            SetActiveAllSys(false);
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

        protected void Start()
        {
            Subscribe();    
            States();
        }

        private void Subscribe()
        {
            if(InputManager.inputActions == null)
                return;
            
            var state = input.GetState();
            
            _onDrop = OnDrop;
            _onAttack = ThrowItemAfterCharge;
            _onThrowStarted = OnThrowStarted;
            _onThrowCanceled = OnThrowCanceled;

            _onMovePerformed = OnMovePerformed;
            _onMoveCanceled = OnMoveCanceled;
            _onMovePlatformCheck = OnMovePlatformCheck;

            _onJumpStarted = OnJumpStarted;
            _onJumpCanceled = OnJumpCanceled;

            _onWeaponWheel = OnWeaponWheel;
            _onDash = OnDash;
            _onSlide = OnSlide;
            _onGrablingHook = OnGrablingHook;

            state.Interact.started += _onInteract;
            state.OnDrop.started += _onDrop;
            state.Attack.started += _onAttack;

            state.ThrowItem.started += _onThrowStarted;
            state.ThrowItem.canceled += _onThrowCanceled;

            state.Move.performed += _onMovePerformed;
            state.Move.canceled += _onMoveCanceled;
            state.Move.performed += _onMovePlatformCheck;

            state.Jump.started += _onJumpStarted;
            state.Jump.canceled += _onJumpCanceled;

            state.WeaponWheel.started += _onWeaponWheel;
            state.Dash.started += _onDash;
            state.Slide.started += _onSlide;
            state.GrablingHook.started += _onGrablingHook;
        }
        private void OnDrop(InputContext c)
        {
            if (!attackComponent.isAttackAnim && fsmComponent.currentState != nameof(TakeHitState))
                _inventorySystem.ThrowItem();
        }

        private void ThrowItemAfterCharge(InputContext c)
        {
            if (itemThrowComponent.isCharging && fsmComponent.currentState != nameof(TakeHitState))
                _itemThrowSystem.Throw();
        }

        private void OnThrowStarted(InputContext c)
        {
            if (!attackComponent.isAttackAnim && inventoryComponent.ActiveItem && fsmComponent.currentState != nameof(TakeHitState))
                _itemThrowSystem.Update();
        }

        private void OnThrowCanceled(InputContext c)
        {
            itemThrowComponent.isCharging = false;
        }

        private void OnMovePerformed(InputContext c)
        {
            moveDirection = c.ReadValue<Vector2>();
        }

        private void OnMoveCanceled(InputContext c)
        {
            moveDirection = c.ReadValue<Vector2>();
        }

        private void OnMovePlatformCheck(InputContext c)
        {
            if (c.ReadValue<Vector2>().y < -0.7f)
                _platformSystem.Update();
        }
        
        
        private void OnJumpStarted(InputContext c)
        {
            if(!_jumpSystem.IsActive)
                return;
            
            if (slideComponent.isCeilOpen &&
                (groundingComponent.isGround || jumpComponent.coyotTime > 0) &&
                wallEdgeClimbComponent.EdgeStuckProcess == null && fsmComponent.currentState != nameof(TakeHitState))
            {
                _fsmSystem.SetState(jumpState);
            }
            else
            {
                _jumpSystem.StartJumpBuffer();
            }
        }

        private void OnJumpCanceled(InputContext c)
        {
            if (slideComponent.isCeilOpen && wallRunComponent.wallRunProcess == null && !wallRunComponent.isJumped && 
                wallEdgeClimbComponent.EdgeStuckProcess == null && fsmComponent.currentState != nameof(TakeHitState))
            {
                _fsmSystem.SetState(jumpUpState);
            }
            
            _jumpSystem.CancelJumpBuffer();
        }

        private void OnWeaponWheel(InputContext context)
        {
            if (attackComponent.isAttackAnim)
                return;

            float y = context.ReadValue<Vector2>().y;

            if (y > 0)
                _inventorySystem.NextItem();
            else if (y < 0)
                _inventorySystem.PreviousItem();
        }

        private void OnDash(InputContext c)
        {
            if (dashComponent.allowDash &&
                dashComponent.DashProcess == null &&
                wallEdgeClimbComponent.EdgeStuckProcess == null &&
                !attackComponent.isAttackAnim && fsmComponent.currentState != nameof(TakeHitState))
            {
                _fsmSystem.SetState(new DashState(this));
            }
        }

        private void OnSlide(InputContext c)
        {
            if (!attackComponent.isAttackAnim &&
                wallRunComponent.wallRunProcess == null &&
                wallEdgeClimbComponent.EdgeStuckProcess == null && fsmComponent.currentState != nameof(TakeHitState))
            {
                _fsmSystem.SetState(new SlideState(this));
            }
        }

        private void OnGrablingHook(InputContext c)
        {
            if (!slideComponent.isCeilOpen &&
                slideComponent.SlideProcess != null &&
                attackComponent.isAttackAnim && fsmComponent.currentState != nameof(TakeHitState))
                return;

            _fsmSystem.SetState(new GrablingHookState(this));
        }
        private void Unsubscribe()  
        {
            abilitieContainer.OnItemAdded -= OnAbility;
            abilitieContainer.OnItemRemoved -= OffAbility;

            var state = input.GetState();

            state.Interact.started -= _onInteract;
            state.OnDrop.started -= _onDrop;
            state.Attack.started -= _onAttack;

            state.ThrowItem.started -= _onThrowStarted;
            state.ThrowItem.canceled -= _onThrowCanceled;

            state.Move.performed -= _onMovePerformed;
            state.Move.canceled -= _onMoveCanceled;
            state.Move.performed -= _onMovePlatformCheck;

            state.Jump.started -= _onJumpStarted;
            state.Jump.canceled -= _onJumpCanceled;

            state.WeaponWheel.started -= _onWeaponWheel;
            state.Dash.started -= _onDash;
            state.Slide.started -= _onSlide;
            state.GrablingHook.started -= _onGrablingHook;
            input.SetProvider(null);
        }
        private void States()
        {

            var idle = new IdleState(this);
            var walk = new WalkState(this);
            var fall = new FallState(this);
            var wallEdge = new WallLeangeClimb(this);
            var wallRun = new WallRunState(this);
            var fallUp = new FallUpState(this);
            var takeHit = new TakeHitState(this);

            jumpState = new JumpState(this);
            jumpUpState = new JumpUpState(this);

            bool tookHit = false;
            Coroutine tookHitProcess = null;

            Action resetTookHit = () => tookHit = false;
            
            healthComponent.OnTakeHit += info =>
            {
                tookHit = true;
                if(tookHitProcess != null)
                    StopCoroutine(tookHitProcess);
                tookHitProcess = StartCoroutine(std.Utilities.Invoke(resetTookHit,0.5f));
            };
            
            _fsmSystem.AddAnyTransition(takeHit, () => tookHit);
            
            _fsmSystem.AddAnyTransition(wallRun, () => _wallRunSystem.CanStartWallRun() && ((cachedVelocity.y >= 2 && Mathf.Abs(LateVelocity.x) >= 4.2f) || !dashComponent.allowDash)  && wallRunComponent.canWallRun && wallRunComponent.wallRunProcess == null 
                                                               && Mathf.Approximately(moveComponent.direction.x, transform.FacingSign()) && attackComponent.isAttackAnim == false && slideComponent.SlideProcess == null  
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
            
            
            animationComponent.AddState("TakeHit", s => s
                .Part("Torso", "TakeHitTorso")
                .Part("Legs", "TakeHitLegs")
                .Part("LeftHand", "TakeHitLeftHand")
                .Part("RightHand", "TakeHitRightHand"));

            animationComponent.AddState("WallGlide", s => s
            .Part("LeftHand", "WallGlideLeftHand")
            .Part("RightHand", "WallGlideRightHand"));

            animationComponent.AddState("AttackDown", s => s
                .Part("LeftHand", "OneHandAttackLeftHand")
                .Part("RightHand", "OneArmedAttackDown"));
            
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
            animationComponent.AddState("FallDown", s => 
                s.Part("Main", "MainIdle")
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
            moveComponent.direction = new Vector2(MoveDirection.x,moveComponent.direction.y);
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

        public override void OnDestroy()
        {
            base.OnDestroy();
            if(Instance == this)
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