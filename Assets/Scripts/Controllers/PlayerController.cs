using System;
using Systems;
using UnityEngine;

namespace Controllers
{
    public class PlayerController : Controller,IAnimationController
    {
        private IInputProvider _input;
        private readonly MoveSystem _moveSystem = new MoveSystem();
        private readonly JumpSystem _jumpSystem = new JumpSystem();
        private readonly BackPackSystem _backPackSys = new BackPackSystem();
        private readonly TakeThrowItemSystem _takeThrowItemSystem = new TakeThrowItemSystem();
        private readonly SpriteFlipSystem _flipSystem = new SpriteFlipSystem();
        private readonly MovementAnimationSystem _movementAnimation = new MovementAnimationSystem();
        private readonly ColorPositioningSystem _colorPositioningSystem = new ColorPositioningSystem();
        [SerializeField] private MoveComponent moveComponent;
        [SerializeField] private JumpComponent jumpComponent;
        [SerializeField] private BackpackComponent backpackComponent = new BackpackComponent();
        [SerializeField] private TakeThrowComponent takeThrowComponent = new TakeThrowComponent();
        [SerializeField] private ColorPositioningComponent colorComponent = new ColorPositioningComponent();
        private readonly AnimationComponent _animComponent = new AnimationComponent();
        private readonly SpriteFlipComponent _flipComponent = new SpriteFlipComponent();

        public Animator animator;


        private Vector2 MoveDirection => _input.GetState().movementDirection;

        protected override void OnValidate()
        {
            base.OnValidate();
            if(!animator)
                animator = GetComponent<Animator>();
        }
        private void Start()
        {
            _input = new NavigationSystem();
            AddComponentsToList();

            EditComponentData();

            InitSystems();
            Subscribe();
        }

        private void Subscribe()
        {
            _input.GetState().OnJumpUp += _jumpSystem.Jump;
            _input.GetState().OnJumpDown += _jumpSystem.OnJumpUp;
            _input.GetState().OnInteract += _takeThrowItemSystem.TakeItem;
            _input.GetState().OnDrop += _takeThrowItemSystem.ThrowItem;
        }
        private void Unsubscribe()
        {
            _input.GetState().OnJumpUp -= _jumpSystem.Jump;
            _input.GetState().OnJumpDown -= _jumpSystem.OnJumpUp;
            _input.GetState().OnInteract -= _takeThrowItemSystem.TakeItem;
            _input.GetState().OnDrop -= _takeThrowItemSystem.ThrowItem;
        }

        private void EditComponentData()
        {
            _animComponent.anim = animator;
            _animComponent.rigidbody = baseFields.rb;
        }

        private void InitSystems()
        {
            _moveSystem.Initialize(this);
            _jumpSystem.Initialize(this);
            _flipSystem.Initialize(this, _flipComponent);
            _backPackSys.Initialize(this, backpackComponent,colorComponent);
            _takeThrowItemSystem.Initialize(this, takeThrowComponent, backpackComponent,colorComponent);

            _movementAnimation.Initialize(_animComponent, moveComponent, jumpComponent);

            _colorPositioningSystem.Initialize(this,colorComponent);
        }

        private void AddComponentsToList()
        {
            AddControllerComponent(moveComponent);
            AddControllerComponent(jumpComponent);
            AddControllerComponent(_animComponent);
            AddControllerComponent(_flipComponent);
            AddControllerComponent(backpackComponent);
            AddControllerComponent(takeThrowComponent);
            AddControllerComponent(colorComponent);
        }

        private void Update()
        {
            _flipComponent.direction = MoveDirection;
            _flipSystem.Update();
            _backPackSys.Update();
            moveComponent.direction = MoveDirection;
            _moveSystem.Update();
            _colorPositioningSystem.Update();
            _movementAnimation.Update();
            _jumpSystem.Update();
        }
        private void FixedUpdate()
        {
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube((Vector2)baseFields.collider.bounds.center + Vector2.down * baseFields.collider.bounds.extents.y, jumpComponent.groundCheackSize);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }
    }
}