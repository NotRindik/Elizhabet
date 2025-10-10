using Assets.Scripts;
using Controllers;
using System;
using System.Collections;
using System.Linq;
using Systems;
using UnityEngine;

public class PenguinAttackSystem : AttackSystem
{
    public override void AllowAttack()
    {
        _attackComponent.canAttack = true;
    }
} 


public class Penguino : EntityController
{
    private SpriteFlipSystem _flipSystem = new SpriteFlipSystem();
    private MoveSystem _moveSystem = new MoveSystem();
    private FSMSystem _fasmSystem = new FSMSystem();
    private JumpSystem _jumpSystem = new JumpSystem();
    private GroundingSystem _groundingSystem = new GroundingSystem();
    private PlatformSystem _platformSystem = new PlatformSystem();
    private FrictionSystem _frictionSystem = new FrictionSystem();
    private CustomGravitySystem _customGravitySystem = new CustomGravitySystem();
    private PenguinAttackSystem _attackSystem = new PenguinAttackSystem();
    private ManaSystem _manaSystem = new ManaSystem();

    public SpriteFlipComponent flipComponent;
    public MoveComponent moveComponent;
    public AnimationComponent animationComponent;
    public IInputProvider inputProvider = new PenguinAI();
    public FsmComponent FsmComponent = new FsmComponent();
    public PenguinAIComponent penguin = new PenguinAIComponent();
    public JumpComponent jumpComponent = new JumpComponent();
    public GroundingComponent groundingComponent = new GroundingComponent();
    public PlatformComponent platformComponent = new PlatformComponent();
    public CustomGravityComponent customGravityComponent = new CustomGravityComponent();
    public AttackComponent attackComponent = new AttackComponent();
    public ManaComponent manaComponent = new ManaComponent();

    public bool isFly;

    private AudioClip _jetClip;

    private Coroutine JetSoundProcess;

    public void Start()
    {
        _jetClip = Resources.Load<AudioClip>($"{FileManager.SFX}jet");

        inputProvider.GetState().Move.performed +=  c => {
            var val = c.ReadValue<Vector2>();
            if (val == Vector2.down)
                _platformSystem.Update();

            moveComponent.direction = val;
        };
        inputProvider.GetState().Move.canceled +=  c => moveComponent.direction = c.ReadValue<Vector2>();

        inputProvider.GetState().Look.performed += c => flipComponent.direction.x = c.ReadValue<Vector2>().x > 0 ? 1 : -1;
        inputProvider.GetState().Jump.performed += c => 
        {
            if (c.ReadValue<bool>() == true)
                _jumpSystem.Jump();
            else
                _jumpSystem.OnJumpUp();
        };

        inputProvider.GetState().Fly.performed += c =>
        {
            isFly = c.ReadValue<bool>();
            var rb = baseFields.rb;

            if (isFly)
            {
                rb.gravityScale = 0;
                rb.linearVelocity = Vector2.zero;
                _customGravitySystem.IsActive = true;

                var multiplier = 100 * Mathf.Max(3, penguin.distanceBetweenFolow.y);

                var dir = penguin.dirToFolow.normalized;

                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                // текущий угол (в градусах)
                float currentAngle = transform.eulerAngles.z;

                // плавно двигаем угол к целевому
                float smoothAngle = Mathf.MoveTowardsAngle(currentAngle, angle - 90f, 180f * Time.deltaTime);

                // примен€ем
                transform.rotation = Quaternion.Euler(0f, 0f, smoothAngle);

                if(JetSoundProcess == null)
                    JetSoundProcess = StartCoroutine(std.Utilities.InvokeRepeatedly(() => AudioManager.instance.PlaySoundEffect(_jetClip),0.07f));

                customGravityComponent.gravityVector = dir;

                customGravityComponent.gravityStrength = multiplier;
                foreach (var col in baseFields.collider)
                {
                    col.enabled = false;
                }
            }
            else
            {
                _customGravitySystem.IsActive = false;
                rb.gravityScale = 1;
                rb.linearVelocityY = 0.5f;
                rb.linearVelocityX = 0;
                transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                StopCoroutine(JetSoundProcess);
                JetSoundProcess = null;
                foreach (var col in baseFields.collider)
                {
                    col.enabled = true;
                }
            }
        };

        _customGravitySystem.IsActive = false ;
    }

