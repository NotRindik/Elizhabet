// PositionSetter.cs
using Controllers;
using Systems;
using UnityEngine;

[ExecuteAlways]
public class PositionSetter : MonoBehaviour
{
    private ColorPositioningComponent colorPositioningComponent;
    private ColorPositioningSystem colorPositioningSystem;

    public EntityController entityController;
    public ColorPosNameConst nameConst;
    public ColorPosNameConst[] ownGroups;

    public int priority = 0;

    private bool _subscribed;

    private void OnEnable()
    {
        if (Application.isPlaying)
            TrySubscribeRuntime();
    }

    private void Update()
    {
        if (Application.isPlaying && !_subscribed)
            TrySubscribeRuntime();
    }

    private void TrySubscribeRuntime()
    {
        if (_subscribed) return;
        if (entityController == null) return;

        var component = entityController.GetControllerComponent<ColorPositioningComponent>();
        if (component == null) return;

        colorPositioningComponent = component;
        colorPositioningSystem = entityController.GetControllerSystem<ColorPositioningSystem>();

        colorPositioningComponent.AfterColorCalculated.Remove(RuntimeCallback);
        colorPositioningComponent.AfterColorCalculated.Add(RuntimeCallback, priority);
        _subscribed = true;
    }

    private void OnDisable()
    {
        colorPositioningComponent?.AfterColorCalculated.Remove(RuntimeCallback);
        _subscribed = false;
    }
    
    private void RuntimeCallback()
    {
        if (colorPositioningComponent == null) return;
        if (!colorPositioningComponent.pointsGroup.TryGetValue(nameConst, out var group)) return;

        transform.position = group.FirstActivePoint();

        if (colorPositioningSystem != null)
            colorPositioningSystem.ForceUpdatePosition(ownGroups);
    }
    
    public void ApplyEditorPosition(Vector3 position)
    {
        transform.position = position;
    }
}