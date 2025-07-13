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
    private SpriteFlipSystem flipSystem = new SpriteFlipSystem();

    public MoveComponent MoveComponent;
    public AnimationComponent AnimationComponent;
    public SpriteFlipComponent FlipComponent = new SpriteFlipComponent();
    public TransformPositioning transformPositioning = new TransformPositioning();
    public RatInputComponent ratInputComponent = new RatInputComponent();

    public IInputProvider InputProvider = new RatInputLogic();

    protected override void Awake()
    {
        base.Awake();
        SubInputs();
    }

    public override void FixedUpdate()
    {
        _moveSystem.Update();
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
            if (hit.collider != null)
                input.x = input.x * -1;

            InputState.Move.Update(true, input);

            if (input.x == 0)
            {
                AnimationComponent.CrossFade("Idle",0.1f);
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
    }

    public void OnDrawGizmos()
    {
        Gizmos.DrawRay(transformPositioning.transformPos[ColorPosNameConst.HEAD].position,Vector2.right * owner.transform.localScale.x * ratInputComponent.headDist);
    }
}

[Serializable]
public class TransformPositioning : IComponent
{
    public SerializedDictionary<ColorPosNameConst, Transform> transformPos = new SerializedDictionary<ColorPosNameConst, Transform>();
}