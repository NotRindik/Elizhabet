using Assets.Scripts;
using Controllers;
using Systems;
using UnityEngine;

public class PositionSetter : MonoBehaviour
{
    private ColorPositioningComponent ColorPositioningComponent;
    private ColorPositioningSystem colorPositioningSystem;
    public EntityController entityController;
    public ColorPosNameConst nameConst;
    public int priority = 0;

    public void Start()
    {
        ColorPositioningComponent = entityController.GetControllerComponent<ColorPositioningComponent>();
        colorPositioningSystem = entityController.GetControllerSystem<ColorPositioningSystem>();
        ColorPositioningComponent.AfterColorCalculated.Add( AfterColorCalculated , priority);
    }
    private void AfterColorCalculated()
    {
        transform.position = ColorPositioningComponent.pointsGroup[nameConst].FirstActivePoint();
        colorPositioningSystem.ForceUpdatePosition();
    }
}
