using Assets.Scripts;
using AYellowpaper.SerializedCollections;
using Controllers;
using System;
using System.Collections;
using Systems;
using UnityEngine;
using static RatInputLogic;

public class RatController : EntityController
{
    private MoveSystem _moveSystem = new MoveSystem();
    private SpriteFlipSystem _flipSystem = new SpriteFlipSystem();

    public MoveComponent MoveComponent;
    public AnimationComponent AnimationComponent;
    public SpriteFlipComponent FlipComponent = new SpriteFlipComponent();
    public TransformPositioning transformPositioning = new TransformPositioning();
    public RatInputComponent ratInputComponent = new RatInputComponent();
    public MobAttackComponent attackComponent = new MobAttackComponent();

    public IInputProvider InputProvider = new RatInputLogic();
    protected override void Awake()
    {
        base.Awake();
        SubInputs();
    }

    public override void FixedUpdate()
    {
        _moveSystem.Update();

        if (baseFields.rb.linearVelocity.magnitude < 0.5f)
        {
            // ��������: ����� ������������� (������� ����)
            if (Vector2.Dot(transform.up, Vector2.down) > 0.7f) // ��� .y < -0.7f
            {
                // ��������� �������, ����� �������������
                Vector2 impulse = (Vector2)transform.right * 3f + Vector2.up * 5f;
                baseFields.rb.AddForce(impulse, ForceMode2D.Impulse);
            }
        }
    }

    public void SubInputs()
    {
        InputProvider.GetState().Move.performed += c =>
        {
            MoveComponent.direction = c;
            FlipComponent.direction = c;
        };
        InputProvider.GetState().Move.canceled += c =>
        {
            MoveComponent.direction = c;
            FlipComponent.direction = c;

        };
    }
    public static bool IsInLayerMask(GameObject obj, LayerMask mask)
    {
        return (mask.value & (1 << obj.layer)) != 0;
    }
    private void OnCollisionEnter2D(Collision2D other)
    {
        Debug.Log("A");
        Debug.Log($"{other.gameObject.layer} and {attackComponent.attackLayer.value}");
        if (IsInLayerMask(other.gameObject, attackComponent.attackLayer))
        {
            if (other.gameObject.TryGetComponent(out Controller controller) )
            {
                var healthSystem = controller.GetControllerSystem<HealthSystem>();
                if (healthSystem != null)
                {
                    healthSystem.TakeHit(attackComponent.damage);
                    controller.GetControllerComponent<ControllersBaseFields>().rb.linearVelocity = Vector2.zero;
                    TimeManager.StartHitStop(0.3f,0.3f,0.4f,this);
                    Vector2 knockDir = ((Vector2)controller.transform.position - other.GetContact(0).point).normalized;
                    knockDir.y *= 0.06f; // ослабить вертикальную составляющую
                    knockDir.Normalize(); // пере-нормализуем
                    controller.GetControllerComponent<ControllersBaseFields>().rb.AddForce(knockDir * attackComponent.knockBackForce, ForceMode2D.Impulse);
                    InputProvider.GetState().Move.Update(true, new Vector2(MoveComponent.direction.x * -1,MoveComponent.direction.y));
                }
            }
        }
    }

}
[Serializable]
public struct MobAttackComponent
{
    public LayerMask attackLayer;
    public float damage;
    public float knockBackForce;
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
                AnimationComponent.CrossFade("Idle",0.1f);
                InputState.Move.Update(true, input);
            }
            else
            {
                AnimationComponent.CrossFade("Run", 0.1f);
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