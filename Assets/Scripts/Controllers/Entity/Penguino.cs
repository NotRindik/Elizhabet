using Controllers;
using Systems;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using static Penguino;

public class Penguino : EntityController
{
    private SpriteFlipSystem _flipSystem = new SpriteFlipSystem();
    private MoveSystem _moveSystem = new MoveSystem();
    private FSMSystem _fasmSystem = new FSMSystem();

    public SpriteFlipComponent flipComponent;
    public MoveComponent moveComponent;
    public AnimationComponent animationComponent;
    public IInputProvider inputProvider = new PenguinAI();
    public FsmComponent FsmComponent = new FsmComponent();
    public PenguinFolowTarget penguin = new PenguinFolowTarget();

    public void Start()
    {
        inputProvider.GetState().Move.performed +=  c => moveComponent.direction = c;
        inputProvider.GetState().Move.canceled +=  c => moveComponent.direction = c;

        inputProvider.GetState().Look.performed += c => flipComponent.direction.x = c.x > 0 ? 1 : -1;
    }
    public override void Update()
    {
        base.Update();

        if (moveComponent.direction == UnityEngine.Vector2.zero)
            animationComponent.CrossFade("Idle", 0.1f);
        else if (moveComponent.direction.x == flipComponent.direction.x)
            animationComponent.CrossFade("WalkForward", 0.1f);
        else if (moveComponent.direction.x < flipComponent.direction.x)
            animationComponent.CrossFade("WalkBack", 0.1f);
    }

    [System.Serializable]
    public struct PenguinFolowTarget : IComponent
    {
        public Transform folow, target;

        public float distanceBetweenFolow, distanceBetweenTarget;

        public Vector2 dirToFolow;
    }

    public class PenguinAI : BaseAI
    {
        private PenguinFolowTarget penguinFolowTarget;
        protected FSMSystem FSMSystem;

        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);

            penguinFolowTarget = owner.GetControllerComponent<PenguinFolowTarget>();
            owner.OnUpdate += Update;

            FSMSystem = owner.GetControllerSystem<FSMSystem>();

            InitStates();
        }

        public void InitStates()
        {
            var idleState = new PenguinIdleState(owner);

            FSMSystem.AddAnyTransition(idleState, () => true);
        }

        public override void OnUpdate()
        {
            penguinFolowTarget.distanceBetweenFolow = Vector2.Distance(owner.transform.position, penguinFolowTarget.folow.position);

            penguinFolowTarget.dirToFolow = (penguinFolowTarget.folow.position - owner.transform.position);

            _inputState.Look.Update(true, penguinFolowTarget.dirToFolow);
        }
    }

    public class PenguinSearchState : BaseState
    {
        private PenguinFolowTarget penguinFolowTarget;
        private IInputProvider inputProvide;
        public PenguinSearchState(Controller owner) : base(owner)
        {
            penguinFolowTarget = owner.GetControllerComponent<PenguinFolowTarget>();
            inputProvide = owner.GetControllerSystem<IInputProvider>();
        }

        public override void Enter()
        {
        }

        public override void Update()
        {
            if (penguinFolowTarget.distanceBetweenFolow > 5)
            {
                inputProvide.GetState().Move.Update(true, penguinFolowTarget.dirToFolow.x > 0 ? Vector2.right : Vector2.left);
            }
            else if (penguinFolowTarget.distanceBetweenFolow < 2)
            {
                inputProvide.GetState().Move.Update(true, Vector2.zero);
            }
        }

        public override void Exit()
        {
        }
    }


    public class PenguinIdleState : BaseState
    {

        public PenguinIdleState(Controller owner) : base(owner)
        {

        }

        public override void Enter()
        {
        }

        public override void Update()
        {

        }

        public override void Exit()
        {
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