    public override void Update()
    {
        base.Update();

        if(baseFields.rb.linearVelocityY > 0.4f)
            animationComponent.CrossFade("Jump", 0.1f);
        else if (baseFields.rb.linearVelocityY < -0.4f || isFly)
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

        public float distanceBetweenTarget, startFolowDist, idleThinkingTime,startFlyDistance;

        public Vector2 dirToFolow, distanceBetweenFolow;

        public Transform downCheackerPos;

        public Vector2 downCheackerSize;

        public LayerMask groundLayer;

        public ParticleSystem jetpackParticle;

        public bool CheackDown => Physics2D.BoxCast(downCheackerPos.position,downCheackerSize,0,Vector2.zero,0,groundLayer);

        public float attackRaduius;
        public Transform handRotatingTransform;

        public System.Action<Collider2D[]> onHited;

        public LayerMask enemyLayer;
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

            owner.StartCoroutine(SlowUpdate());
        }
        private bool IsFreeAroundPenguin()
        {
            float checkRadius = 0.2f; // радиус проверки вокруг пингвина

            return !Physics2D.OverlapCircle(transform.position, checkRadius, penguinComponent.groundLayer);
        }

        public void InitStates()
        {
            var idleState = new PenguinIdleState(owner);
            var searchState = new PenguinSearchState(owner);
            var flyState = new PenguinFlyState(owner);

            bool isFollowing = false;
            bool isFly = false;

            FSMSystem.AddAnyTransition(flyState, () => {

                if (!isFly && (penguinComponent.distanceBetweenFolow.y > penguinComponent.startFlyDistance))
                {
                    // Ќачинаем следовать
                    isFly = true;
                }
                else if (isFly && penguinComponent.distanceBetweenFolow.y < 1f && penguinComponent.distanceBetweenFolow.x < 1f  && IsFreeAroundPenguin())
                {
                    // ’ватит следовать, слишком близко
                    isFly = false;
                }

                return isFly;
            });


            FSMSystem.AddAnyTransition(searchState, () => {

                if (!isFollowing && (penguinComponent.distanceBetweenFolow.x > penguinComponent.startFolowDist || penguinComponent.distanceBetweenFolow.y > 1))
                {
                    // Ќачинаем следовать
                    isFollowing = true;
                }
                else if (isFollowing && penguinComponent.distanceBetweenFolow.x < penguinComponent.startFolowDist / 4 && Mathf.Abs( penguinComponent.distanceBetweenFolow.y) < 1f )
                {
                    // ’ватит следовать, слишком близко
                    isFollowing = false;
                }

                return isFollowing;
            });

            FSMSystem.AddAnyTransition(idleState, () => true);
        }

        public unsafe override void OnUpdate()
        {
            Vector2 delta = penguinComponent.folow.position - owner.transform.position;

            // рассто€ние по X и по Y
            penguinComponent.distanceBetweenFolow.x = Mathf.Abs(delta.x);
            penguinComponent.distanceBetweenFolow.y = Mathf.Abs(delta.y);

            penguinComponent.dirToFolow = (penguinComponent.folow.position - owner.transform.position);
        }

        public IEnumerator SlowUpdate()
        {
            penguinComponent.onHited += (col) =>
            {
                penguinComponent.target = col
                    .Select(c => new
                    {
                        collider = c,
                        distance = Vector2.Distance(transform.position, c.transform.position),
                        visible = !Physics2D.Linecast(transform.position, c.transform.position, penguinComponent.groundLayer)
                    })
                    .Where(x => x.visible)
                    .OrderBy(x => x.distance)
                    .Select(x => x.collider)
                    .FirstOrDefault().transform;


            };

            while (true)
            {
                yield return new WaitForSeconds(0.5f);
                penguinComponent.onHited.Invoke(Physics2D.OverlapCircleAll(transform.position, penguinComponent.attackRaduius, penguinComponent.enemyLayer));
            }
        }

