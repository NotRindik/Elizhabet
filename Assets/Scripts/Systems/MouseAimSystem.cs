using System;
using Systems;
using UnityEngine;

public class MouseAimSystem : BaseSystem,IDisposable
{
    private MouseAimComponent _aim;
    private HandsRotatoningSystem _hands;
    private IInputProvider _input;
    private ItemComponent itemComponent;
    protected Item item;
    
    private AnimationComponentsComposer animationComponent;

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
        _input = entity.GetControllerSystem<IInputProvider>();
        _input.GetState().Point.performed += OnPoint;
        animationComponent = entity.GetControllerComponent<AnimationComponentsComposer>();
        
        item.itemPositioningSystem = new OneHandAlongArmPositioning();
        item.itemPositioningSystem.Initialize(owner);
        
        if (animationComponent != null)
        {
            animationComponent.animations["RightPivot"].animator.enabled = false;
            animationComponent.TakeControl("RightPivot");
        }
    }

    protected override void OnActiveStateChange(bool value)
    {
        base.OnActiveStateChange(value);
        
        if (animationComponent == null)
            return;
        
        if (value)
        {
            animationComponent.animations["RightPivot"].animator.enabled = false;
            animationComponent.TakeControl("RightPivot");
        }
        else
        {
            animationComponent.animations["RightPivot"].animator.enabled = true;
            animationComponent.ReleaseControl("RightPivot");
        }
    }

    private void OnUnequip()
    {
        _hands = null;
        item.itemPositioningSystem = null;
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

    private void CalculateAim()
    {
        Vector3 worldPos = ContextManager.Instance.mainCamera.ScreenToWorldPoint(_aim.pointPos);
        worldPos.z = 0f;

        Vector2 ownerPos = transform.position;

        Vector2 dir = ((Vector2)worldPos - ownerPos).normalized;

        float distance = Vector2.Distance(ownerPos, worldPos);

        Vector2 target = ownerPos + dir * distance;

        _hands?.RotateHand(_aim.handSide, target);
    }
    

    public void Dispose()
    {
        owner.OnLateUpdate -= Update;
        item.OnTake -= OnTake;
        item.OnReferenceClean -= OnUnequip;
        
        if (animationComponent != null)
        {
            animationComponent.animations["RightPivot"].animator.enabled = true;
            animationComponent.ReleaseControl("RightPivot");
        }
    }
}


[Serializable]
public class MouseAimComponent : IComponent
{
    public Vector2 pointPos;
    
    public Side handSide = Side.Right;
}