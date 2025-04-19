using Assets.Scripts;
using Controllers;
using UnityEngine;
namespace Systems
{
    public class WallEdgeClimbSystem : BaseSystem
    {
        private ColorPositioningComponent _colorPositioningComponent;
        private WallEdgeClimbComponent _wallEdgeClimbComponent;
        private JumpSystem _jumpSystem;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _colorPositioningComponent = owner.GetControllerComponent<ColorPositioningComponent>();
            _wallEdgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
            _jumpSystem = owner.GetControllerSystem<JumpSystem>();
            owner.OnUpdate += Update;
            owner.OnGizmosUpdate += OnDrawGizmos;
        }

        public override void Update()
        {
            if (CanGrabLedge(out var foreHeadHit,out var tazcastHit))
            {
                owner.transform.position = tazcastHit.point;
                ((EntityController)owner).baseFields.rb.gravityScale = 0;
            }
        }
        bool CanGrabLedge(out RaycastHit2D foreHeadChecker,out RaycastHit2D tazChecker)
        {
            foreHeadChecker = Physics2D.Raycast(
                _colorPositioningComponent.pointsGroup[ColorPosNameConst.FORE_HEAD].FirstActivePoint(),
                owner.transform.right * owner.transform.localScale.x,
                _wallEdgeClimbComponent.rayDistance,
                _wallEdgeClimbComponent.wallLayerMask);

            tazChecker = Physics2D.Raycast(
                _colorPositioningComponent.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint(),
                owner.transform.right * owner.transform.localScale.x,
                _wallEdgeClimbComponent.rayDistance,
                _wallEdgeClimbComponent.wallLayerMask);

            return !foreHeadChecker && tazChecker;
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
