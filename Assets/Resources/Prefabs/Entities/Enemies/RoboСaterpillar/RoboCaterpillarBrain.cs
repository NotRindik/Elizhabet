using System;
using System.Collections.Generic;
using System.Threading;
using AYellowpaper.SerializedCollections;
using Cysharp.Threading.Tasks;
using Systems;
using UnityEngine;
using Random = UnityEngine.Random;

public class RoboCaterpillarBrain : BaseAI, IDisposable
{
    private FSMSystem _fsmSystem;
    private FsmComponent _fsmComponent;
    private SimpleMoveComponent _moveComponent;
    private VisionComponent visionComponent;

    private BaseAttackComponent attackComponent;
    private CaterpillarIdle idle;
    private CaterpillarChase chaseState;
    private CaterpillarHit caterpillarHit;
    private AudioDataComponent audioDataComponent;

    private HealthComponent hpC;

    private AudioSource engineSource;

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        SetState(new InputState());

        _fsmSystem = owner.GetControllerSystem<FSMSystem>();
        _fsmComponent = owner.GetControllerComponent<FsmComponent>();
        _moveComponent = owner.GetControllerComponent<SimpleMoveComponent>();
        visionComponent = owner.GetControllerComponent<VisionComponent>();
        attackComponent = owner.GetControllerComponent<BaseAttackComponent>();
        audioDataComponent = owner.GetControllerComponent<AudioDataComponent>();
        hpC = owner.GetControllerComponent<HealthComponent>();
        idle = new CaterpillarIdle(owner);
        chaseState = new CaterpillarChase(owner);
        caterpillarHit = new CaterpillarHit(owner);

        _fsmSystem.AddTransition(idle, chaseState, () => visionComponent.currentTarget && _fsmComponent.state != caterpillarHit);
        attackComponent.OnHitAnything.AddListener(OnHit);
        _fsmSystem.AddTransition(chaseState, idle, () =>
            {
                bool condition = visionComponent.currentTarget == null && _fsmComponent.state != caterpillarHit;
                if(condition)
                    chaseState.playAlertAnimation = true;
                return condition;
            }
        );
        
        _fsmSystem.AddTransition(caterpillarHit, idle, () => !caterpillarHit.isActive);

        _fsmSystem.SetState(idle);

        owner.OnUpdate += Update;
        hpC.OnTakeHit += OnHitStun;
        engineSource = audioDataComponent.Sources["engine"];
        engineSource.Play();
    }
    public override void OnUpdate()
    {
        if(engineSource)
            engineSource.pitch = Mathf.Lerp(1f,1.2f,_moveComponent.direction.x != 0 ? Mathf.Clamp01(_moveComponent.speedMultiplier) : 0);
    }
    public void OnHit(HitInfo _)
    {
        _fsmSystem.SetState(caterpillarHit);
    }
    public void OnHitStun(HitInfo _)
    {
        caterpillarHit.isStun = true;
        _fsmSystem.SetState(caterpillarHit);
    }

    public void Dispose()
    {
        engineSource.Stop();
        attackComponent.OnHitAnything.RemoveListener(OnHit);
        _fsmComponent.state.Exit();
        hpC.OnTakeHit -= OnHitStun;
        owner.OnUpdate -= Update;
    }
}

public class CaterpillarHit : BaseState
{
    private SimpleMoveComponent _moveComponent;
    private AnimationComponent _animationComponent;

    private const float MinHit = 1f;
    private const float MaxHit = 2f;

    public const float stunWait = 1;

    public bool isStun;

    public bool isActive;

    private CancellationTokenSource cts;

    public CaterpillarHit(AbstractEntity owner) : base(owner)
    {
        _moveComponent = owner.GetControllerComponent<SimpleMoveComponent>();
        _animationComponent = owner.GetControllerComponent<AnimationComponent>();
    }

    public override void Enter()
    {
        cts = new CancellationTokenSource();
        isActive = true;
        _moveComponent.direction.x = 0;
        _moveComponent.speedMultiplier = 0;

        _animationComponent.Play("Idle");

        HitUpdate(cts.Token).Forget();
    }

