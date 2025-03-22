using System;
using Systems;
using UnityEngine;
using UnityEngine.Serialization;

namespace Controllers
{
    public class PlayerController : Controller
    {
        private IInputProvider _input;
        private readonly MoveSystem _moveSystem = new MoveSystem();
        private readonly JumpSystem _jumpSystem = new JumpSystem();
        private readonly BackPackSystem _backPackSys = new BackPackSystem();
        private readonly InventorySystem _inventorySystem = new InventorySystem();
        private readonly SpriteFlipSystem _flipSystem = new SpriteFlipSystem();
        private readonly ColorPositioningSystem _colorPositioningSystem = new ColorPositioningSystem();
        [SerializeField] private MoveComponent moveComponent;
        [SerializeField] private JumpComponent jumpComponent;
        [SerializeField] private AttackComponent attackComponent;
        [SerializeField] private BackpackComponent backpackComponent = new BackpackComponent();
        [SerializeField] private InventoryComponent inventoryComponent = new InventoryComponent();
        [SerializeField] private ColorPositioningComponent handColorPos = new ColorPositioningComponent();
        private readonly AnimationStateControllerSystem _animSystem = new AnimationStateControllerSystem();
        private readonly SpriteFlipComponent _flipComponent = new SpriteFlipComponent();

        private readonly AttackSystem _attackSystem = new AttackSystem();
        

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

            InitSystems();
            Subscribe();
        }

        private void Subscribe()
        {
            _input.GetState().OnJumpUp += _jumpSystem.Jump;
            _input.GetState().OnJumpDown += _jumpSystem.OnJumpUp;
            _input.GetState().OnInteract += _inventorySystem.TakeItem;
            _input.GetState().OnDrop += _inventorySystem.ThrowItem;
            
            _input.GetState().OnNext += _inventorySystem.NextItem;
            _input.GetState().OnPrev += _inventorySystem.PreviousItem;
            _input.GetState().OnAttackPressed += callback => _attackSystem.Update();
        }
        private void Unsubscribe()
        {
            _input.GetState().OnJumpUp -= _jumpSystem.Jump;
            _input.GetState().OnJumpDown -= _jumpSystem.OnJumpUp;
            _input.GetState().OnInteract -= _inventorySystem.TakeItem;
            _input.GetState().OnDrop -= _inventorySystem.ThrowItem;
        }
        private void InitSystems()
        {
            _moveSystem.Initialize(this);
            _jumpSystem.Initialize(this);
            _flipSystem.Initialize(this, _flipComponent);
            _backPackSys.Initialize(this, backpackComponent,handColorPos);
            _inventorySystem.Initialize(this, inventoryComponent, backpackComponent,handColorPos);

            _animSystem.Initialize(this);

            _colorPositioningSystem.Initialize(this,handColorPos);
            
            _attackSystem.Initialize(this);
        }

        private void AddComponentsToList()
        {
            AddControllerComponent(moveComponent);
            AddControllerComponent(jumpComponent);
            AddControllerComponent(_flipComponent);
            AddControllerComponent(backpackComponent);
            AddControllerComponent(inventoryComponent);
            AddControllerComponent(handColorPos);
            AddControllerComponent(attackComponent);
        }

        private void Update()
        {
            _flipComponent.direction = MoveDirection;
            _flipSystem.Update();
            _backPackSys.Update();
            moveComponent.direction = MoveDirection;
            _moveSystem.Update();
            _colorPositioningSystem.Update();
            _animSystem.Update();
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