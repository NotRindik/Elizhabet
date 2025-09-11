using Assets.Scripts.Systems;
using Controllers;
using UnityEngine;

namespace Systems
{
    public class WallGlideSystem : BaseModificator
    {
        private ColorPositioningComponent _colorPositioningComponent;
        private WallGlideComponent _wallGlideComponent;
        private ControllersBaseFields _baseFields;
        private AnimationComponentsComposer _animationComponent;
        public WallEdgeClimbComponent wallEdgeClimbComponent;

        private bool wasLocked;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _colorPositioningComponent = owner.GetControllerComponent<ColorPositioningComponent>();
            _wallGlideComponent = _modComponent.GetModComponent<WallGlideComponent>();
            _baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            _animationComponent = owner.GetControllerComponent<AnimationComponentsComposer>();
            wallEdgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
            owner.OnUpdate += Update;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            if (!CanWallGlide() || wallEdgeClimbComponent.EdgeStuckProcess != null)
            {
                if (wasLocked)
                {
                    _animationComponent.UnlockParts("LeftHand", "RightHand");
                }
                return;
            }

            _animationComponent.PlayState("WallGlide");
            _animationComponent.LockParts("LeftHand", "RightHand");
            wasLocked = true;
            Vector2 vel = _baseFields.rb.linearVelocity;
            vel.y = Mathf.Max(vel.y, -2.4f);
            _baseFields.rb.linearVelocity = vel;
        }

        public bool CanWallGlide() =>  Physics2D.Raycast(_colorPositioningComponent.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint(), owner.transform.right* owner.transform.localScale.x, _wallGlideComponent.rayDist, _wallGlideComponent.wallLayer);

    }
    [System.Serializable]
    public struct WallGlideComponent : IComponent
    {
        public float rayDist;
        public LayerMask wallLayer;

        public WallGlideComponent(float dist, LayerMask layaer)
        {
            rayDist = dist;
            wallLayer = layaer;
        }
    }
}