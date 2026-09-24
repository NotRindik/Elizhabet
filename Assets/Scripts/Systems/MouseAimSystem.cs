using System;
using Systems;
using UnityEngine;

public class MouseAimSystem : BaseSystem,IDisposable
{
    private MouseAimComponent _aim;
    private HandsRotatoningSystem _hands;
    private IInputProvider _input;
    private ItemComponent itemComponent;
    private AttackComponent attackComponent;
    protected Item item;

    private float speed = 2;
    
    private AnimationComponentsComposer animationComponent;

    private IArmGrip Grip = new HybridGrip( 0.8f, 1.3f, 5f);
    
    private float deadzone = 1f;

    private bool isCalc;

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);
        item = (Item)owner;
        _aim = owner.GetControllerComponent<MouseAimComponent>();
        itemComponent = owner.GetControllerComponent<ItemComponent>();
        owner.OnLateUpdate += Update;
        
        item.OnTake += OnTake;
        item.OnReferenceClean += OnUnequip;
    }

    private void OnTake(AbstractEntity  entity)
    {
        _hands = itemComponent._currentOwner.GetControllerSystem<HandsRotatoningSystem>();
        attackComponent = itemComponent._currentOwner.GetControllerComponent<AttackComponent>();
        isCalc = true;
        _input = entity.GetControllerSystem<IInputProvider>();
        _input.GetState().Point.performed += OnPoint;
        animationComponent = entity.GetControllerComponent<AnimationComponentsComposer>();
        _hands.SetGrip(Side.Right,Grip);
        item.itemPositioningSystem = new OneHandAlongArmPositioning();
        item.itemPositioningSystem.Initialize(owner);
        attackComponent.OnPlayerTakeControlOfHand += UntakeControl;
        attackComponent.OnPlayerReleseControlOfHand += TakeControl;
        if (animationComponent != null)
        {
            animationComponent.animations["RightPivot"].animator.enabled = false;
            animationComponent.TakeControl("RightPivot");
        }

        SnapAim();
    }

    public void UntakeControl()
    {
        animationComponent.animations["RightPivot"].animator.enabled = true;
        animationComponent.ReleaseControl("RightPivot");
        isCalc = false;
    }

    public void TakeControl()
    {
        animationComponent.animations["RightPivot"].animator.enabled = false;
        animationComponent.TakeControl("RightPivot");
        isCalc = true;
    }

    private void OnUnequip()
    {
        _hands.SetGrip(Side.Right,new StraightGrip());
        _hands = null;
        UntakeControl();
        attackComponent.OnPlayerTakeControlOfHand -= UntakeControl;
        attackComponent.OnPlayerReleseControlOfHand -= TakeControl;
        attackComponent = null;
        item.itemPositioningSystem = null;
        isCalc = false;

        if (_input != null)
            _input.GetState().Point.performed -= OnPoint;
    }

    public override void OnUpdate()
    {
        CalculateAim();
    }

    private void OnPoint(InputContext context)
    {
        _aim.pointPos = context.ReadValue<Vector2>();
    }
    Vector2 target = Vector2.zero;
    private Vector2 _targetVelocity;

    private float sharpness = 15f;
    private float _angle;
    private float _dist;

    private Vector2 GetRawOffset()
    {
        var world = (Vector2)ContextManager.Instance.mainCamera.ScreenToWorldPoint(_aim.pointPos);
        return world - _hands.GetShoulderPos(_aim.handSide);
    }

    private void SnapAim()
    {
        var raw = GetRawOffset();
        _angle = Mathf.Atan2(raw.y, raw.x) * Mathf.Rad2Deg;
        _dist = raw.magnitude;
    }

    private void CalculateAim()
    {
        if(_hands == null || !isCalc) return;

        var raw = GetRawOffset();
        var t = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
        var tAngle = t * Mathf.Clamp01(raw.magnitude / deadzone);
        _angle = Mathf.LerpAngle(_angle, Mathf.Atan2(raw.y, raw.x) * Mathf.Rad2Deg, tAngle);
        _dist = Mathf.Lerp(_dist, raw.magnitude, t);

        var rad = _angle * Mathf.Deg2Rad;
        var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        _hands.RotateHand(_aim.handSide, _hands.GetShoulderPos(_aim.handSide) + dir * _dist);
    }

    public void Dispose()
    {
        owner.OnLateUpdate -= Update;
        item.OnTake -= OnTake;
        item.OnReferenceClean -= OnUnequip;

        if (attackComponent != null)
        {
            attackComponent.OnPlayerTakeControlOfHand -= UntakeControl;
            attackComponent.OnPlayerReleseControlOfHand -= TakeControl;
        }

        UntakeControl();
    }
}


[Serializable]
public class MouseAimComponent : IComponent
{
    public Vector2 pointPos;
    
    public Side handSide = Side.Right;
}