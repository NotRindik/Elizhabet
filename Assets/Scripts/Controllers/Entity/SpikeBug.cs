using System;
using System.Collections;
using Assets.Scripts;
using Controllers;
using Systems;
using UnityEngine;

public class SpikeBug : EntityController
{
    private MoveSystem _moveSystem = new MoveSystem();

    private GroundingSystem _groundingSystem = new GroundingSystem();
    private SpriteFlipSystem _spriteFlipSystem = new SpriteFlipSystem();
    private WallWalkSystem _walkSystem = new WallWalkSystem();
    private CustomGravitySystem _customGravitySystem = new CustomGravitySystem(); 
    private ContactDamageSystem _contactDamageSystem = new ContactDamageSystem();
    
    public MoveComponent moveComponent;
    public AnimationComponent animationComponent;
    public IInputProvider InputProvider = new SpikeBugInputLogic();
    public GroundingComponent groundingComponent;
    public SpriteFlipComponent flipComponent;
    public WallWalkComponent wallWalkComponent;
    public TransformPositioning transformPositioning;
    public CustomGravityComponent customGravity;
    public BaseAttackComponent mobAttackComponent;
    public Action<HitInfo> TakeDamageHandler;

    public ParticleComponent particleComponent;
    
    public IEnumerator OnHitProcess()
    {
        yield return StopMove(0.3f);
        yield return StopMoveUntil(() => groundingComponent.isGround);
    }
    public IEnumerator StopMove(float duration)
    {
        _moveSystem.IsActive = false;
        yield return new WaitForSeconds(duration);
        _moveSystem.IsActive = true;
    }
    public IEnumerator StopMoveUntil(Func<bool> f)
    {
        _moveSystem.IsActive = false;
        yield return new WaitUntil(f);
        _moveSystem.IsActive = true;
    }
    public void OnContactDamage()
    {
        StartCoroutine( StopMove(0.3f));
        InputProvider.GetState().Move.Update(true, new Vector2(moveComponent.direction.x * -1,moveComponent.direction.y));
    }
    

    protected override void Awake()
    {
        base.Awake();
        SubInputs();
        TakeDamageHandler = (where) =>
        {
            var hitParticle = Instantiate(particleComponent.hitParticlePrefab,(Vector3)where.GetHitPos(),Quaternion.identity);
            var bloodParticle = Instantiate(particleComponent.bloodParticlePrefab,(Vector3)where.GetHitPos(),Quaternion.identity);
            var mainBlood = bloodParticle.main;
            mainBlood.startColor = new Color(1, 0.5f, 0,1);
            var subEmitters = bloodParticle.subEmitters;
            ParticleSystem sub = subEmitters.GetSubEmitterSystem(0);
            var subMain = sub.main;
            subMain.startColor = new Color(1, 0.3f, 0, 1);
            
            bloodParticle.Emit(20);
            hitParticle.Emit(5);
            StartCoroutine(OnHitProcess());
        };

        healthComponent.OnTakeHit += TakeDamageHandler;
        _contactDamageSystem.OnContactDamage += OnContactDamage;
    }
    
    public void SubInputs()
    {
        InputProvider.GetState().Move.performed += c =>
        {
            if(!groundingComponent.isGround)
                return;
            var value = c.ReadValue<Vector2>();
            moveComponent.direction = value;
            flipComponent.direction = value;
        };
        InputProvider.GetState().Move.canceled += c =>
        {
            if(!groundingComponent.isGround)
                return;
            var value = c.ReadValue<Vector2>();
            moveComponent.direction = value;
            flipComponent.direction = value;

        };
    }
    
    protected override void ReferenceClean()
    {
        healthComponent.OnTakeHit -= TakeDamageHandler;
        _contactDamageSystem.OnContactDamage -= OnContactDamage;
    }
}

public class SpikeBugInputLogic : IInputProvider,IDisposable
{
    private InputState InputState = new InputState();
    private AnimationComponent AnimationComponent;
    private Controller owner;

    public void Dispose()
    {
        owner.OnUpdate -= OnUpdate;
    }

    public InputState GetState()
    {
        return InputState;
    }

    public void Initialize(AbstractEntity owner)
    {
        if (owner is not Controller controller)
        {
            Debug.Log("Not Legacy Controller");
            return;
        }
        else
        {
            this.owner = controller;
            AnimationComponent = owner.GetControllerComponent<AnimationComponent>();

            controller.StartCoroutine(AIProcess());
        }
    }

    public void OnUpdate()
    {
    }

    private IEnumerator AIProcess()
    {
        var input = new Vector2(UnityEngine.Random.Range(-1, 1) == -1 ? -1 : 1, 0);
        yield return new WaitForSeconds(0.1f);
        while (true)
        {
            yield return null;

            if (AnimationComponent.currentState != "walk")
            {
                AnimationComponent.CrossFade("walk", 0.1f);
                InputState.Move.Update(true, input);
            }
        }
    }
}

