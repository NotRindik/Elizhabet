using System;
using Controllers;
using Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Controllers
{
    public class PlayerController : EntityController
    {
        public IInputProvider input;
        private readonly MoveSystem _moveSystem = new MoveSystem();
        private readonly JumpSystem _jumpSystem = new JumpSystem();
        private readonly InventorySystem _inventorySystem = new InventorySystem();
        private readonly SpriteFlipSystem _flipSystem = new SpriteFlipSystem();
        private readonly ColorPositioningSystem _colorPositioningSystem = new ColorPositioningSystem();
        
        [SerializeField] private MoveComponent moveComponent;
        [SerializeField] private JumpComponent jumpComponent;
        [SerializeField] private AttackComponent attackComponent = new AttackComponent();
        [SerializeField] private InventoryComponent inventoryComponent = new InventoryComponent();
        [SerializeField] private ColorPositioningComponent handColorPos = new ColorPositioningComponent();
        private readonly AnimationStateControllerSystem _animSystem = new AnimationStateControllerSystem();
        private readonly AnimationStateComponent _animationStateComponent = new AnimationStateComponent();
        private readonly SpriteFlipComponent _flipComponent = new SpriteFlipComponent();

        private readonly AttackSystem _attackSystem = new AttackSystem();
        

        public Animator animator;


        private Vector2 MoveDirection => input.GetState().movementDirection;

        protected override void OnValidate()
        {
            base.OnValidate();
            if(!animator)
                animator = GetComponent<Animator>();
        }
        protected override void Awake()
        {
            input = new NavigationSystem();
            base.Awake();
        }
        protected void Start()
        {
            Subscribe();
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
        }
        private void Subscribe()
        {
            EnableAllActions();
            input.GetState().inputActions.Player.Jump.performed += _jumpSystem.Jump;
            input.GetState().inputActions.Player.Jump.canceled += _jumpSystem.OnJumpUp;
            input.GetState().inputActions.Player.Interact.started += _inventorySystem.TakeItem;
            input.GetState().inputActions.Player.OnDrop.started += _inventorySystem.ThrowItem;
            
            input.GetState().inputActions.Player.Next.started += _inventorySystem.NextItem;
            input.GetState().inputActions.Player.Previous.started += _inventorySystem.PreviousItem;
            input.GetState().inputActions.Player.Attack.started += _ => _attackSystem.Update();
        }
        private void Unsubscribe()
        {
            input.GetState().inputActions.Player.Jump.started -= _jumpSystem.Jump;
            input.GetState().inputActions.Player.Jump.canceled -= _jumpSystem.OnJumpUp;
            input.GetState().inputActions.Player.Interact.started -= _inventorySystem.TakeItem;
            input.GetState().inputActions.Player.OnDrop.started -= _inventorySystem.ThrowItem;

            input.GetState().inputActions.Player.Next.started -= _inventorySystem.NextItem;
            input.GetState().inputActions.Player.Previous.started -= _inventorySystem.PreviousItem;
            input.GetState().inputActions.Player.Attack.started -= _ => _attackSystem.Update();
        }
        protected override void InitSystems()
        {
            base.InitSystems();

            foreach (var system in systems)
            {
                system.Value.Initialize(this);
            }
        }

        protected override void AddSystemToList()
        {
            base.AddSystemToList();
            AddControllerSystem(_moveSystem);
            AddControllerSystem(_jumpSystem);
            AddControllerSystem(_inventorySystem);
            AddControllerSystem(_colorPositioningSystem);
            AddControllerSystem(_animSystem);
            AddControllerSystem(_attackSystem);
            AddControllerSystem(_flipSystem);
        }
        protected override void AddComponentsToList()
        {
            base.AddComponentsToList();
            AddControllerComponent(moveComponent);
            AddControllerComponent(jumpComponent);
            AddControllerComponent(_flipComponent);
            AddControllerComponent(inventoryComponent);
            AddControllerComponent(handColorPos);
            AddControllerComponent(attackComponent);
            AddControllerComponent(input.GetState());
            AddControllerComponent(_animationStateComponent);
        }

        protected void Update()
        {
            _flipComponent.direction = MoveDirection;
            _colorPositioningSystem.Update();
            _animSystem.Update();
            _flipSystem.Update();
            _jumpSystem.Update();
        }
        private void FixedUpdate()
        {
            moveComponent.direction = MoveDirection;
            _moveSystem.Update();
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