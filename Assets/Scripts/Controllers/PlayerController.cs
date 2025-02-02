using System;
using Systems;
using UnityEngine;

namespace Controllers
{
    public class PlayerController : Controller,IAnimationController
    {
        public IInputProvider input;
        private MoveSystem moveSystem = new MoveSystem();
        private JumpSystem jumpSystem = new JumpSystem();
        private BackPackSystem backPackSys = new BackPackSystem();
        private TakeThrowItemSystem takeThrowItemSystem = new TakeThrowItemSystem();
        private SpriteFlipSystem flipSystem = new SpriteFlipSystem();
        private MovementAnimationSystem movementAnimation = new MovementAnimationSystem();
        private ColorPositioningSystem colorPositioningSystem = new ColorPositioningSystem();
        [SerializeField] private MoveComponent moveComponent;
        [SerializeField] private JumpComponent jumpComponent;
        [SerializeField] private BackpackComponent backpackComponent = new BackpackComponent();
        [SerializeField] private TakeThrowComponent takeThrowComponent = new TakeThrowComponent();
        [SerializeField] private ColorPositioningComponent colorComponent = new ColorPositioningComponent();
        private AnimationComponent animComponent = new AnimationComponent();
        private SpriteFlipComponent flipComponent = new SpriteFlipComponent();

        public Animator animator;


        private Vector2 moveDirection => input.GetState().movementDirection;

        protected override void OnValidate()
        {
            base.OnValidate();
            if(!animator)
                animator = GetComponent<Animator>();
        }
        private void Start()
        {
            input = new NavigationSystem();
            AddComponentsToList();

            EditComponentData();

            InitSystems();
            Subscribe();
        }

        private void Subscribe()
        {
            input.GetState().OnJumpUp += jumpSystem.Jump;
            input.GetState().OnJumpDown += jumpSystem.OnJumpUp;
            input.GetState().OnInteract += takeThrowItemSystem.TakeItem;
        }
        private void Unsubscribe()
        {
            input.GetState().OnJumpUp -= jumpSystem.Jump;
            input.GetState().OnJumpDown -= jumpSystem.OnJumpUp;
            input.GetState().OnInteract -= takeThrowItemSystem.TakeItem;
        }

        private void EditComponentData()
        {
            animComponent.anim = animator;
            animComponent.rigidbody = baseFields.rb;
        }

        private void InitSystems()
        {
            moveSystem.Initialize(this);
            jumpSystem.Initialize(this);
            flipSystem.Initialize(this, flipComponent);
            backPackSys.Initialize(this, backpackComponent);
            takeThrowItemSystem.Initialize(this, takeThrowComponent, backpackComponent);

            movementAnimation.Initialize(animComponent, moveComponent, jumpComponent);

            colorPositioningSystem.Initialize(this,colorComponent);
        }

        private void AddComponentsToList()
        {
            AddControllerComponent(moveComponent);
            AddControllerComponent(jumpComponent);
            AddControllerComponent(animComponent);
            AddControllerComponent(flipComponent);
            AddControllerComponent(backpackComponent);
            AddControllerComponent(takeThrowComponent);
            AddControllerComponent(colorComponent);
        }

        private void Update()
        {
            flipComponent.direction = moveDirection;
            flipSystem.Update();
            movementAnimation.Update();
            colorPositioningSystem.Update();
        }
        private void FixedUpdate()
        {
            moveComponent.direction = moveDirection;
            moveSystem.Update();
            jumpSystem.Update();
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