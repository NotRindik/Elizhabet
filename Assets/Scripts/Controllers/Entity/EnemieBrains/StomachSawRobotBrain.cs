using System;
using System.Threading;
using Controllers;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
using std;
using std.UniTaskExtensions;
using Systems;
using UnityEngine;
using Random = UnityEngine.Random;
using UniTaskExtensions = std.UniTaskExtensions.UniTaskExtensions;

public class StomachSawRobotBrain : BaseAI,IDisposable
{
    private FSMSystem _fsmSystem;
    private FsmComponent _fsmComponent;
    private StomachSawRobotComponent _robotComponent;
    private BaseAttackComponent _attackComponent;
    
    private GroundingComponent _groundingComponent;
    private SimpleMoveComponent _moveComponent;

    private ContactFilter2D filter;


    private WanderingIdle idle;
    private HitState hitState;
    private StomachSawRobotChase chaseState;
    
    private EventSoundInstance roboHitSound,enemyHitSound;

    private VisionComponent visionComponent;


    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        SetState(new InputState());

        _fsmSystem = owner.GetControllerSystem<FSMSystem>();
        
        _robotComponent = owner.GetControllerComponent<StomachSawRobotComponent>();
        _fsmComponent = owner.GetControllerComponent<FsmComponent>();
        _attackComponent = owner.GetControllerComponent<BaseAttackComponent>();
        _groundingComponent = owner.GetControllerComponent<GroundingComponent>();
        _moveComponent = owner.GetControllerComponent<SimpleMoveComponent>();
        visionComponent = owner.GetControllerComponent<VisionComponent>();
        
        roboHitSound = new EventSoundInstance(_robotComponent.bladeHitEvent);
        enemyHitSound = new EventSoundInstance(_robotComponent.hitSound);
        
        roboHitSound.SetData(new MaterialData()
        {
            material = owner.GetComponent<AudioMaterialSetter>().AudioMaterial,
            interaction = "hit"
        });
        
        idle = new WanderingIdle(owner);
        hitState = new HitState(owner);
        chaseState = new StomachSawRobotChase(owner);
        
        _fsmSystem.AddAnyTransition(idle, () => _robotComponent.lastHit == default && _fsmComponent.state != chaseState);
        
        _fsmSystem.AddTransition(idle,chaseState, () => visionComponent.currentTarget);
        
        _fsmSystem.AddTransition(chaseState,idle, () =>
            {
                if(visionComponent.currentTarget == null)
                    _robotComponent.playAlertAnim = true;
                return visionComponent.currentTarget == null;
            }
        );

        _groundingComponent.OnGround += OnGround;
        _groundingComponent.OnUnGround += OnUnGround;
        
        _fsmSystem.SetState(idle);

        owner.OnUpdate += Update;
        
        filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = _robotComponent.hitLayer
        };


        _robotComponent.sawRotation = _robotComponent.sawTransform
            .DOLocalRotate(new Vector3(0, 0, 360), _robotComponent.SawRotPerSec, RotateMode.LocalAxisAdd)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart);
    }

    public void OnGround()
    {
        _robotComponent.groundSparkles.Play();
        _robotComponent.sparklesLoop.Play();
    }
    
    public void OnUnGround()
    {
        _robotComponent.groundSparkles.Stop();
        _robotComponent.sparklesLoop.Stop();
    }

    private Vector2 minMaxPitch = new Vector2(0.8f, 1.2f);

    public override void OnUpdate()
    {
        
        float speed = _moveComponent.speedMultiplier;

        float pitch = Mathf.Lerp(minMaxPitch.x, minMaxPitch.y, speed);

        float pulseProgress = 1f - Mathf.InverseLerp(0f, 0.3f, speed);

        float pulse = Mathf.Sin(Time.time * 5) * 0.1f * pulseProgress;

        _robotComponent.sparklesLoop.pitch = pitch + pulse;
        
        int hitCount = Physics2D.Linecast(_robotComponent.firstPos.position, _robotComponent.secondPos.position, filter, _robotComponent.hitBuffer);
        
        for (int i = 0; i < hitCount; i++)
        {
            var hit = _robotComponent.hitBuffer[i];

            if (hit.collider == null)
                continue;

            if (hit.collider.transform == owner.transform ||
                hit.collider.transform.IsChildOf(owner.transform))
                continue;
            
            if (_robotComponent.lastHit.collider == hit.collider)
                continue;

            _robotComponent.lastHit = hit;
#if UNITY_EDITOR
            _robotComponent.LastHit = hit.collider.gameObject;
#endif
            
            if (_fsmComponent.state != hitState)
            {
                _fsmSystem.SetState(hitState);
            }
            else
            {
                hitState.Exit();
                hitState.Enter();
            }
            
            _robotComponent.hitPs.transform.position = hit.point;
            _robotComponent.hitPs.Emit(10);

            AudioManager.instance.PlayEvent(roboHitSound);
            
            if (!_attackComponent.attackLayer.Contains(hit.collider.gameObject.layer))
                break;

            if (hit.collider.TryGetComponent<AbstractEntity>(out var entity))
            {
                var material = entity.GetComponent<AudioMaterialSetter>()?.AudioMaterial;
                if (material != null)
                {
                    enemyHitSound.SetData(
                        new MaterialData
                        {
                            material = material,
                            interaction = "hit"
                        }
                    );
                    AudioManager.instance.PlayEvent(enemyHitSound);
                }
                
                var hp = entity.GetControllerSystem<HealthSystem>();
                var bs = entity.GetControllerComponent<ControllersBaseFields>();
                
                bs.rb.linearVelocity = Vector2.zero;
                Vector2 knockDir = ((Vector2)entity.transform.position - hit.point).normalized;
                knockDir.Normalize();
                    
                Debug.Log(knockDir);
                bs.rb.AddForce(new Vector2(knockDir.x * _attackComponent.knockBackForce,knockDir.y * _attackComponent.knockBackForceVertical), ForceMode2D.Impulse);
                
                var info = new HitInfo
                {
                    Attacker = owner,
                    Target = entity,
                    hitPosition = hit.point,
                };

                new EnemyDamage(_attackComponent.damage).ApplyDamage(hp, ref info);
            }

            break;
        }
    }

    public void Dispose()
    {
        _robotComponent.sawRotation?.Kill();
        owner.OnUpdate -= Update;
        _groundingComponent.OnGround -= OnGround;
        _groundingComponent.OnUnGround -= OnUnGround;
        _fsmComponent.state.Exit();
    }
}

[System.Serializable]
public class StomachSawRobotComponent : IComponent
{
    public Transform firstPos, secondPos, RotationTransform, sawTransform;
    public RaycastHit2D lastHit;
    
    public RaycastHit2D[] hitBuffer = new RaycastHit2D[10];

    public float SawRotPerSec;

    public ParticleSystem hitPs,groundSparkles;
    public EventSound hitSound,bladeHitEvent;

    public AudioSource alertSound,sparklesLoop;

    public LayerMask hitLayer;
    public LayerMask obstacleLayer;

#if UNITY_EDITOR
    [ReadOnly] public GameObject LastHit;
#endif

    public Tween sawRotation;

    public bool playAlertAnim = true;
}

public class WanderingIdle : BaseState
{
    private SimpleMoveComponent  _moveComponent;
    private AnimationComponent  _animationComponent;
    private SpriteFlipSystem spriteFlipSystem;
    private StomachSawRobotComponent roboC;

    private float thinkTime = 2;
    
    private float moveTime = 2;
    
    private CancellationTokenSource cts;
    
    private Tween rotationTween;

    
    public WanderingIdle(AbstractEntity owner) : base(owner)
    {
        _moveComponent = owner.GetControllerComponent<SimpleMoveComponent>();
        _animationComponent = owner.GetControllerComponent<AnimationComponent>();
        roboC = owner.GetControllerComponent<StomachSawRobotComponent>();
        spriteFlipSystem = owner.GetControllerSystem<SpriteFlipSystem>();
    }
    public override void Enter()
    {
        _animationComponent.Play("Idle");
        
        cts = new CancellationTokenSource();
        
        IdleUpdate(cts.Token).Forget();
    }
    public override void Exit()
    {
        cts?.Cancel();
        cts?.Dispose();
        rotationTween?.Kill();
        rotationTween = null;
    }