    public override void Exit()
    {
        cts?.Cancel();
        cts?.Dispose();
        _moveComponent.direction.x = 0;
        _moveComponent.speedMultiplier = 0;
        isStun = false;
    }

    private async UniTaskVoid HitUpdate(CancellationToken token)
    {
        if (!isStun)
        {
            await UniTask.WaitForSeconds(
                Random.Range(MinHit, MaxHit),
                cancellationToken: token
            );
        }
        else
        {
            await UniTask.WaitForSeconds(stunWait, cancellationToken: token);
        }
        isActive = false;
    }
}
public class CaterpillarIdle : BaseState
{
    private SimpleMoveComponent _moveComponent;
    private AnimationComponent _animationComponent;
    private SpriteFlipSystem spriteFlipSystem;
    private SensorComponent sensorComponent;

    private const float MinStand = 1f, MaxStand = 3f;
    private const float MinMove = 1f, MaxMove = 2.5f;

    private CancellationTokenSource cts;

    public CaterpillarIdle(AbstractEntity owner) : base(owner)
    {
        _moveComponent = owner.GetControllerComponent<SimpleMoveComponent>();
        _animationComponent = owner.GetControllerComponent<AnimationComponent>();
        spriteFlipSystem = owner.GetControllerSystem<SpriteFlipSystem>();
        sensorComponent = owner.GetControllerComponent<SensorComponent>();
    }

    public override void Enter()
    {
        cts = new CancellationTokenSource();
        IdleUpdate(cts.Token).Forget();
    }

    public override void Exit()
    {
        cts?.Cancel();
        cts?.Dispose();
        _moveComponent.direction.x = 0;
        _moveComponent.speedMultiplier = 0;
    }

    private async UniTaskVoid IdleUpdate(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            int moveDir = Random.Range(-1, 2);

            if (moveDir != 0)
            {
                _animationComponent.Play("Walk");
                _moveComponent.direction.x = moveDir;
                _moveComponent.speedMultiplier = 1;
                spriteFlipSystem.SetFacing(moveDir);

                float moveTime = Random.Range(MinMove, MaxMove);
                float elapsed = 0f;

                while (elapsed < moveTime)
                {
                    if (sensorComponent.HasWallFace)
                    {
                        moveDir *= -1;

                        _moveComponent.direction.x = moveDir;
                        spriteFlipSystem.SetFacing(moveDir);

                        break;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    elapsed += Time.deltaTime;
                }

                _moveComponent.speedMultiplier = 0;
            }

            _animationComponent.Play("Idle");

            await UniTask.WaitForSeconds(
                Random.Range(MinStand, MaxStand),
                cancellationToken: token);
        }
    }
}

[Serializable]
public class SensorComponent : IComponent
{
    public bool HasWallBack;
    public bool HasWallFace;
    
    [SerializeReference,SubclassSelector]public Sensor[] Sensors;

    private Dictionary<Type, Sensor> cash = new(5);

    public T GetSensor<T>() where T : Sensor
    {
        if (cash.Count == 0)
        {
            cash = new();
            foreach (var sensor in Sensors)
            {
                cash.Add(sensor.GetType(), sensor);
            }   
        }
        
        return (T)cash[typeof(T)];
    }
}

public class SensorSystem : BaseSystem, IDisposable
{
    private SensorComponent sensorComponent;

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        sensorComponent = owner.GetControllerComponent<SensorComponent>();
        foreach (var sensor in sensorComponent.Sensors)
        {
            sensor.Init(owner,sensorComponent);
        }

        owner.OnUpdate += Update;
    }

    public override void OnUpdate()
    {
        foreach (var sensor in sensorComponent.Sensors)
        {
            sensor.Update();
        }
    }
    public void Dispose()
    {
        owner.OnUpdate -= Update;
    }
}

