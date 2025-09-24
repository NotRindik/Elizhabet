using Controllers;
using System.Collections;
using Systems;
using UnityEngine;

public class Penguino : EntityController
{
    private SpriteFlipSystem _flipSystem = new SpriteFlipSystem();
    private MoveSystem _moveSystem = new MoveSystem();
    private FSMSystem _fasmSystem = new FSMSystem();
    private JumpSystem _jumpSystem = new JumpSystem();
    private GroundingSystem _groundingSystem = new GroundingSystem();
    private PlatformSystem _platformSystem = new PlatformSystem();
    private FrictionSystem _frictionSystem = new FrictionSystem();

    public SpriteFlipComponent flipComponent;
    public MoveComponent moveComponent;
    public AnimationComponent animationComponent;
    public IInputProvider inputProvider = new PenguinAI();
    public FsmComponent FsmComponent = new FsmComponent();
    public PenguinAIComponent penguin = new PenguinAIComponent();
    public JumpComponent jumpComponent = new JumpComponent();
    public GroundingComponent groundingComponent = new GroundingComponent();
    public PlatformComponent platformComponent = new PlatformComponent();

    public void Start()
    {
        inputProvider.GetState().Move.performed +=  c => moveComponent.direction = c;
        inputProvider.GetState().Move.canceled +=  c => moveComponent.direction = c;

        inputProvider.GetState().Look.performed += c => flipComponent.direction.x = c.x > 0 ? 1 : -1;
    }
    public override void Update()
    {
        base.Update();

        if(baseFields.rb.linearVelocityY > 0.4f)
            animationComponent.CrossFade("Jump", 0.1f);
        else if (baseFields.rb.linearVelocityY < -0.4f)
            animationComponent.CrossFade("Fall", 0.1f);
        else if (moveComponent.direction == UnityEngine.Vector2.zero)
            animationComponent.CrossFade("Idle", 0.1f);
        else if (moveComponent.direction.x >= flipComponent.direction.x)
            animationComponent.CrossFade("WalkForward", 0.1f);
        else if (moveComponent.direction.x < flipComponent.direction.x)
            animationComponent.CrossFade("WalkBack", 0.1f);
    }

    [System.Serializable]
    public class PenguinAIComponent : IComponent
    {
        public Transform folow, target;

        public float distanceBetweenTarget, startFolowDist, idleThinkingTime;

        public Vector2 dirToFolow, distanceBetweenFolow;
    }

    public class PenguinAI : BaseAI
    {
        private PenguinAIComponent penguinComponent;
        protected FSMSystem FSMSystem;

        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);

            penguinComponent = owner.GetControllerComponent<PenguinAIComponent>();
            owner.OnUpdate += Update;

            FSMSystem = owner.GetControllerSystem<FSMSystem>();