    private async UniTaskVoid IdleUpdate(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await UniTask.WaitForSeconds(thinkTime, cancellationToken: token);

            int moveDir = Random.Range(-1, 2);

            _moveComponent.direction.x = moveDir;
            spriteFlipSystem.SetFacing(moveDir);

            rotationTween?.Kill();

            var transform = roboC.RotationTransform;

            Vector3 target = transform.localEulerAngles;
            target.z = -15f;

            rotationTween = transform
                .DOLocalRotate(target, moveTime)
                .SetEase(Ease.OutSine);
            
            if(moveDir != 0)
                await UniTaskExtensions.WaitWithProgress(moveTime, token, Acceleration);
            else
            {
                _moveComponent.speedMultiplier = 0;
                await UniTask.WaitForSeconds(moveTime, cancellationToken: token);
            }

            target.z = 15f;

            rotationTween = transform
                .DOLocalRotate(target, moveTime)
                .SetEase(Ease.OutSine);
            
            if(moveDir != 0)
                await UniTaskExtensions.WaitWithProgress(moveTime, token, Deceleration);
            else
                await UniTask.WaitForSeconds(moveTime, cancellationToken: token);
            
            rotationTween?.Kill();
            target.z = 0;

            rotationTween = transform
                .DOLocalRotate(target, moveTime)
                .SetEase(Ease.OutBounce);
        }
    }

    public void Acceleration(in ProgressTimer timer)
    {
        _moveComponent.speedMultiplier = timer.Normalized;
        
    }
    
    public void Deceleration(in ProgressTimer timer)
    {
        _moveComponent.speedMultiplier = Mathf.Lerp(1,0,timer.Normalized);
        int dir = (int)_moveComponent.direction.x;

        spriteFlipSystem.SetFacing(timer.Normalized >= 0.3f ? -dir : dir);
    }
}

public class HitState : BaseState
{
    private SimpleMoveComponent  _moveComponent;
    private AnimationComponent  _animationComponent;
    private SpriteFlipSystem spriteFlipSystem;
    private StomachSawRobotComponent _roboC;
    
    private float moveTime = 2;
    
    private CancellationTokenSource cts;
    private Tween rotationTween;
    
    public HitState(AbstractEntity owner) : base(owner)
    {
        _moveComponent = owner.GetControllerComponent<SimpleMoveComponent>();
        _animationComponent = owner.GetControllerComponent<AnimationComponent>();
        _roboC = owner.GetControllerComponent<StomachSawRobotComponent>();
        spriteFlipSystem = owner.GetControllerSystem<SpriteFlipSystem>();
    }
    public override void Enter()
    {
        _animationComponent.Play("Idle");
        
        cts = new CancellationTokenSource();
        
        IdleUpdate(cts.Token).Forget();
    }
    public override void Exit()
    {
        cts?.Cancel();
        cts?.Dispose();

        rotationTween?.Kill();
        rotationTween = null;

        _roboC.lastHit = default;
    }

    private async UniTaskVoid IdleUpdate(CancellationToken token)
    {
        Vector2 delta = _roboC.lastHit.point - (Vector2)owner.transform.position;

        int moveDir = delta.x > 0 ? -1 : 1;

        _moveComponent.direction.x = moveDir;

        rotationTween?.Kill();

        var transform = _roboC.RotationTransform;

        Vector3 target = transform.localEulerAngles;
        target.z = 15f;
        
        rotationTween = transform.DOLocalRotate(target, moveTime).SetEase(Ease.OutSine);
        
        await UniTaskExtensions.WaitWithProgress(moveTime, token, Deceleration);

        _roboC.lastHit = default;
        target.z = 0;
        
        rotationTween = transform.DOLocalRotate(target, moveTime).SetEase(Ease.OutBounce);
    }

    public void Deceleration(in ProgressTimer timer)
    {
        _moveComponent.speedMultiplier = Mathf.Lerp(1.5f,0,timer.Normalized);

        _roboC.sawRotation.timeScale = timer.Normalized;
        
        int dir = (int)_moveComponent.direction.x;

        spriteFlipSystem.SetFacing(timer.Normalized >= 0.3f ? -dir : dir);
    }
}

public class StomachSawRobotChase : BaseState
{
    private SimpleMoveComponent _moveComponent;
    private AnimationComponent _animationComponent;
    private SpriteFlipSystem spriteFlipSystem;
    private StomachSawRobotComponent roboC;
    private VisionComponent visionComponent;
    
    private ControllersBaseFields controllersBase;

    private CancellationTokenSource cts;
    private Tween rotationTween;

    private int currentDir;

    private const float CheckInterval = 0.2f;
    private const float MoveTime = 1.5f;
    private const float DecelerateTime = 1.5f/2;

    public StomachSawRobotChase(AbstractEntity owner) : base(owner)
    {
        _moveComponent = owner.GetControllerComponent<SimpleMoveComponent>();
        _animationComponent = owner.GetControllerComponent<AnimationComponent>();
        roboC = owner.GetControllerComponent<StomachSawRobotComponent>();
        controllersBase = owner.GetControllerComponent<ControllersBaseFields>();
        visionComponent = owner.GetControllerComponent<VisionComponent>();
        spriteFlipSystem = owner.GetControllerSystem<SpriteFlipSystem>();
    }