        void Dispose()
        {
            GetState().Dispose();
            penguinComponent.onHited = null;
        }
    }

    public class PenguinSearchState : BaseState
    {
        private PenguinAIComponent penguinComponent;
        private IInputProvider inputProvide;
        private MoveComponent moveComponent;
        private AnimationComponent animationComponent;
        private JumpComponent _jumpComponent;
        private GroundingComponent _groundingComponent;
        public PenguinSearchState(Controller owner) : base(owner)
        {
            penguinComponent = owner.GetControllerComponent<PenguinAIComponent>();
            animationComponent = owner.GetControllerComponent<AnimationComponent>();
            moveComponent = owner.GetControllerComponent<MoveComponent>();
            _jumpComponent = owner.GetControllerComponent<JumpComponent>();
            _groundingComponent = owner.GetControllerComponent<GroundingComponent>();

            inputProvide = owner.GetControllerSystem<IInputProvider>();
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
            {
                inputProvide.GetState().Move.Update(true, Vector2.down);
            }
            else if (penguinComponent.dirToFolow.y > 2 && _groundingComponent.isGround)
            {
                inputProvide.GetState().Jump.Update(true, true);
            }
            else if ((_jumpComponent.coyotTime > 0 && !_groundingComponent.isGround))
            {
                inputProvide.GetState().Jump.Update(true, true);
            }

            if (penguinComponent.dirToFolow.y < 0 && !_jumpComponent.isJumpCuted)
            {
                Debug.Log("Released");
                inputProvide.GetState().Jump.Update(true, false);
            }
        }

        public override void Exit()
        {
            moveComponent.speedMultiplierDynamic = 1;
            animationComponent.SetAnimationSpeed(1);
            inputProvide.GetState().Move.Update(true, Vector2.zero);
        }
    }

    public class PenguinAttackState : BaseState
    {
        private PenguinAIComponent penguinComponent;
        private IInputProvider inputProvide;
        private MoveComponent moveComponent;
        private AnimationComponent animationComponent;
        private JumpComponent _jumpComponent;
        private GroundingComponent _groundingComponent;

        private Action<Collider2D[]> onColliderDataChange;
        public PenguinAttackState(Controller owner) : base(owner)
        {
            penguinComponent = owner.GetControllerComponent<PenguinAIComponent>();
            animationComponent = owner.GetControllerComponent<AnimationComponent>();
            moveComponent = owner.GetControllerComponent<MoveComponent>();
            _jumpComponent = owner.GetControllerComponent<JumpComponent>();
            _groundingComponent = owner.GetControllerComponent<GroundingComponent>();

            inputProvide = owner.GetControllerSystem<IInputProvider>();

            onColliderDataChange = col =>
            {

            };

            penguinComponent.onHited += onColliderDataChange;
        }
        //TODO: использу€ таргет наводитс€ на ближайшего врага и стрел€ть по очеред€м по 3м пул€м
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

                int a = UnityEngine.Random.Range(-1, 2);
                yield return new WaitForSeconds(penguinComponent.idleThinkingTime);
                float t = 2;
                while (t >= 0)
                {
                    if (!penguinComponent.CheackDown)
                    {
                        a *= -1;
                    }

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

    public class PenguinFlyState : BaseState
    {
        private PenguinAIComponent penguinComponent;
        private IInputProvider inputProvide;

        private ParticleSystem jetpackParticle;
        public PenguinFlyState(Controller owner) : base(owner)
        {
            penguinComponent = owner.GetControllerComponent<PenguinAIComponent>();
            inputProvide = owner.GetControllerSystem<IInputProvider>();
            jetpackParticle = penguinComponent.jetpackParticle;
        }

        public override void Enter()
        {
            jetpackParticle.Play();
        }

        public override void Update()
        {

            inputProvide.GetState().Look.Update(true, penguinComponent.dirToFolow);

            inputProvide.GetState().Fly.Update(true,true);
        }
        public override void Exit()
        {
            inputProvide.GetState().Fly.Update(true, false);
            jetpackParticle.Stop();
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
        protected Transform transform => owner.transform;
        protected GameObject gameObject => owner.gameObject;
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
