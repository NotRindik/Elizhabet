using Controllers;
using States;
using System.Collections.Generic;
using UnityEngine;

namespace Systems { 
    public class PlayerTest : EntityController
    {
        public IInputProvider input = new PlayerSourceInput();
        protected MoveSystem _moveSystem = new MoveSystem();
        private readonly JumpSystem _jumpSystem = new JumpSystem();
        private readonly SpriteFlipSystem _flipSystem = new SpriteFlipSystem();
        private readonly GroundingSystem _groundingSystem = new GroundingSystem();
        private readonly FSMSystem _fsmSystem = new FSMSystem();
        private readonly ColorPositioningSystem _colorPositioningSystem = new ColorPositioningSystem();

        [SerializeField] private MoveComponent moveComponent;
        [SerializeField] private JumpComponent jumpComponent;
        private readonly SpriteFlipComponent _flipComponent = new SpriteFlipComponent();
        [SerializeField] public GroundingComponent groundingComponent;
        public FsmComponent fsmComponent;

        public AnimationComponentsComposer animationComposer;
        public ColorPositioningComponent colorPositioningComponent;


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

        public LayerMask layer;

        public List<AnimationState> states;

        protected void Start()
        {
            Subscribe();
            States();
        }

        private void Subscribe()
        {
            input.GetState().Move.performed += c => moveDirection = c;
            input.GetState().Move.canceled += c => moveDirection = c;


            input.GetState().Jump.started += c =>
            {

                if ((groundingComponent.isGround || jumpComponent.coyotTime > 0))
                    _fsmSystem.SetState(new JumpState(this));
                else
                {
                    _jumpSystem.StartJumpBuffer();
                }
            };
            input.GetState().Jump.canceled += c =>
            {
                _fsmSystem.SetState(new JumpUpState(this));
            };
        }
        private void States()
        {

            var idle = new IdleState(this);
            var walk = new WalkState(this);
            _fsmSystem.AddAnyTransition(walk, () => Mathf.Abs(baseFields.rb.linearVelocity.x) > 1.5f && groundingComponent.isGround && Mathf.Abs(baseFields.rb.linearVelocity.y) < 1.5f);
            _fsmSystem.AddAnyTransition(idle, () => Mathf.Abs(baseFields.rb.linearVelocity.x) <= 1.5f && Mathf.Abs(baseFields.rb.linearVelocity.y) < 1.5f);

            _fsmSystem.SetState(idle);

        }

        public override void Update()
        {
            base.Update();
            _flipComponent.direction = MoveDirection;
            moveComponent.direction = new Vector2(MoveDirection.x, moveComponent.direction.y);

/*
            if(fsmComponent.currentState == "States.IdleState")
            {
                animationComposer.CrossFadeState(states[0],0.1f);
            }
            else if (fsmComponent.currentState == "States.WalkState")
            {
                animationComposer.CrossFadeState(states[1], 0.1f);
            }
            else if (fsmComponent.currentState == "States.JumpState")
            {
                animationComposer.CrossFadeState(states[3], 0.1f);
            }
            else if (fsmComponent.currentState == "States.JumpUpState")
            {
                animationComposer.CrossFadeState(states[2], 0.1f);
            }*/
        }

        public override void LateUpdate()
        {
            base.LateUpdate();
            _colorPositioningSystem.Update();
        }
    }
}