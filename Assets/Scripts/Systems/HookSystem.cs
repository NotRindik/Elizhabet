using System.Collections;
using Assets.Scripts;
using Controllers;
using States;
using Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

public class HookSystem : BaseSystem,IStopCoroutineSafely
{
    private HookComponent _hookComponent;
    private ColorPositioningComponent _colorPositioning;
    private ControllersBaseFields _baseFields;
    private FSMSystem _fsm;
    private LineRenderer inst;

    private Vector2 lastPos;
    public override void Initialize(Controller owner)
    {
        base.Initialize(owner);
        _hookComponent = owner.GetControllerComponent<HookComponent>();
        _colorPositioning = owner.GetControllerComponent<ColorPositioningComponent>();
        _baseFields = base.owner.GetControllerComponent<ControllersBaseFields>();
        _fsm = base.owner.GetControllerSystem<FSMSystem>();
        owner.OnGizmosUpdate += OnDrawGizmos;
    }

    public override void OnUpdate()
    {
        if (_hookComponent.HookGrabProcess == null)
        {
            _hookComponent.HookGrabProcess = owner.StartCoroutine(TryHookGrapple());
        }
    }

    public IEnumerator HookGrabProcess(Vector2 hookPoint,LineRenderer lineRenderer)
    {
        _hookComponent.IsHooked = true;
        _fsm.SetState(new FallState((PlayerController)owner));
        Vector2 startPos = owner.transform.position;
        float elapsedTime = 0f;
        float sfxTime = 0.1f;
        _baseFields.rb.bodyType = RigidbodyType2D.Dynamic;
        _baseFields.rb.gravityScale = 0;
        AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}HookStuck");
        foreach (var system in owner.systems.Values)
        {
            if (system is IStopCoroutineSafely safe && !(system is HookSystem))
            {
                safe.StopCoroutineSafely();
            }
        }
        while (elapsedTime < _hookComponent.MoveTime)
        {
            _baseFields.rb.linearVelocity = Vector2.zero;
            lineRenderer.SetPosition(0,lineRenderer.transform.InverseTransformPoint(owner.transform.position));
            elapsedTime += Time.deltaTime;
            sfxTime -= Time.deltaTime;
            float t = elapsedTime / _hookComponent.MoveTime;
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
        _baseFields.rb.linearVelocity = Vector2.up * _hookComponent.upForceAfterHook;
        _hookComponent.IsHooked = false;
    }
    
    public IEnumerator TryHookGrapple()
    {
        float elapsedTime = 0f;
        Vector2 screenPos = Pointer.current.position.ReadValue();
        Vector3 worldMousePos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -Camera.main.transform.position.z));
        Vector2 mouseWorldPos2D = new Vector2(worldMousePos.x, worldMousePos.y);
        var audio = AudioManager.instance.PlaySoundEffect($"{FileManager.SFX}HookOut");
        inst = Object.Instantiate(_hookComponent.hookPrefab,_colorPositioning.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint(),Quaternion.identity);
        var boobsStartPos = _colorPositioning.pointsGroup[ColorPosNameConst.BOOBS].FirstActivePoint();
        Vector2 dir = (mouseWorldPos2D - boobsStartPos).normalized;
        Collider2D hookedWall = null;
        while (elapsedTime < _hookComponent.MoveTime)
        {
            inst.transform.position = _colorPositioning.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint();
            
            var hitPoint =  inst.transform.position + owner.transform.InverseTransformDirection(dir * _hookComponent.Range);
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
            elapsedTime = 0f;
            Vector2 target = _colorPositioning.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint();
            while (Vector2.Distance(lastPos, target) > 0.01f)
            {
                target = _colorPositioning.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint();
                elapsedTime += Time.deltaTime;
                lastPos = Vector2.MoveTowards(lastPos, target, elapsedTime);
                inst.transform.position = target;
                hookedWall = Physics2D.OverlapCircle(lastPos,_hookComponent.hookedRadius,_hookComponent.hookLayer);
                if(hookedWall)
                    break;
                inst.SetPosition(1, inst.transform.InverseTransformPoint(lastPos));
                yield return null;
            }
            if(hookedWall)
                yield return HookGrabProcess(lastPos,inst);
        }
        
        if(audio)
            AudioManager.instance.StopSoundEffect(audio.clip);
        Object.Destroy(inst.gameObject);
        _hookComponent.HookGrabProcess = null;
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
        var hit = Physics2D.Raycast(startPos, dir, _hookComponent.Range, _hookComponent.hookLayer);
        if (hit.collider != null) {
            Gizmos.DrawLine(startPos, hit.point);
        } else {
            Gizmos.DrawLine(startPos, startPos + dir * _hookComponent.Range);
        }

        if (_hookComponent.HookGrabProcess == null)
        {
            return;
        }
        Gizmos.DrawWireSphere(lastPos,_hookComponent.hookedRadius);
    }
    public void StopCoroutineSafely()
    {
        
        if(_hookComponent.HookGrabProcess == null)
            return;
        owner.StopCoroutine(_hookComponent.HookGrabProcess);
        if(inst)
            Object.Destroy(inst.gameObject);
        
        _baseFields.rb.gravityScale = 1;
        _baseFields.rb.linearVelocity = Vector2.up;
        _hookComponent.IsHooked = false;
        _hookComponent.HookGrabProcess = null;
    }
}
[System.Serializable]
public class HookComponent: IComponent
{
    public Coroutine HookGrabProcess;
    public float Range;
    public float hookedRadius = 0.5f;
    public float MoveTime;
    public bool IsHooked;
    public float upForceAfterHook = 3;
    public LayerMask hookLayer;
    public LineRenderer hookPrefab;
}