    public override void Enter()
    {
        currentDir = 0;
        cts = new CancellationTokenSource();

        ChaseUpdate(cts.Token).Forget();
    }

    public override void Exit()
    {
        cts?.Cancel();
        cts?.Dispose();
        rotationTween?.Kill();
        rotationTween = null;
    }

    private bool AnimationFinished() => _animationComponent.GetProgressRaw() >= 1;

    private async UniTaskVoid ChaseUpdate(CancellationToken token)
    {
        if (roboC.playAlertAnim)
        {
            controllersBase.rb.linearVelocityY = 5;
            _animationComponent.Play("Alert");
            roboC.alertSound.Play();
            roboC.playAlertAnim = false;
            
            await UniTask.NextFrame(token);
            await UniTask.WaitUntil(AnimationFinished, cancellationToken: token);
        }
        
        _animationComponent.Play("Idle");

        while (!token.IsCancellationRequested)
        {
            var player = visionComponent.currentTarget;
            
            if(player == null)
                continue;
            
            float diffX = player.position.x - owner.transform.position.x;
            int desiredDir = diffX > 0 ? 1 : -1;

            if (desiredDir != currentDir)
            {
                if (currentDir != 0)
                {
                    await Decelerate(token);
                }
                

                await Accelerate(desiredDir, token);
            }
                
            await UniTask.WaitForSeconds(CheckInterval, cancellationToken: token);
        }
    }

    private async UniTask Accelerate(int dir, CancellationToken token)
    {
        currentDir = dir;

        _moveComponent.direction.x = dir;
        spriteFlipSystem.SetFacing(dir);

        rotationTween?.Kill();

        var transform = roboC.RotationTransform;
        Vector3 target = transform.localEulerAngles;
        target.z = -15f;

        rotationTween = transform.DOLocalRotate(target, MoveTime).SetEase(Ease.OutSine);

        await UniTaskExtensions.WaitWithProgress(MoveTime, token, Acceleration);
    }

    private async UniTask Decelerate(CancellationToken token)
    {
        rotationTween?.Kill();

        var transform = roboC.RotationTransform;
        Vector3 target = transform.localEulerAngles;
        target.z = 15f;

        rotationTween = transform.DOLocalRotate(target, DecelerateTime).SetEase(Ease.OutSine);

        await UniTaskExtensions.WaitWithProgress(DecelerateTime, token, Deceleration);

        rotationTween?.Kill();
        target.z = 0f;

        rotationTween = transform.DOLocalRotate(target, DecelerateTime).SetEase(Ease.OutBounce);

        _moveComponent.direction.x = 0;
        currentDir = 0;
        
        await UniTask.WaitForSeconds(0.5f, cancellationToken:token);
    }

    public void Acceleration(in ProgressTimer timer)
    {
        _moveComponent.speedMultiplier = Mathf.Lerp(0, 2, timer.Normalized);
    }

    public void Deceleration(in ProgressTimer timer)
    {
        _moveComponent.speedMultiplier = Mathf.Lerp(2, 0, timer.Normalized);
        int dir = (int)_moveComponent.direction.x;

        spriteFlipSystem.SetFacing(timer.Normalized >= 0.3f ? -dir : dir);
    }
}


[System.Serializable]
public struct VisionHit
{
    public Collider2D collider;
    public Vector2 point;
}

[System.Serializable]
public class VisionComponent : IComponent
{
    public LayerMask targetLayer;
     
    [NonSerialized] public Collider2D[] overlapBuffer = new Collider2D[8];
    [NonSerialized] public RaycastHit2D[] rayBuffer = new RaycastHit2D[8];
    
    private VisionHit[] hitsBuffer = new VisionHit[16];

    [NonSerialized] public int overlapFullness;
    [NonSerialized] public int rayFullness;
    
    [NonSerialized] public bool hasContact;
    public Transform currentTarget;
    [NonSerialized] public Vector3 lastKnownPosition;
    [NonSerialized] public float timeSinceLastSeen;
    
    public float forgetTime = 2f;

    [SerializeReference,SubclassSelector] public IVisionFilter[] filters;

