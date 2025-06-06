using System.Collections;
using Controllers;
using std;
using Systems;
using UnityEngine;
using UnityEngine.InputSystem;

public class HookSystem : BaseSystem
{
    private HookComponent _hookComponent;

    public override void Initialize(Controller owner)
    {
        base.Initialize(owner);
        _hookComponent = owner.GetControllerComponent<HookComponent>();

        owner.OnGizmosUpdate += OnDrawGizmos;
    }

    public override void OnUpdate()
    {
        if (_hookComponent.HookGrabProcess == null)
        {
            _hookComponent.HookGrabProcess = owner.StartCoroutine(HookGrabProcess());
        }
    }

    public IEnumerator HookGrabProcess()
    {
        _hookComponent.IsHooked = true;
        Vector2 startPos = owner.transform.position;
        Vector2 mouseScreenPos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 dir = mouseScreenPos - startPos;
        float elapsedTime = 0f;
        var targetPoint = Physics2D.Raycast(startPos, dir.normalized, _hookComponent.Range, _hookComponent.hookLayer);
        while (elapsedTime < _hookComponent.MoveTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / _hookComponent.MoveTime;
            owner.transform.position = Vector2.Lerp(startPos, targetPoint.point, t);
            yield return null;
        }
        owner.transform.position = targetPoint.point;
        _hookComponent.IsHooked = false;
    }

    void OnDrawGizmos() {
        if (owner == null || _hookComponent == null || Camera.main == null) return;

        Vector2 startPos = owner.transform.position;
        Vector2 mouseScreenPos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector2 dir = mouseScreenPos - startPos;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(startPos, startPos + dir.normalized * _hookComponent.Range);
    }

}
[System.Serializable]
public class HookComponent: IComponent
{
    public Coroutine HookGrabProcess;
    public float Range;
    public float MoveSpeed;
    public float MoveTime;
    public bool IsHooked;
    public LayerMask hookLayer;
}