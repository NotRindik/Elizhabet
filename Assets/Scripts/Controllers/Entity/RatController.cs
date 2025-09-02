using Assets.Scripts;
using AYellowpaper.SerializedCollections;
using Controllers;
using System;
using System.Collections;
using Systems;
using UnityEngine;
using UnityEngine.Serialization;
using static RatInputLogic;

public class RatController : EntityController
{
    private MoveSystem _moveSystem = new MoveSystem();
    private SpriteFlipSystem _flipSystem = new SpriteFlipSystem();
    private RotationToFootSystem _rotationToFootSystem = new RotationToFootSystem();
    private GroundingSystem _groundingSystem = new GroundingSystem();
    private ContactDamageSystem _contactDamageSystem = new ContactDamageSystem();
    
    public MoveComponent MoveComponent;
    public AnimationComponent AnimationComponent;
    public SpriteFlipComponent FlipComponent = new SpriteFlipComponent();
    public TransformPositioning transformPositioning = new TransformPositioning();
    public RatInputComponent ratInputComponent = new RatInputComponent();
    public BaseAttackComponent attackComponent = new BaseAttackComponent();
    public RotationToFootComponent rotationToFoot = new RotationToFootComponent();
    public GroundingComponent groundingComponent = new GroundingComponent();
    public ParticleComponent particleComponent;
    public IInputProvider InputProvider = new RatInputLogic();

    public Action<float,Vector2> TakeDamageHandler;
    protected override void Awake()
    {
        MoveComponent.autoUpdate = true;
        base.Awake();
        SubInputs();
        TakeDamageHandler = (c,where) =>
        {
            var hitParticle = Instantiate(particleComponent.hitParticlePrefab,where,Quaternion.identity);
            var bloodParticle = Instantiate(particleComponent.bloodParticlePrefab,where,Quaternion.identity);
            hitParticle.Emit(5);
            bloodParticle.Emit(20);
            StartCoroutine(OnHitProcess());
        };
        
        healthComponent.OnTakeHit += TakeDamageHandler;
        _contactDamageSystem.OnContactDamage += OnContactDamage;
    }

    public void OnContactDamage()
    {
        InputProvider.GetState().Move.Update(true, new Vector2(MoveComponent.direction.x * -1,MoveComponent.direction.y));
    }
    
    public void SubInputs()
    {
        InputProvider.GetState().Move.performed += c =>
        {
            if(!groundingComponent.isGround)
                return;
            MoveComponent.direction = c;
            FlipComponent.direction = c;
        };
        InputProvider.GetState().Move.canceled += c =>
        {
            if(!groundingComponent.isGround)
                return;
            MoveComponent.direction = c;
            FlipComponent.direction = c;

        };
    }

    public IEnumerator OnHitProcess()
    {
        yield return StopMove(0.3f);
        yield return StopMoveUntil(() => groundingComponent.isGround);
    }
    protected override void ReferenceClean()
    {
        healthComponent.OnTakeHit -= TakeDamageHandler;
        _contactDamageSystem.OnContactDamage -= OnContactDamage;
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

}

[Serializable]
public class BaseAttackComponent : IComponent
{
    public LayerMask attackLayer;
    public DamageComponent damage;
    public float knockBackForce;
    public float knockBackForceVertical;
    
    public static bool IsInLayerMask(GameObject obj, LayerMask mask)
    {
        return (mask.value & (1 << obj.layer)) != 0;
    }
}

public class ContactDamageSystem : BaseSystem
{
    private EntityController _entityController;
    private BaseAttackComponent _attackComponent;
    private MoveComponent _moveComponent;
    public Action OnContactDamage;
    public override void Initialize(Controller owner)
    {
        base.Initialize(owner);
        if (base.owner is EntityController entityController)
        {
            _entityController = entityController;
        }
        else
        {
            Debug.LogError("Not Entity");
            return;
        }
        _entityController.OnCollisionEnter2DHandle += ContactDamage;
        _attackComponent = _entityController.GetControllerComponent<BaseAttackComponent>();
        _moveComponent = _entityController.GetControllerComponent<MoveComponent>();
    }

    public void ContactDamage(Collision2D other)
    {
        if (BaseAttackComponent.IsInLayerMask(other.gameObject, _attackComponent.attackLayer))
        {
            if (other.gameObject.TryGetComponent(out Controller controller) )
            {
                var healthSystem = controller.GetControllerSystem<HealthSystem>();
                if (healthSystem != null)
                {
                    var point = other.GetContact(0).point;
                    Debug.Log(point);
                    new Damage(_attackComponent.damage, controller.GetControllerComponent<ProtectionComponent>()).ApplyDamage(healthSystem,point);
                    controller.GetControllerComponent<ControllersBaseFields>().rb.linearVelocity = Vector2.zero;
                    TimeManager.StartHitStop(0.3f,0.3f,0.4f,owner);
                    Vector2 knockDir = ((Vector2)controller.transform.position - other.GetContact(0).point).normalized;
                    knockDir.Normalize(); // пере-нормализуем
                    controller.GetControllerComponent<ControllersBaseFields>().rb.AddForce(new Vector2(knockDir.x * _attackComponent.knockBackForce,knockDir.y * _attackComponent.knockBackForceVertical), ForceMode2D.Impulse);
                    OnContactDamage?.Invoke();
                }
            }
        }
    }
}

public class RotationToFootSystem : BaseSystem,IDisposable
{
    private ControllersBaseFields _baseFields;
    private Rigidbody2D rb => _baseFields.rb;