[Serializable]
public class AudioDataComponent : IComponent
{
    public SerializedDictionary<string, AudioSource> Sources = new();
    public SerializedDictionary<string, EventSound> EventSounds = new();
    public SerializedDictionary<string, AudioClip> SimpleClips = new();
}

[Serializable]
public abstract class Sensor
{
    protected AbstractEntity owner;
    protected SensorComponent data;
    
    public virtual void Init(AbstractEntity entity, SensorComponent sensorComponent)
    {
        owner = entity;
        data = sensorComponent;
    }

    public abstract void Update();
}

[Serializable]
public class FaceWallSensor : Sensor
{
    public Transform origin;
    public float dist;
    private RaycastHit2D[] hit = new RaycastHit2D[3];
    private int hitCount;
    public LayerMask mask;
    private ContactFilter2D filter;

    public override void Init(AbstractEntity entity, SensorComponent sensorComponent)
    {
        base.Init(entity, sensorComponent);
        filter = new ContactFilter2D
        {
            layerMask = mask,
            useLayerMask = true
        };
    }

    public override void Update()
    {
        hitCount = Physics2D.Raycast(origin.position, owner.transform.right,filter, hit,dist);
        
        if (hitCount > 0)
        {
            data.HasWallFace = true;
        }
        else
        {
            data.HasWallFace = false;
        }
    }
}

public class CaterpillarChase : BaseState
{
    private SimpleMoveComponent _moveComponent;
    private AnimationComponent _animationComponent;
    private SpriteFlipSystem spriteFlipSystem;
    private VisionComponent visionComponent;

    private CancellationTokenSource cts;
    
    public bool playAlertAnimation = true;

    public int saveDir;
    
    private AudioDataComponent audioDataComponent;

    private AudioSource alertSound;

    public CaterpillarChase(AbstractEntity owner) : base(owner)
    {
        _moveComponent = owner.GetControllerComponent<SimpleMoveComponent>();
        _animationComponent = owner.GetControllerComponent<AnimationComponent>();
        visionComponent = owner.GetControllerComponent<VisionComponent>();
        audioDataComponent = owner.GetControllerComponent<AudioDataComponent>();
        spriteFlipSystem = owner.GetControllerSystem<SpriteFlipSystem>();
        
        alertSound = audioDataComponent.Sources["alert"];
    }

    public override void Enter()
    {
        _moveComponent.speedMultiplier = 1;
        cts = new CancellationTokenSource();
        saveDir = 0;
        ChaseUpdate(cts.Token).Forget();
    }

    public override void Exit()
    {
        cts?.Cancel();
        cts?.Dispose();
        _moveComponent.direction.x = 0;
        _moveComponent.speedMultiplier = 0;
    }
    private bool AnimationFinished() => _animationComponent.GetProgressRaw() >= 1;
    private async UniTaskVoid ChaseUpdate(CancellationToken token)
    {
        if (playAlertAnimation)
        {
            _animationComponent.Play("Alert");
            alertSound.Play();
            playAlertAnimation = false;
            
            await UniTask.NextFrame();
            await UniTask.WaitUntil(AnimationFinished,cancellationToken: token);
        }
        
        while (!token.IsCancellationRequested)
        {
            var target = visionComponent.currentTarget;

            if (target != null)
            {
                int dir = target.position.x > owner.transform.position.x ? 1 : -1;

                if (saveDir == 0)
                {
                    _animationComponent.Play("Walk");
                    _moveComponent.direction.x = dir;
                    spriteFlipSystem.SetFacing(dir);
                    saveDir = dir;
                }
                
                if (dir != saveDir)
                {
                    _animationComponent.Play("Idle");
                    _moveComponent.direction.x = 0;
                    saveDir = dir;
                    
                    await UniTask.WaitForSeconds(1,cancellationToken:token);
                }
                
                _animationComponent.Play("Walk");
                
                _moveComponent.direction.x = dir;
                spriteFlipSystem.SetFacing(dir);
            }

            await UniTask.NextFrame(token);
        }
    }
}