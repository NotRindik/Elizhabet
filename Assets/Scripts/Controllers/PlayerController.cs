using System.Collections.Generic;
using States;
using Systems;
using UnityEngine;
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
        private readonly LedgeClimbSystem _ledgeClimbSystem = new LedgeClimbSystem();
        private readonly FrictionSystem _frictionSystem = new FrictionSystem();
        private readonly FSMSystem _fsmSystem = new FSMSystem();
        private readonly DashSystem _dashSystem = new DashSystem();
        
        [SerializeField] private MoveComponent moveComponent;
        [SerializeField] private JumpComponent jumpComponent;
        [SerializeField] private AttackComponent attackComponent = new AttackComponent();
        [SerializeField] private InventoryComponent inventoryComponent = new InventoryComponent(); 
        [SerializeField] private ColorPositioningComponent colorPositioningComponent = new ColorPositioningComponent();
        [SerializeField] public WallEdgeClimbComponent wallEdgeClimbComponent = new WallEdgeClimbComponent();
        [SerializeField] public  DashComponent dashComponent= new DashComponent();
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
            input.GetState().inputActions.Player.Interact.started += _inventorySystem.TakeItem;
            input.GetState().inputActions.Player.OnDrop.started += _inventorySystem.ThrowItem;
            input.GetState().inputActions.Player.Jump.started += c => _fsmSystem.SetState(new JumpState(this));
            input.GetState().inputActions.Player.Jump.canceled += c => _fsmSystem.SetState(new JumpUpState(this));
            
            input.GetState().inputActions.Player.Next.started += _inventorySystem.NextItem;
            input.GetState().inputActions.Player.Previous.started += _inventorySystem.PreviousItem;
            /*input.GetState().inputActions.Player.Attack.started += _ => _attackSystem.Update();*/
        }
        private void Unsubscribe()
        {
            input.GetState().inputActions.Player.Interact.started -= _inventorySystem.TakeItem;
            input.GetState().inputActions.Player.OnDrop.started -= _inventorySystem.ThrowItem;

            input.GetState().inputActions.Player.Next.started -= _inventorySystem.NextItem;
            input.GetState().inputActions.Player.Previous.started -= _inventorySystem.PreviousItem;
            input.GetState().inputActions.Player.Attack.started -= _ => _attackSystem.OnUpdate();
        }
        protected override void InitSystems()
        {
            base.InitSystems();

            foreach (var system in systems)
            {
                system.Value.Initialize(this);
            }
            
            var idle = new IdleState(this);
            var walk = new WalkState(this);
            var fall = new FallState(this);
            var wallEdge = new WallLeangeClimb(this);
            
            _fsmSystem.AddAnyTransition(fall, () => !jumpComponent.isGround && baseFields.rb.linearVelocityY < -1);
            _fsmSystem.AddAnyTransition(walk, () =>Mathf.Abs(baseFields.rb.linearVelocityX) > 1.5f && jumpComponent.isGround && Mathf.Abs(baseFields.rb.linearVelocityY) < 1.5f);
            _fsmSystem.AddTransition(fall,wallEdge, () => _ledgeClimbSystem.CanGrabLedge(out var _, out var _));
            _fsmSystem.AddAnyTransition(idle, () => Mathf.Abs(baseFields.rb.linearVelocityX) <= 1.5f && jumpComponent.isGround && jumpComponent.isGround && Mathf.Abs(baseFields.rb.linearVelocityY) < 1.5f);
            
            _fsmSystem.SetState(idle);
        }

        protected override void AddSystemToList()
        {
            base.AddSystemToList();
            AddControllerSystem(_moveSystem);
            AddControllerSystem(_jumpSystem);
            AddControllerSystem(_inventorySystem);
            AddControllerSystem(_colorPositioningSystem);
            AddControllerSystem(_attackSystem);
            AddControllerSystem(_flipSystem);
            AddControllerSystem(_ledgeClimbSystem);
            AddControllerSystem(_fsmSystem);
            AddControllerSystem(_frictionSystem);
            AddControllerSystem(_dashSystem);
        }
        protected override void AddComponentsToList()
        {
            base.AddComponentsToList();
            AddControllerComponent(moveComponent);
            AddControllerComponent(jumpComponent);
            AddControllerComponent(_flipComponent);
            AddControllerComponent(inventoryComponent);
            AddControllerComponent(colorPositioningComponent);
            AddControllerComponent(attackComponent);
            AddControllerComponent(input.GetState());
            AddControllerComponent(wallEdgeClimbComponent);
            AddControllerComponent(dashComponent);
        }

        public override void Update()
        {
            base.Update();
            _flipComponent.direction = MoveDirection;
            _colorPositioningSystem.OnUpdate();
        }
        public override void FixedUpdate()
        {
            moveComponent.direction = new Vector2(input.GetState().movementDirection.x,moveComponent.direction.y);
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