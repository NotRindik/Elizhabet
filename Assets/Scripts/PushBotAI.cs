using System;
using Controllers;
using States;
using Systems;
using UnityEngine;
using UnityEngine.Serialization;
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

        GetState().Move.performed += c => flyingMove.MoveDir = c.ReadValue<Vector2>();
    }
}

[System.Serializable]
public class TargetSearchComponent : IComponent
{
    public LayerMask targetLayer,blockLayer;
    public float searchRadius = 5f;
    public Transform currentTarget;
    
    [NonSerialized] public Collider2D[] hitsBuffer = new Collider2D[10]; // заранее выделяем память
}
public class TargetSearchSystem : BaseSystem, IDisposable
{
    private TargetSearchComponent targetSearch;
    private ControllersBaseFields baseFields;

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        targetSearch = owner.GetControllerComponent<TargetSearchComponent>();
        baseFields = owner.GetControllerComponent<ControllersBaseFields>();
        owner.OnUpdate += Update;
    }

    public override void OnUpdate()
    {
        if (targetSearch == null || baseFields == null) return;

        Vector2 position = baseFields.rb.position;

        int hitCount = Physics2D.OverlapCircleNonAlloc(
            position,
            targetSearch.searchRadius,
            targetSearch.hitsBuffer,
            targetSearch.targetLayer
        );

        Transform closest = null;
        float minDist = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            var hit = targetSearch.hitsBuffer[i];

            if (hit.transform == baseFields.rb.transform) continue;

            Vector2 toTarget = (Vector2)(hit.transform.position - (Vector3)baseFields.rb.position);
            float dist = toTarget.magnitude;


            RaycastHit2D rayHit = Physics2D.Raycast(
                position,
                toTarget.normalized,    
                dist,
                targetSearch.blockLayer
            );

            if (rayHit.collider != null)
                continue;
            
            if (dist < minDist)
            {
                minDist = dist;
                closest = hit.transform;
            }
        }

        targetSearch.currentTarget = closest;
    }

    public void Dispose()
    {
        owner.OnUpdate -= Update;
    }
}


public class ChaseState : BasicState
{
    private IInputProvider _provider;

    private TargetSearchComponent targetSearchComponent;

    public ChaseState(AbstractEntity entity) : base(entity)
    {
        _provider = entity.GetControllerSystem<IInputProvider>();
        targetSearchComponent = entity.GetControllerSystem<TargetSearchComponent>();
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


public enum PatrolType
{
    Loop,           // после последней точки возвращаемся к первой (круг)
    PingPong        // идём до конца, потом обратно к началу (туда-сюда)
}

[System.Serializable]
public class PatrolComponent : IComponent
{
    public PatrolType patrolType;
    public Transform[] points;
    public int currentIndex = 0;
    public float stopDistance = 0.2f;
    public float waitTime = 1f;
    [NonSerialized] public float waitTimer = 0f;
    
    [NonSerialized] public int direction = 1;
    
    public void NextIndex()
    {
        if (points == null || points.Length == 0) return;

        if (patrolType == PatrolType.Loop)
        {
            currentIndex = (currentIndex + 1) % points.Length;
        }
        else if (patrolType == PatrolType.PingPong)
        {
            currentIndex += direction;

            if (currentIndex >= points.Length)
            {
                currentIndex = points.Length - 2;
                direction = -1;
            }
            else if (currentIndex < 0)
            {
                currentIndex = 1;
                direction = 1;
            }
        }
    }
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
                patrol.NextIndex();
            }
        }
        else
        {
            Vector2 dir = toTarget.normalized;
            inputProvider.GetState().Move.Update(true, dir); // двигаемся к следующей точке
        }
    }
}

public class FlyingMoveSystem : BaseSystem, IDisposable
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
        
        Vector2 impulse = (move.MoveDir * move.speed);
        

        rb.AddForce(impulse, ForceMode2D.Force);
        
        if (rb.linearVelocity.magnitude > move.maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * move.maxSpeed;
        }
        
        Vector2 velocity = rb.linearVelocity;
        
        Vector2 desiredDir = move.MoveDir.normalized;
        
        Vector2 forwardVel = Vector2.Dot(velocity, desiredDir) * desiredDir;
        
        Vector2 lateralVel = velocity - forwardVel;
        
        rb.linearVelocity = forwardVel + lateralVel * Mathf.Clamp01(1f - move.damping * Time.deltaTime);
    }

    public void Dispose()
    {
        owner.OnUpdate -= Update;
    }
}

[System.Serializable]
public class FlyingMoveComponent : IComponent
{
    public Vector2 MoveDir;
    
    public float maxSpeed = 5f;
    public float speed = 5f;
    
    public float damping = 1f; 
}