            InitStates();
        }

        public void InitStates()
        {
            var idleState = new PenguinIdleState(owner);
            var searchState = new PenguinSearchState(owner);

            bool isFollowing = false;

            FSMSystem.AddAnyTransition(searchState, () => {

                if (!isFollowing && (penguinComponent.distanceBetweenFolow.x > penguinComponent.startFolowDist || penguinComponent.distanceBetweenFolow.y > 2))
                {
                    // Начинаем следовать
                    isFollowing = true;
                }
                else if (isFollowing && penguinComponent.distanceBetweenFolow.x < penguinComponent.startFolowDist / 4 )
                {
                    // Хватит следовать, слишком близко
                    isFollowing = false;
                }

                return isFollowing;
            });

            FSMSystem.AddAnyTransition(idleState, () => true);
        }

        public override void OnUpdate()
        {
            Vector2 delta = penguinComponent.folow.position - owner.transform.position;

            // расстояние по X и по Y
            penguinComponent.distanceBetweenFolow.x = Mathf.Abs(delta.x);
            penguinComponent.distanceBetweenFolow.y = Mathf.Abs(delta.y);

            penguinComponent.dirToFolow = (penguinComponent.folow.position - owner.transform.position);
        }
    }

    public class PenguinSearchState : BaseState
    {
        private PenguinAIComponent penguinComponent;
        private IInputProvider inputProvide;
        private MoveComponent moveComponent;
        private AnimationComponent animationComponent;
        private JumpSystem _jumpSystem;
        private JumpComponent _jumpComponent;
        private GroundingComponent _groundingComponent;
        private PlatformSystem _platformSystem;
        public PenguinSearchState(Controller owner) : base(owner)
        {
            penguinComponent = owner.GetControllerComponent<PenguinAIComponent>();
            animationComponent = owner.GetControllerComponent<AnimationComponent>();
            moveComponent = owner.GetControllerComponent<MoveComponent>();
            _jumpComponent = owner.GetControllerComponent<JumpComponent>();
            _groundingComponent = owner.GetControllerComponent<GroundingComponent>();

            inputProvide = owner.GetControllerSystem<IInputProvider>();
            _jumpSystem = owner.GetControllerSystem<JumpSystem>();
            _platformSystem = owner.GetControllerSystem<PlatformSystem>();
        }

        public override void Enter()
        {
        }

        public override void Update()
        {

            inputProvide.GetState().Look.Update(true, penguinComponent.dirToFolow);
            var multiplier = Mathf.Max(1, 1 * penguinComponent.distanceBetweenFolow.x / 2);

            moveComponent.speedMultiplierDynamic = multiplier;
            animationComponent.SetAnimationSpeed(multiplier);

            inputProvide.GetState().Move.Update(true, penguinComponent.dirToFolow.x > 0 ? Vector2.right : Vector2.left);

            if (penguinComponent.dirToFolow.y < -1)
                _platformSystem.Update();
            else if (penguinComponent.dirToFolow.y > 2 && _groundingComponent.isGround)
            {
                _jumpSystem.Jump();
            }
            else if((_jumpComponent.coyotTime > 0 && !_groundingComponent.isGround))
            {
                _jumpSystem.Jump();
            }

            if (penguinComponent.dirToFolow.y < 0.3f && penguinComponent.dirToFolow.y > - 1f && !_jumpComponent.isJumpCuted)
            {
                Debug.Log("Released");
                _jumpSystem.OnJumpUp();
            }
        }

        public override void Exit()
        {
            moveComponent.speedMultiplierDynamic = 1;
            animationComponent.SetAnimationSpeed(1);
            inputProvide.GetState().Move.Update(true, Vector2.zero);
        }
    }


    public class PenguinIdleState : BaseState
    {
        private PenguinAIComponent penguinComponent;
        private IInputProvider inputProvide;
        private Coroutine idleProcess;
        public PenguinIdleState(Controller owner) : base(owner)
        {
            penguinComponent = owner.GetControllerComponent<PenguinAIComponent>();
            inputProvide = owner.GetControllerSystem<IInputProvider>();
        }

        public override void Enter()
        {
            idleProcess = owner.StartCoroutine(IdleProcess());
        }

        public IEnumerator IdleProcess()
        {
            while (true)
            {
                inputProvide.GetState().Move.Update(true, Vector2.zero);

                int a = Random.Range(-1, 2);
                yield return new WaitForSeconds(penguinComponent.idleThinkingTime);
                float t = 2;
                while (t >= 0)
                {
                    t -= Time.deltaTime;
                    if (a != 0)
                    {
                        inputProvide.GetState().Move.Update(true, a > 0 ? Vector2.right : Vector2.left);
                        inputProvide.GetState().Look.Update(true, a > 0 ? Vector2.right : Vector2.left);
                    }
                    yield return null;
                }
            }
        }

        public override void Exit()
        {
            owner.StopCoroutine(idleProcess);
        }
    }


    public abstract class BaseState : States.IState
    {
        protected Controller owner;

        public BaseState(Controller owner)
        {
            this.owner = owner;
        }

        public virtual void Update() { }
        public virtual void FixedUpdate() { }
        public virtual void LateUpdate() { }

        public abstract void Enter();

        public abstract void Exit();
    }


    public class BaseAI : IInputProvider
    {
        public bool isActive = true;
        protected Controller owner;
        protected InputState _inputState;
        public virtual InputState GetState()
        {
            return _inputState;
        }

        public virtual void Initialize(Controller owner)
        {
            this.owner = owner;
            _inputState = new InputState();
        }

        public void Update()
        {
            if (!isActive)
                return;

            OnUpdate();
        }

        public virtual void OnUpdate()
        {
        }
    }
}