    private RotationToFootComponent _rotationToFootComponent;

    public override void Initialize(Controller owner)
    {
        base.Initialize(owner);
        _rotationToFootComponent = base.owner.GetControllerComponent<RotationToFootComponent>();
        _baseFields = base.owner.GetControllerComponent<ControllersBaseFields>();
        owner.OnUpdate += Update;
    }

    public override void OnUpdate()
    {
        base.OnUpdate();
        
        if (IsUpsideDown())
        {
            float targetAngle = 0f; // хотим стоять прямо
            float currentAngle = rb.rotation;
            float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.fixedDeltaTime * _rotationToFootComponent.gravityStrength);
            rb.MoveRotation(newAngle);

        }
    }
    bool IsUpsideDown()
    {
        return Mathf.Abs(Mathf.DeltaAngle(rb.rotation, 0f)) > 45f;
    }

    public void Dispose()
    {
        owner.OnUpdate -= Update;
    }
}
[Serializable]
public struct RotationToFootComponent : IComponent
{
    public float gravityStrength;
}

public class RatInputLogic : IInputProvider, IDisposable
{
    private InputState InputState = new InputState();
    private MoveComponent moveComponent;
    private AnimationComponent AnimationComponent;
    private RatInputComponent ratInputComponent;
    private Controller owner;
    private TransformPositioning transformPositioning;

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
        moveComponent = owner.GetControllerComponent<MoveComponent>();
        transformPositioning = owner.GetControllerComponent<TransformPositioning>();
        AnimationComponent = owner.GetControllerComponent<AnimationComponent>();
        ratInputComponent = owner.GetControllerComponent<RatInputComponent>();

        owner.StartCoroutine(AIProcess());

        owner.OnGizmosUpdate += OnDrawGizmos;
    }

    public void OnUpdate()
    {
    }

    private IEnumerator AIProcess()
    {
        var input = new Vector3(UnityEngine.Random.Range(-1, 1) == -1 ? -1 : 1, 0, 0);
        while (true)
        {
            yield return null;


            RaycastHit2D hit = Physics2D.Raycast(transformPositioning.transformPos[ColorPosNameConst.HEAD].position,
                Vector2.right * owner.transform.localScale.x, ratInputComponent.headDist, ratInputComponent.layer);

            RaycastHit2D hitGround = Physics2D.Raycast(transformPositioning.transformPos[ColorPosNameConst.HEAD].position,
                Vector2.down, ratInputComponent.ratDist, ratInputComponent.layer);

            if (hit.collider != null || hitGround.collider == null)
            {
                input.x = InputState.Move.ReadValue().x * -1;
                InputState.Move.Update(true, input);
            }

            if (InputState.Move.ReadValue().x == 0)
            {
                if(AnimationComponent.currentState != "Idle")AnimationComponent.CrossFade("Idle",0.1f);
                InputState.Move.Update(true, input);
            }
            else
            {
                if(AnimationComponent.currentState != "Run")AnimationComponent.CrossFade("Run", 0.1f);
            }
        }
    }
    [Serializable]
    public class RatInputComponent : IComponent
    {
        public LayerMask layer;
        public float headDist;
        public float ratDist = 1;
    }

    public void OnDrawGizmos()
    {
        Gizmos.DrawRay(transformPositioning.transformPos[ColorPosNameConst.HEAD].position,Vector2.right * owner.transform.localScale.x * ratInputComponent.headDist);
        Gizmos.DrawRay(transformPositioning.transformPos[ColorPosNameConst.HEAD].position,Vector2.down * ratInputComponent.ratDist);
    }
}

[Serializable]
public class TransformPositioning : IComponent
{
    public SerializedDictionary<ColorPosNameConst, Transform> transformPos = new SerializedDictionary<ColorPosNameConst, Transform>();
}

[Serializable]
public class WallStickComponent : IComponent
{
    public float stickForce = 10f;
}

public class WallStickSystem : BaseSystem
{
    private WallStickComponent _wallStickComponent;
    private ControllersBaseFields _controllersBase;
    private Vector2 surfaceNormal = Vector2.up;

    public override void Initialize(Controller owner)
    {
        base.Initialize(owner);
        _wallStickComponent = owner.GetControllerComponent<WallStickComponent>();
        _controllersBase = owner.GetControllerComponent<ControllersBaseFields>();
        owner.OnFixedUpdate += Update;
        owner.OnGizmosUpdate += OnDrawGizmos;
    }

    public override void OnUpdate()
    {

        Vector2 tangent = new Vector2(-surfaceNormal.y, surfaceNormal.x);

        // ���������������� � �����������
        _controllersBase.rb.AddForce(-surfaceNormal * _wallStickComponent.stickForce);

        // ������ raycast "�����" � "����", ����� ����� ����� ����
        RaycastHit2D hit = Physics2D.Raycast(owner.transform.position + (Vector3)tangent * 0.5f, -tangent, 0.6f);
        if (hit.collider != null)
        {
            surfaceNormal = hit.normal;
            // ��������� ����� ��������� (�������������)
            float angle = Mathf.Atan2(surfaceNormal.y, surfaceNormal.x) * Mathf.Rad2Deg;
            owner.transform.rotation = Quaternion.Euler(0, 0, angle - 90); // ������ ��� ������
        }
    }

    public void OnDrawGizmos()
    {
        Vector2 tangent = new Vector2(-surfaceNormal.y, surfaceNormal.x);
        Gizmos.DrawRay(owner.transform.position + (Vector3)tangent * 0.5f, -tangent* 0.6f);
    }

}