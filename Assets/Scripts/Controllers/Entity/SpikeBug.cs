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
    public MobAttackComponent mobAttackComponent;
    public Action<float> TakeDamageHandler;
    
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
        InputProvider.GetState().Move.Update(true, new Vector2(moveComponent.direction.x * -1,moveComponent.direction.y));
    }
    

    protected override void Awake()
    {
        base.Awake();
        SubInputs();
        TakeDamageHandler = c =>
        {
            StartCoroutine(OnHitProcess());
        };
        
        healthComponent.OnCurrHealthDataChanged += TakeDamageHandler;
        _contactDamageSystem.OnContactDamage += OnContactDamage;
    }
    
    public void SubInputs()
    {
        InputProvider.GetState().Move.performed += c =>
        {
            if(!groundingComponent.isGround)
                return;
            moveComponent.direction = c;
            flipComponent.direction = c;
        };
        InputProvider.GetState().Move.canceled += c =>
        {
            if(!groundingComponent.isGround)
                return;
            moveComponent.direction = c;
            flipComponent.direction = c;

        };
    }
    
    protected override void ReferenceClean()
    {
        healthComponent.OnCurrHealthDataChanged -= TakeDamageHandler;
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

    public void Initialize(Controller owner)
    {
        this.owner = owner;
        AnimationComponent = owner.GetControllerComponent<AnimationComponent>();

        owner.StartCoroutine(AIProcess());
    }

    public void OnUpdate()
    {
    }

    private IEnumerator AIProcess()
    {
        var input = new Vector3(UnityEngine.Random.Range(-1, 1) == -1 ? -1 : 1, 0, 0);
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
    
    public override void Initialize(Controller owner)
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

    private Coroutine rotationCooldown;

    private ControllersBaseFields _baseFields;
    
    public override void Initialize(Controller owner)
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

        // Направление "вперёд" и "вниз" относительно ориентации
        Vector3 forward = owner.transform.right * Mathf.Sign(owner.transform.localScale.x);

        // Проверка на стену спереди
        RaycastHit2D wallhit = Physics2D.Raycast(headPos, forward, _wallWalkComponent.wallCheckDistance, _wallWalkComponent.wallLayer);
    
        // Проверка на землю под ногами

        // Установка гравитации (очищенное направление)
        Vector3 dir = owner.transform.TransformDirection(Vector3.up);
        if (Mathf.Abs(dir.x) > 0.99f) dir.y = 0f;
        if (Mathf.Abs(dir.y) > 0.99f) dir.x = 0f;
        dir = dir.normalized;
        _customGravityComponent.gravityVector = dir;

        if (wallhit.collider != null && _groundingComponent.isGround)
        {
            if(rotationCooldown == null)
                rotationCooldown = owner.StartCoroutine(RotationWithCoolDown(new Vector3(0,0,90 * owner.transform.localScale.x),0.3f));
        }
        else if(!_groundingComponent.isGround)
        {
            if(rotationCooldown == null)
                rotationCooldown = owner.StartCoroutine(RotationUntil(new Vector3(0,0,-90 * owner.transform.localScale.x),() => _groundingComponent.isGround));
        }
    }

    private IEnumerator RotationWithCoolDown(Vector3 rotation, float t)
    {
        _baseFields.rb.linearVelocity = Vector2.zero;
        owner.transform.Rotate(rotation);
        yield return new WaitForSeconds(t);
        rotationCooldown = null;
    }
    
    private IEnumerator RotationUntil(Vector3 rotation, Func<bool> f)
    {
        _baseFields.rb.linearVelocity = Vector2.zero;
        owner.transform.Rotate(rotation);
        yield return new WaitUntil(f);
        rotationCooldown = null;
    }


    public void OnDrawGizmos()
    {
        Gizmos.DrawRay(_transformPositioning.transformPos[ColorPosNameConst.HEAD].position,owner.transform.right * _wallWalkComponent.wallCheckDistance * owner.transform.localScale.x);
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