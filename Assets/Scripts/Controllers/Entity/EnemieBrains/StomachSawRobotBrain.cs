using System;
using System.Threading;
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

    private ContactFilter2D filter;


    private WanderingIdle idle;
    private HitState hitState;
    private StomachSawRobotChase chaseState;
    
    private EventSoundInstance roboHitSound,enemyHitSound;


    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        SetState(new InputState());

        _fsmSystem = owner.GetControllerSystem<FSMSystem>();
        
        _robotComponent = owner.GetControllerComponent<StomachSawRobotComponent>();
        _fsmComponent = owner.GetControllerComponent<FsmComponent>();
        _attackComponent = owner.GetControllerComponent<BaseAttackComponent>();
        
        roboHitSound = new EventSoundInstance(_robotComponent.bladeHitEvent);
        enemyHitSound = new EventSoundInstance(_robotComponent.hitSound);
        
        roboHitSound.SetData(new MaterialData()
        {
            material = owner.GetComponent<AudioMaterialSetter>().AudioMaterial
        });
        
        idle = new WanderingIdle(owner);
        hitState = new HitState(owner);
        chaseState = new StomachSawRobotChase(owner);
        
        _fsmSystem.AddAnyTransition(idle, () => _robotComponent.lastHit == default && _fsmComponent.state != chaseState);
        
        _fsmSystem.AddTransition(idle,chaseState, () => _robotComponent.playerNear);
        
        _fsmSystem.AddTransition(chaseState,idle, () =>
            {
                if(!_robotComponent.playerNear)
                    _robotComponent.playAlertAnim = true;
                return !_robotComponent.playerNear;
            }
        );
        
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

    public override void OnUpdate()
    {

        var player = ContextManager.Instance.player;



        if (player != null)
        {
            Vector2 delta = player.transform.position - transform.position;

            Vector2 radius = !_robotComponent.playerNear
                ? _robotComponent.playerDetectRadius
                : _robotComponent.playerUnDetectRadius;
            
            float normX = delta.x / radius.x;
            float normY = delta.y / radius.y;
            bool insideEllipse = (normX * normX + normY * normY) <= 1f;

            bool hasLineOfSight = insideEllipse && HasLineOfSightToPlayer(delta, player.transform.position);

            _robotComponent.playerNear = hasLineOfSight;
        }
        
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
            _robotComponent.LastHit = hit.collider.gameObject;
            
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
                            material = material
                        }
                    );
                    AudioManager.instance.PlayEvent(enemyHitSound);
                }
                
                var hp = entity.GetControllerSystem<HealthSystem>();

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
    
    private bool HasLineOfSightToPlayer(Vector2 delta, Vector3 playerPos)
    {
        Vector2 origin = transform.position;
        float distance = delta.magnitude;
        
        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            delta.normalized,
            distance,
            _robotComponent.obstacleLayer
        );
        
        return hit.collider == null;
    }

    public void Dispose()
    {
        _robotComponent.sawRotation?.Kill();
        owner.OnUpdate -= Update;
        
        _fsmComponent.state.Exit();
    }
}

[System.Serializable]
public class StomachSawRobotComponent : IComponent
{
    public Transform firstPos, secondPos, RotationTransform, sawTransform;
    public RaycastHit2D lastHit;

    public float SawRotPerSec;

    public ParticleSystem hitPs;
    public EventSound hitSound,bladeHitEvent;

    public AudioSource alertSound;
    
    public Vector2 playerDetectRadius = new Vector2(6f, 3f);
    public Vector2 playerUnDetectRadius = new Vector2(8f, 4f);

    public LayerMask hitLayer;
    public LayerMask obstacleLayer;

    public RaycastHit2D[] hitBuffer = new RaycastHit2D[16];

#if UNITY_EDITOR
    [ReadOnly] public GameObject LastHit;
#endif

    public Tween sawRotation;

    public bool playerNear, playAlertAnim = true;
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
            
            await UniTaskExtensions.WaitWithProgress(moveTime, token, Acceleration);
            
            target.z = 15f;

            rotationTween = transform
                .DOLocalRotate(target, moveTime)
                .SetEase(Ease.OutSine);

            await UniTaskExtensions.WaitWithProgress(moveTime, token, Deceleration);
            
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
            _animationComponent.Play("Alert");

            roboC.alertSound.PlayOneShot(roboC.alertSound.clip);
            
            await UniTask.NextFrame(token);
            await UniTask.WaitUntil(AnimationFinished, cancellationToken: token);
            
            roboC.playAlertAnim = false;
        }
        
        _animationComponent.Play("Idle");

        while (!token.IsCancellationRequested)
        {
            var player = ContextManager.Instance.player.transform;
            
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