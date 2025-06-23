using System;
using System.Collections;
using Assets.Scripts;
using Controllers;
using States;
using Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

public class HookSystem : BaseSystem,IStopCoroutineSafely,IDisposable
{
    private HookComponent _hookComponent;
    private ColorPositioningComponent _colorPositioning;
    private ControllersBaseFields _baseFields;
    private FSMSystem _fsm;
    private LineRenderer inst;
    private JumpSystem _jumpSystem;
    private Collider2D hookedWall;
    private Vector2 lastPos;
    private float _koyoteTime;

    private Action<bool> _jumpHandler;
    public override void Initialize(Controller owner)
    {
        base.Initialize(owner);
        _hookComponent = owner.GetControllerComponent<HookComponent>();
        _colorPositioning = owner.GetControllerComponent<ColorPositioningComponent>();
        _baseFields = base.owner.GetControllerComponent<ControllersBaseFields>();
        _fsm = base.owner.GetControllerSystem<FSMSystem>();
        _jumpSystem = base.owner.GetControllerSystem<JumpSystem>();
        _jumpHandler = context =>
        {
            if (_hookComponent.isHooked || _koyoteTime > 0)
            {
                StopCoroutineSafely();
                _koyoteTime = -1;
                _hookComponent.isHooked = false;
                _fsm.SetState(new JumpState((PlayerController)base.owner));
            }
        };
        ((PlayerController)owner).input.GetState().Jump.performed += _jumpHandler;
        owner.OnUpdate += Timers;
        owner.OnGizmosUpdate += OnDrawGizmos;
    }
    public override void OnUpdate()
    {
        if (_hookComponent.HookGrabProcess == null)
        {
            _hookComponent.HookGrabProcess = owner.StartCoroutine(TryHookGrapple());
        }
    }

    public void Timers()
    {
        if (_koyoteTime >= 0)
        {
            _koyoteTime -= Time.deltaTime;
        }
    }

    public IEnumerator HookGrabProcess(Vector2 hookPoint,LineRenderer lineRenderer)
    {
        _hookComponent.isHooked = true;
        _fsm.SetState(new FallState((PlayerController)owner));
        Vector2 startPos = owner.transform.position;
        float elapsedTime = 0f;
        float sfxTime = 0.1f;
        _baseFields.rb.bodyType = RigidbodyType2D.Dynamic;
        _baseFields.rb.gravityScale = 0;
        AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}HookStuck");
        foreach (var system in owner.Systems.Values)
        {
            if (system is IStopCoroutineSafely safe && !(system is HookSystem))
            {
                safe.StopCoroutineSafely();
            }
        }
        
        while (elapsedTime < _hookComponent.moveTimeAfterHooked)
        {
            _baseFields.rb.linearVelocity = Vector2.zero;
            lineRenderer.SetPosition(0,lineRenderer.transform.InverseTransformPoint(owner.transform.position));
            elapsedTime += Time.deltaTime;
            sfxTime -= Time.deltaTime;
            float t = elapsedTime / _hookComponent.moveTimeAfterHooked;
            _baseFields.rb.MovePosition(Vector2.Lerp(startPos, hookPoint, t));
            if (sfxTime <= 0)
            {
                AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}HookPulling");
                sfxTime = 0.1f;
            }
            if (Vector2.Distance(startPos,hookPoint) < 0.2f)
            {
                break;
            }
            