    public (VisionHit[] buffer, int fillness) OnVision()
    {
        int count = 0;
        
        for (int i = 0; i < overlapFullness && count < hitsBuffer.Length; i++)
        {
            var col = overlapBuffer[i];
            if (col == null) continue;

            Vector2 point = col.transform.position;
            
            var temp = new VisionHit
            {
                collider = col,
                point = point,
            };
            bool passes = true;
            foreach (var filter in filters)
            {
                if (!filter.Passes(temp))
                {
                    passes = false;
                    break;
                }
            }

            if (passes)
            {
                hitsBuffer[count] = temp;
                count++;
            }
        }
        
        for (int i = 0; i < rayFullness && count < hitsBuffer.Length; i++)
        {
            var hit = rayBuffer[i];
            if (hit.collider == null) continue;

            var temp = new VisionHit
            {
                collider = hit.collider,
                point = hit.point,
            };
            
            bool passes = true;
            
            foreach (var filter in filters)
            {
                if (!filter.Passes(temp))
                {
                    passes = false;
                    break;
                }
            }
            if (passes)
            {
                hitsBuffer[count] = temp;
                count++;
            }
        }

        return (hitsBuffer, count);
    }
}

public interface IVisionFilter
{
    bool Passes(in VisionHit hit);
}

[Serializable]
public class FilterByLineOfSight : IVisionFilter
{
    public AbstractEntity owner;
    public LayerMask obstacleLayer;

    public bool Passes(in VisionHit hit)
    {
        Vector2 origin = owner.transform.position;
        Vector2 target = hit.collider.transform.position;

        return !Physics2D.Linecast(origin, target,obstacleLayer);
    }
}
public class BoxVisionComponent : IComponent
{
    public Vector2 size = new Vector2(16,2);
    public Vector2 offset = new Vector2(0,0);
    public float angle = 0;
}

[Serializable]
public class FilterByViewAngle : IVisionFilter
{
    public AbstractEntity owner;
    public float viewAngle = 90f;

    public bool Passes(in VisionHit hit)
    {
        Vector2 direction = (hit.collider.transform.position - owner.transform.position).normalized;

        float angle = Vector2.Angle(owner.transform.right, direction);

        return angle <= viewAngle * 0.5f;
    }
}
public class BoxVisionSystem : BaseSystem, IDisposable
{
    private BoxVisionComponent boxConfig;
    private VisionComponent visionComponent;
    private Transform ownerTransform;
    private ContactFilter2D contactFilter2D;
    
    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        boxConfig = owner.GetControllerComponent<BoxVisionComponent>();
        visionComponent = owner.GetControllerComponent<VisionComponent>();
        ownerTransform = owner.transform;

        contactFilter2D = new ContactFilter2D
        {
            layerMask = visionComponent.targetLayer,
            useLayerMask = true,
        };
        
        owner.OnFixedUpdate += Update;
    }

    public override void OnUpdate()
    {
        Vector2 origin = (Vector2)ownerTransform.position + boxConfig.offset;

        visionComponent.overlapFullness = Physics2D.OverlapBox(
            origin,
            boxConfig.size,
            boxConfig.angle,
            contactFilter2D,
            visionComponent.overlapBuffer
        );
    }
    public void Dispose()
    {
        owner.OnFixedUpdate -= Update;
    }
}

public class VisionMemorySystem : BaseSystem,IDisposable
{
    private VisionComponent vision;

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        vision = owner.GetControllerComponent<VisionComponent>();
        
        owner.OnFixedUpdate +=  Update;
    }

    public override void OnUpdate()
    {
        var (hits, count) = vision.OnVision();

        bool seesCurrentTarget = false;

        if (vision.currentTarget != null)
        {
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform == vision.currentTarget)
                {
                    seesCurrentTarget = true;
                    vision.lastKnownPosition = hits[i].point;
                    break;
                }
            }
        }

        if (seesCurrentTarget)
        {
            vision.timeSinceLastSeen = 0f;
            vision.hasContact = true;
        }
        else if (vision.currentTarget != null)
        {
            vision.timeSinceLastSeen += Time.deltaTime;

            if (vision.timeSinceLastSeen >= vision.forgetTime)
            {
                vision.currentTarget = null;
                vision.hasContact = false;
                vision.timeSinceLastSeen = 0f;
            }
        }
        else if (count > 0)
        {
            vision.currentTarget = hits[0].collider.transform;
            vision.lastKnownPosition = hits[0].point;
            vision.hasContact = true;
            vision.timeSinceLastSeen = 0f;
        }
    }
    public void Dispose()
    {
        owner.OnFixedUpdate -=  Update;
    }
}