public class CustomGravitySystem : BaseSystem , IDisposable
{
    private CustomGravityComponent _customGravityComponent;
    private ControllersBaseFields _baseFields;

    private Rigidbody2D Rb => _baseFields.rb;
    
    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        _customGravityComponent = owner.GetControllerComponent<CustomGravityComponent>();
        _baseFields = owner.GetControllerComponent<ControllersBaseFields>();
        if(_customGravityComponent.autoDisableRb)
            Rb.gravityScale = 0;
        base.owner.OnFixedUpdate += UpdateGravity;
    }

    public void UpdateGravity()
    {
        if(!IsActive)
            return;

        Vector3 customGravity = _customGravityComponent.gravityVector * _customGravityComponent.gravityStrength;
        Rb.AddForce(customGravity, ForceMode2D.Force);
    }
    public void Dispose()
    {
        base.owner.OnFixedUpdate -= UpdateGravity;
    }
}

[Serializable]
public class CustomGravityComponent : IComponent
{
    public bool autoDisableRb;

    public Vector3 gravityVector;

    public float gravityStrength;
}
public class WallWalkSystem : BaseSystem,IDisposable
{
    private TransformPositioning _transformPositioning;
    private WallWalkComponent _wallWalkComponent;
    private GroundingComponent _groundingComponent;
    private CustomGravityComponent _customGravityComponent;

    private Coroutine rotationCooldown,idleRotDelay;

    private ControllersBaseFields _baseFields;

    private float fallTimer = 0f;
    private const float maxFallTimeWithoutGround = 0.2f; // сколько секунд в воздухе до сброса вращения
    private bool hasResetRotation = false;
    
    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);

        base.owner.OnGizmosUpdate += OnDrawGizmos;
        _transformPositioning = base.owner.GetControllerComponent<TransformPositioning>();
        _wallWalkComponent = base.owner.GetControllerComponent<WallWalkComponent>();
        _customGravityComponent = base.owner.GetControllerComponent<CustomGravityComponent>();
        _groundingComponent = base.owner.GetControllerComponent<GroundingComponent>();
        _baseFields = base.owner.GetControllerComponent<ControllersBaseFields>();
        owner.OnUpdate += Update;
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        Vector3 headPos = _transformPositioning.transformPos[ColorPosNameConst.HEAD].position;
        Vector3 forward = transform.right * Mathf.Sign(transform.localScale.x);
        RaycastHit2D wallhit = Physics2D.Raycast(headPos, forward, _wallWalkComponent.wallCheckDistance, _wallWalkComponent.wallLayer);

        Vector3 dir = transform.TransformDirection(Vector3.up);
        if (Mathf.Abs(dir.x) > 0.99f) dir.y = 0f;
        if (Mathf.Abs(dir.y) > 0.99f) dir.x = 0f;
        dir = dir.normalized;
        _customGravityComponent.gravityVector = dir;

        if (!_groundingComponent.isGround)
        {
            _baseFields.rb.linearVelocity = Vector2.zero;
            if(idleRotDelay == null) 
                idleRotDelay = mono.StartCoroutine(
                    std.Utilities.Invoke(() => {
                        mono.transform.rotation = Quaternion.Euler(Vector2.zero);
                        idleRotDelay = null;
                    },0.1f)
                );
        }
        else
        {
            fallTimer = 0f;
            hasResetRotation = false;
            if(idleRotDelay != null)
            {
                mono.StopCoroutine(idleRotDelay);
                idleRotDelay = null;
            }

            if (wallhit.collider != null)
            {
                if (rotationCooldown == null)
                {
                    rotationCooldown = mono.StartCoroutine(
                        RotationWithCoolDown(new Vector3(0, 0, 90 * transform.localScale.x), 0.2f)
                    );
                }
            }
        }
    }


    private IEnumerator RotationWithCoolDown(Vector3 rotation, float t)
    {
        _baseFields.rb.linearVelocity = Vector2.zero;
        transform.Rotate(rotation);
        yield return new WaitForSeconds(t);
        rotationCooldown = null;
    }
    
    private IEnumerator RotationUntil(Vector3 rotation, Func<bool> f)
    {
        _baseFields.rb.linearVelocity = Vector2.zero;
        transform.Rotate(rotation);
        yield return new WaitUntil(f);
        rotationCooldown = null;
    }


    public void OnDrawGizmos()
    {
        Gizmos.DrawRay(_transformPositioning.transformPos[ColorPosNameConst.HEAD].position,transform.right * _wallWalkComponent.wallCheckDistance * transform.localScale.x);
    }
    public void Dispose()
    {
        owner.OnUpdate -= Update;
    }
}

[Serializable]
public class WallWalkComponent : IComponent
{
    public LayerMask wallLayer;
    public float wallCheckDistance;
    public float groundCheckDistance;

}