using System.Collections;
using Assets.Scripts;
using Controllers;
using UnityEngine;
using UnityEngine.Serialization;

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
            owner.OnGizmosUpdate += OnDrawGizmos;
        }

        public override void Update()
        {
            if(_wallEdgeClimbComponent.EdgeStuckProcess == null)
                _wallEdgeClimbComponent.EdgeStuckProcess = owner.StartCoroutine(EdgeStuckProcess());
        }

        public IEnumerator EdgeStuckProcess()
        {
            if (CanGrabLedge(out var foreHeadHit,out var tazcastHit))
            {
                owner.transform.position = tazcastHit.point - (Vector2)owner.transform.right * 0.1f * owner.transform.localScale.x;

                
                while (((PlayerController)owner).input.GetState().movementDirection.x != owner.transform.localScale.x)
                {
                    ((EntityController)owner).baseFields.rb.linearVelocity = Vector2.zero;
                    ((EntityController)owner).baseFields.rb.gravityScale = 0;
                    yield return null;
                }
                
                var floor = Physics2D.Raycast(
                    _colorPositioningComponent.pointsGroup[ColorPosNameConst.FORE_HEAD].FirstActivePoint() + new Vector2(0.5f,0)*owner.transform.localScale.x,
                    Vector2.down,
                    _wallEdgeClimbComponent.foreHeadRayDistance,
                    _wallEdgeClimbComponent.wallLayerMask);
                if(floor.collider)
                    owner.transform.position = floor.point + new Vector2(0,0.5f);
            }
            
            ((EntityController)owner).baseFields.rb.gravityScale = 1;
                
            
            _wallEdgeClimbComponent.EdgeStuckProcess = null;
        }
        public bool CanGrabLedge(out RaycastHit2D foreHeadChecker,out RaycastHit2D tazChecker)
        {
            foreHeadChecker = Physics2D.Raycast(
                _colorPositioningComponent.pointsGroup[ColorPosNameConst.FORE_HEAD].FirstActivePoint(),
                owner.transform.right * owner.transform.localScale.x,
                _wallEdgeClimbComponent.foreHeadRayDistance,
                _wallEdgeClimbComponent.wallLayerMask);

            tazChecker = Physics2D.Raycast(
                _colorPositioningComponent.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint(),
                owner.transform.right * owner.transform.localScale.x,
                _wallEdgeClimbComponent.tazRayDistance,
                _wallEdgeClimbComponent.wallLayerMask);

            return !foreHeadChecker && tazChecker;
        }

        public void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(_colorPositioningComponent.pointsGroup[ColorPosNameConst.FORE_HEAD].FirstActivePoint(),
                owner.transform.right*owner.transform.localScale.x*_wallEdgeClimbComponent.foreHeadRayDistance);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(_colorPositioningComponent.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint(),
                owner.transform.right*owner.transform.localScale.x*_wallEdgeClimbComponent.tazRayDistance);
            Gizmos.DrawRay(
                _colorPositioningComponent.pointsGroup[ColorPosNameConst.FORE_HEAD].FirstActivePoint() + (new Vector2(0.5f,0)*owner.transform.localScale.x),
                Vector2.down*_wallEdgeClimbComponent.foreHeadRayDistance);
        }
    }
    
    [System.Serializable]
    public class WallEdgeClimbComponent : IComponent
    {
        public float tazRayDistance;
        public float foreHeadRayDistance;
        public LayerMask wallLayerMask;
        public Coroutine EdgeStuckProcess;
    }
}
