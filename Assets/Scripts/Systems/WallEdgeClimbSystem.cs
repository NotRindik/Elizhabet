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
            owner.OnUpdate -= OnDrawGizmos;
        }

        public override void Update()
        {
            RaycastHit2D foreHeadChecker = Physics2D.Raycast(ColorPositioningComponent.pointsGroup[Assets.Scripts.ColorPosNameConst.FORE_HEAD].FirstActivePoint(),owner.transform.right);
        }

        public void OnDrawGizmos()
        {
        }
    }
    
    [System.Serializable]
    public class WallEdgeClimbComponent : IComponent
    {
        
    }
}
