using Assets.Scripts;
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

    private void OnEnable()
    {
        if (entityController == null) return;

        colorPositioningComponent = entityController.GetControllerComponent<ColorPositioningComponent>();
        if (colorPositioningComponent == null) return;
        
        colorPositioningSystem = entityController.GetControllerSystem<ColorPositioningSystem>();
        
        colorPositioningComponent.AfterColorCalculated.Remove(AfterColorCalculated);
        colorPositioningComponent.AfterColorCalculated.Add(AfterColorCalculated, priority);
    }

    private void OnDisable()
    {
        colorPositioningComponent?.AfterColorCalculated.Remove(AfterColorCalculated);
    }

    private void AfterColorCalculated()
    {
        if (colorPositioningComponent == null) return;
        if (!colorPositioningComponent.pointsGroup.TryGetValue(nameConst, out var group)) return;

        transform.position = group.FirstActivePoint();

        if (Application.isPlaying && colorPositioningSystem != null)
        {
            colorPositioningSystem.ForceUpdatePosition(ownGroups);
        }
    }
}