using Controllers;
using UnityEngine;
namespace Systems
{
    public class WallEdgeClimbSystem : BaseSystem
    {
        private ColorPositioningComponent _colorPositioningComponent;
        private WallEdgeClimbComponent _wallEdgeClimbComponent;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _colorPositioningComponent = owner.GetControllerComponent<ColorPositioningComponent>();
            _wallEdgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
            owner.OnUpdate += Update;
            owner.OnGizmosUpdate += OnDrawGizmos;
        }

        public override void Update()
        {
            RaycastHit2D foreHeadChecker = Physics2D.Raycast(
                _colorPositioningComponent.pointsGroup[Assets.Scripts.ColorPosNameConst.FORE_HEAD].FirstActivePoint()
                ,owner.transform.right*owner.transform.localScale.x,_wallEdgeClimbComponent.rayDistance,_wallEdgeClimbComponent.wallLayerMask);
            RaycastHit2D taz = Physics2D.Raycast(
                _colorPositioningComponent.pointsGroup[Assets.Scripts.ColorPosNameConst.TAZ].FirstActivePoint()
                ,owner.transform.right*owner.transform.localScale.x,_wallEdgeClimbComponent.rayDistance,_wallEdgeClimbComponent.wallLayerMask);
            if (foreHeadChecker)
            {
                Debug.Log("WALL");
            }
            if (taz)
            {
                Debug.Log("TAZOL");
            }
        }

        public void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(_colorPositioningComponent.pointsGroup[Assets.Scripts.ColorPosNameConst.FORE_HEAD].FirstActivePoint(),
                owner.transform.right*owner.transform.localScale.x*_wallEdgeClimbComponent.rayDistance);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(_colorPositioningComponent.pointsGroup[Assets.Scripts.ColorPosNameConst.TAZ].FirstActivePoint(),
                owner.transform.right*owner.transform.localScale.x*_wallEdgeClimbComponent.rayDistance);
        }
    }
    
    [System.Serializable]
    public class WallEdgeClimbComponent : IComponent
    {
        public float rayDistance;
        public LayerMask wallLayerMask;
    }
}
