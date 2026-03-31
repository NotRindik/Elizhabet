using System;
using Controllers;
using States;
using Systems;
using UnityEngine;
using Random = UnityEngine.Random;

public class PushBotAI : BaseAI
{
    private FSMSystem _fsmSystem;
    private FlyingMoveComponent flyingMove;
    
    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        SetState(new InputState());
        _fsmSystem = owner.GetControllerSystem<FSMSystem>();
        flyingMove = owner.GetControllerComponent<FlyingMoveComponent>();
        var patrolC = owner.GetControllerComponent<PatrolComponent>();

        if(patrolC != null) 
            _fsmSystem.AddAnyTransition(new FlyingPatrolState(owner),() => patrolC.points?.Length > 0);

        _fsmSystem.AddAnyTransition(new ChaoticFlyState(owner), () => true);

        GetState().Move.performed += c => flyingMove.targetVelocity = c.ReadValue<Vector2>();
    }
}

public class ChaoticFlyState : BasicState
{
    private IInputProvider _provider;

    private float _timer;
    private float _switchTime;

    private float _directionY = 1f;
    private float _directionX = 0f;

    public ChaoticFlyState(AbstractEntity entity) : base(entity)
    {
        _provider = entity.GetControllerSystem<IInputProvider>();
    }

    public override void Enter()
    {
        _timer = 0f;
        _switchTime = Random.Range(0.5f, 2f);

        _directionY = Random.value > 0.5f ? 1f : -1f;
        _directionX = Random.Range(-1f, 1f);
    }

    public override void Exit() { }

    public override void Update()
    {
        _timer += Time.deltaTime;

        if (_timer >= _switchTime)
        {
            _timer = 0f;

            // строго меняем только Y (вверх/вниз)
            _directionY *= -1f;

            // X выбираем заново, но он стабильный до следующего переключения
            _directionX = Random.Range(-1f, 1f);

            _switchTime = Random.Range(0.5f, 2f);
        }

        _provider.GetState().Move.Update(true, new Vector2(_directionX, _directionY));
    }
}

[System.Serializable]
public class PatrolComponent : IComponent
{
    public Transform[] points;       // массив точек для патруля
    public int currentIndex = 0;     // текущая цель
    public float stopDistance = 0.2f; // расстояние до точки, чтобы считать достигнутой
    public float waitTime = 1f;      // время ожидания на точке
    [NonSerialized] public float waitTimer = 0f; // внутренний таймер ожидания
}

public class FlyingPatrolState : BasicState
{
    private PatrolComponent patrol;
    private FlyingMoveComponent flyingMove;
    private IInputProvider inputProvider;

    public FlyingPatrolState(AbstractEntity entity) : base(entity)
    {
        patrol = entity.GetControllerComponent<PatrolComponent>();
        flyingMove = entity.GetControllerComponent<FlyingMoveComponent>();
        inputProvider = entity.GetControllerSystem<IInputProvider>();
    }

    public override void Enter()
    {
        patrol.waitTimer = 0f;
        patrol.currentIndex = 0;
    }

    public override void Exit() { }

    public override void Update()
    {
        if (patrol.points == null || patrol.points.Length == 0)
            return;

        Vector2 currentPos = entity.transform.position;
        Vector2 targetPos = patrol.points[patrol.currentIndex].position;

        Vector2 toTarget = targetPos - currentPos;
        float distance = toTarget.magnitude;

        if (distance < patrol.stopDistance)
        {
            patrol.waitTimer += Time.deltaTime;
            inputProvider.GetState().Move.Update(true, Vector2.zero); // стоим на месте

            if (patrol.waitTimer >= patrol.waitTime)
            {
                patrol.waitTimer = 0f;
                patrol.currentIndex = (patrol.currentIndex + 1) % patrol.points.Length;
            }
        }
        else
        {
            Vector2 dir = toTarget.normalized;
            inputProvider.GetState().Move.Update(true, dir); // двигаемся к следующей точке
        }
    }
}

public class FlyingMoveSystem : BaseSystem,IDisposable
{
    public FlyingMoveComponent FlyingMoveComponent;
    public ControllersBaseFields _BaseFields;
    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        FlyingMoveComponent = owner.GetControllerComponent<FlyingMoveComponent>();
        _BaseFields = owner.GetControllerComponent<ControllersBaseFields>();
        owner.OnUpdate += Update;
    }
    public override void OnUpdate()
    {
        var move = FlyingMoveComponent;
        var rb = _BaseFields.rb;

        Vector2 velocity = rb.linearVelocity;
        
        Vector2 delta = move.targetVelocity - velocity;
        
        velocity += delta * move.acceleration * Time.deltaTime;
        
        velocity -= velocity * move.damping * Time.deltaTime;
        
        velocity = Vector2.ClampMagnitude(velocity, move.maxSpeed);

        rb.linearVelocity = velocity;
    }
    public void Dispose()
    {
        owner.OnUpdate -= Update;
    }
}

[System.Serializable]
public class FlyingMoveComponent : IComponent
{
    public Vector2 targetVelocity;
    
    public float acceleration;
    public float damping;
    public float maxSpeed;
}