            yield return null;
        }
        _baseFields.rb.gravityScale = 1;
        _baseFields.rb.linearVelocity += Vector2.up * _hookComponent.upForceAfterHook;
        _hookComponent.isHooked = false;
        _koyoteTime = _hookComponent.koyoteTime;
        _hookComponent.isHookBacked = true;
    }
    
    public IEnumerator TryHookGrapple()
    {
        _hookComponent.isHookBacked = false;
        float elapsedTime = 0f;
        Vector2 screenPos = Pointer.current.position.ReadValue();
        Vector3 worldMousePos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -Camera.main.transform.position.z));
        Vector2 mouseWorldPos2D = new Vector2(worldMousePos.x, worldMousePos.y);
        var audio = AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}HookOut");
        inst = Object.Instantiate(_hookComponent.hookPrefab,_colorPositioning.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint(),Quaternion.identity);
        var boobsStartPos = _colorPositioning.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint();
        Vector2 dir = (mouseWorldPos2D - boobsStartPos).normalized;
        hookedWall = null;
        while (elapsedTime < _hookComponent.moveTime)
        {
            inst.transform.position = _colorPositioning.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint();
            
            var hitPoint =  inst.transform.position + owner.transform.InverseTransformDirection(dir * _hookComponent.range);
            elapsedTime += Time.deltaTime;
            lastPos = Vector2.Lerp(boobsStartPos, hitPoint, elapsedTime);
            hookedWall = Physics2D.OverlapCircle(lastPos,_hookComponent.hookedRadius,_hookComponent.hookLayer);
            inst.SetPosition(1, inst.transform.InverseTransformPoint(lastPos));
            if(hookedWall)
                break;
            yield return null;
        }
        if(hookedWall)
            yield return HookGrabProcess(lastPos,inst);
        else
        {
            yield return HookBack();
            if(hookedWall)
                yield return HookGrabProcess(lastPos,inst);
        }
        
        if(audio)
            AudioManager.instance.StopSoundEffect(audio.clip);
        Object.Destroy(inst.gameObject);
        _hookComponent.HookGrabProcess = null;
    }
    
    public IEnumerator HookBack()
    {
        float speed = 20f;
        _hookComponent.isHookBacked = false;
        
        Vector2 current = lastPos;

        while (Vector2.Distance(current, _colorPositioning.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint()) > 0.05f)
        {
            Vector2 target = _colorPositioning.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint();
            current = Vector2.MoveTowards(current, target, speed * Time.deltaTime);
            hookedWall = Physics2D.OverlapCircle(current,_hookComponent.hookedRadius,_hookComponent.hookLayer);
            if(hookedWall)
                break;
            if (inst != null)
            {
                inst.transform.position = target;
                inst.SetPosition(0, Vector3.zero);
                inst.SetPosition(1, inst.transform.InverseTransformPoint(current));
            }

            yield return null;
        }

        _hookComponent.isHookBacked = true;
    }


    void OnDrawGizmos() {
        if (owner == null || _hookComponent == null || Camera.main == null || Pointer.current == null)
            return;

        Vector2 startPos = owner.transform.position;
        
        Vector2 screenPos = Pointer.current.position.ReadValue();
        Vector3 worldMousePos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -Camera.main.transform.position.z));
        Vector2 mouseWorldPos2D = new Vector2(worldMousePos.x, worldMousePos.y);

        Vector2 dir = (mouseWorldPos2D - startPos).normalized;

        Gizmos.color = Color.green;
        var hit = Physics2D.Raycast(startPos, dir, _hookComponent.range, _hookComponent.hookLayer);
        if (hit.collider != null) {
            Gizmos.DrawLine(startPos, hit.point);
        } else {
            Gizmos.DrawLine(startPos, startPos + dir * _hookComponent.range);
        }

        if (_hookComponent.HookGrabProcess == null)
        {
            return;
        }
        Gizmos.DrawWireSphere(lastPos,_hookComponent.hookedRadius);
    }
    public void StopCoroutineSafely()
    {
        if (_hookComponent.HookGrabProcess == null)
            return;
        
        owner.StopCoroutine(_hookComponent.HookGrabProcess);

        if (!_hookComponent.isHookBacked)
        {
            owner.StartCoroutine(StoppingCoroutineProcess());
            return;
        }

        CleanupHook();
    }

    private IEnumerator StoppingCoroutineProcess()
    {
        yield return HookBack();
        CleanupHook();
    }
    
    private void CleanupHook()
    {
        if (inst)
            Object.Destroy(inst.gameObject);

        _hookComponent.HookGrabProcess = null;
        _hookComponent.isHooked = false;
        _hookComponent.isHookBacked = true;

        _baseFields.rb.gravityScale = 1;
        _baseFields.rb.linearVelocity += Vector2.up * _hookComponent.upForceAfterHook;
    }
    public void Dispose()
    {
        owner.OnUpdate -= Timers;
        owner.OnGizmosUpdate -= OnDrawGizmos;
        ((PlayerController)owner).input.GetState().Jump.performed -= _jumpHandler;
    }
}
[System.Serializable]
public class HookComponent: IComponent
{
    public Coroutine HookGrabProcess;
    public float range;
    public float hookedRadius = 0.5f;
    public float moveTime;
    public float moveTimeAfterHooked = 0.3f;
    public bool isHooked;
    public float upForceAfterHook = 3;
    public LayerMask hookLayer;
    public LineRenderer hookPrefab;
    public bool isHookBacked = true;
    public float koyoteTime;
}