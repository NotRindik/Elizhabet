using System;
using States;
using Systems;
using UnityEngine;
using Random = UnityEngine.Random;

public class WormAI : BaseAI
{
    private FSMSystem _fsm;
    private TargetSearchComponent _targetSearch;
    private FlyingMoveComponent FlyingMoveComponent;

    private Action<InputContext> ctx;

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        SetState(new InputState());

        _fsm = owner.GetControllerSystem<FSMSystem>();
        _targetSearch = owner.GetControllerComponent<TargetSearchComponent>();
        FlyingMoveComponent = owner.GetControllerComponent<FlyingMoveComponent>();

        _fsm.AddAnyTransition(new WormIdleState(owner), () => _targetSearch.CurrentTarget == null);
        _fsm.AddAnyTransition(new WormChaseState(owner), () => _targetSearch.CurrentTarget != null);

        ctx = context => FlyingMoveComponent.MoveDir = context.ReadValue<Vector2>();
        
        GetState().Move.performed += ctx;
    }

    public void Dispose()
    {
        GetState().Move.performed -= ctx;
    }
}

public class WormChaseState : BasicState
{
    private IInputProvider _input;
    private TargetSearchComponent _targetSearch;
    private Transform _self;

    private Vector2 _offset;
    private float _offsetTimer;

    private float _offsetRadius = 1.5f;
    private float _repathTime = 1.5f;

    private float _avoidStrength = 2f;
    private float _avoidDistance = 1.5f;

    public WormChaseState(AbstractEntity entity) : base(entity)
    {
        _input = entity.GetControllerSystem<IInputProvider>();
        _targetSearch = entity.GetControllerComponent<TargetSearchComponent>();
        _self = entity.transform;
    }

    public override void Enter()
    {
        PickOffset();
    }
    public override void Exit()
    {
    }

    public override void Update()
    {
        var target = _targetSearch.CurrentTarget;

        if (target == null)
        {
            _input.GetState().Move.Update(true, Vector2.zero);
            return;
        }

        _offsetTimer += Time.deltaTime;

        if (_offsetTimer > _repathTime)
        {
            PickOffset();
        }

        Vector2 targetPos = (Vector2)target.position + _offset;
        Vector2 currentPos = _self.position;

        Vector2 desiredDir = (targetPos - currentPos).normalized;
        
        if (IsBlocked(currentPos, targetPos))
        {
            Vector2 avoid = CalculateAvoidance(currentPos, desiredDir);
            desiredDir = (desiredDir + avoid).normalized;
        }

        _input.GetState().Move.Update(true, desiredDir);
    }

    void PickOffset()
    {
        _offsetTimer = 0f;
        _offset = Random.insideUnitCircle * _offsetRadius;
    }

    bool IsBlocked(Vector2 from, Vector2 to)
    {
        Vector2 dir = to - from;
        float dist = dir.magnitude;

        var hit = Physics2D.Raycast(from, dir.normalized, dist, _targetSearch.blockLayer);

        return hit.collider != null;
    }

    Vector2 CalculateAvoidance(Vector2 pos, Vector2 forward)
    {
        Vector2 left = new Vector2(-forward.y, forward.x);
        Vector2 right = -left;

        float leftHit = Cast(pos, left);
        float rightHit = Cast(pos, right);

        // выбираем сторону где больше свободного места
        if (leftHit > rightHit)
            return left * _avoidStrength;
        else
            return right * _avoidStrength;
    }

    float Cast(Vector2 pos, Vector2 dir)
    {
        var hit = Physics2D.Raycast(pos, dir, _avoidDistance, _targetSearch.blockLayer);
        return hit.collider ? hit.distance : _avoidDistance;
    }
}

[System.Serializable]
public class WormIdleComponent : IComponent
{
    public float amplitude = 0.5f;
    public float frequency = 1.5f;

    public float noiseStrength = 0.3f;
    public float noiseSpeed = 0.8f;

    public float phaseOffset; // стабильный сдвиг
}

public class WormIdleState : BasicState
{
    private IInputProvider _input;
    private WormIdleComponent _idle;

    private float _time;
    
    private float _baseY;

    public WormIdleState(AbstractEntity entity) : base(entity)
    {
        _input = entity.GetControllerSystem<IInputProvider>();
        _idle = entity.GetControllerComponent<WormIdleComponent>();
    }

    public override void Enter()
    {
        _baseY = entity.transform.position.y;
        _time = _idle.phaseOffset;
    }

    public override void Exit() { }

    public override void Update()
    {
        _time += Time.deltaTime;

        float sin = Mathf.Sin(_time * _idle.frequency) * _idle.amplitude;

        float noise = (Mathf.PerlinNoise(_time * _idle.noiseSpeed, 0.1f) - 0.5f)
                      * _idle.noiseStrength;

        float targetY = _baseY + sin;

        Vector2 current = entity.transform.position;

        // вместо "скорости вверх/вниз" → ошибка позиции
        float errorY = targetY - current.y;

        Vector2 dir = new Vector2(noise, errorY).normalized;

        _input.GetState()
            .Move
            .Update(true, dir);
    }
}