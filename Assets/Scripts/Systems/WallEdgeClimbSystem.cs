using Controllers;
using UnityEngine;
namespace Systems
{
    public class WallEdgeClimbSystem : BaseSystem
    {
        private ColorPositioningComponent ColorPositioningComponent;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            ColorPositioningComponent = owner.GetControllerComponent<ColorPositioningComponent>();
            owner.OnUpdate += Update;
        }

        public override void Update()
        {
            Physics2D foreHeadChecker = Physics2D.Raycast(ColorPositioningComponent.pointsGroup[Assets.Scripts.ColorPosNameConst.FORE_HEAD],owner.transform.right);
        }
    }
    
    [System.Serializable]
    public class WallEdgeClimbComponent : IComponent
    {
        
    }
}
