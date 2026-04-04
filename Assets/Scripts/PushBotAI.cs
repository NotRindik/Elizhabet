using System;
using Controllers;
using DG.Tweening;
using States;
using Systems;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class PushBotAI : BaseAI
{
    private FSMSystem _fsmSystem;
    private ControllersBaseFields _BaseFields;
    private FlyingMoveComponent flyingMove;
    private TargetSearchComponent _tSC;
    private BaseAttackComponent _attackComponent;
    private AnimationComponent animationComponent;

    private Tween _tween;
    
    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        SetState(new InputState());
        _fsmSystem = owner.GetControllerSystem<FSMSystem>();
        flyingMove = owner.GetControllerComponent<FlyingMoveComponent>();
        _BaseFields = owner.GetControllerComponent<ControllersBaseFields>();
        var patrolC = owner.GetControllerComponent<PatrolComponent>();
        _tSC = owner.GetControllerComponent<TargetSearchComponent>();
        _attackComponent = owner.GetControllerComponent<BaseAttackComponent>();
        animationComponent = owner.GetControllerComponent<AnimationComponent>();

        if(patrolC != null) 
            _fsmSystem.AddAnyTransition(new FlyingPatrolState(owner),() => patrolC.points?.Length > 0);

        _fsmSystem.AddAnyTransition(new ChaoticFlyState(owner), () => _tSC.currentTarget == null);
        _fsmSystem.AddAnyTransition(new ChaseState(owner), () => _tSC.currentTarget != null);

        GetState().Move.performed += c => flyingMove.MoveDir = c.ReadValue<Vector2>();

        _attackComponent.OnHitAnything.AddListener(OnHit);
    }

    public void OnHit(HitInfo info)
    {
        var rb = _BaseFields.rb;

        Vector2 hitPos = info.GetHitPos();
        Vector2 selfPos = rb.position;
    
        Vector2 dir = (selfPos - hitPos).normalized;

        if (dir.sqrMagnitude < 0.001f)
            dir = Random.insideUnitCircle.normalized;
        float force = 4f;
        if (info.Target == null)
        {
            force = 2f;
            rb.AddForce(dir * force, ForceMode2D.Impulse);
            return;
        }
        rb.AddForce(dir * force, ForceMode2D.Impulse);
        
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0, 0, angle);
        _tween.Kill();
        _tween = transform.DORotate(Vector3.zero,1f);
        animationComponent.Play("Attack");
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
    private TargetSearchComponent targetSearch;
    private Transform _self;

    private float _attackDistance = 1.5f;

    public ChaseState(AbstractEntity entity) : base(entity)
    {
        _provider = entity.GetControllerSystem<IInputProvider>();
        targetSearch = entity.GetControllerComponent<TargetSearchComponent>();
        _self = entity.transform;
    }

    public override void Enter() { }

    public override void Exit() { }

    public override void Update()
    {
        var target = targetSearch.currentTarget;

        if (target == null)
        {
            _provider.GetState().Move.Update(true, Vector2.zero);
            return;
        }

        Vector2 toTarget = target.position - _self.position;
        float distance = toTarget.magnitude;
        
        Vector2 dir = toTarget.normalized;
        _provider.GetState().Move.Update(true, dir);
        
        if (distance <= _attackDistance)
        {
            _provider.GetState().Attack.Update(true,true);
        }
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