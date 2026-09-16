using System;
using Systems;
using UnityEngine;

public class MouseAimSystem : BaseSystem,IDisposable
{
    private MouseAimComponent _aim;
    private HandsRotatoningSystem _hands;
    private InputComponent _input;
    private ItemComponent itemComponent;

    public override void Initialize(AbstractEntity owner)
    {
        base.Initialize(owner);

        _aim = owner.GetControllerComponent<MouseAimComponent>();
        _input = owner.GetControllerComponent<InputComponent>();
        itemComponent = owner.GetControllerComponent<ItemComponent>();
        
        _input.input.GetState().Point.performed += OnPoint;
        owner.OnUpdate += Update;
    }

    public override void OnUpdate()
    {
        if (itemComponent._currentOwner != null)
        {
            _hands ??= itemComponent._currentOwner.GetControllerSystem<HandsRotatoningSystem>();
        }
        else
        {
            _hands = null;
        }
        
        base.OnUpdate();

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
        owner.OnUpdate -= Update;
        if (_input != null)
            _input.input.GetState().Point.performed -= OnPoint;
    }
}


[Serializable]
public class MouseAimComponent : IComponent
{
    public Vector2 pointPos;
    
    public Side handSide = Side.Right;
}