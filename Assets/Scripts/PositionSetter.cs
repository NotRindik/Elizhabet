using Assets.Scripts;
using Controllers;
using Systems;
using UnityEngine;

public class PositionSetter : MonoBehaviour
{
    private ColorPositioningComponent ColorPositioningComponent;
    public EntityController entityController;
    public ColorPosNameConst nameConst;

    public void Start()
    {
        ColorPositioningComponent = entityController.GetControllerComponent<ColorPositioningComponent>();
        ColorPositioningComponent.AfterColorCalculated += AfterColorCalculated;
    }

    private void AfterColorCalculated()
    {
        transform.position = ColorPositioningComponent.pointsGroup[nameConst].FirstActivePoint();
    }
}
