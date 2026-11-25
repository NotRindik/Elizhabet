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
        private AnimationComponentsComposer _animation;
        public WallEdgeClimbComponent wallEdgeClimbComponent;

        private bool wasLocked;
        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _colorPositioningComponent = owner.GetControllerComponent<ColorPositioningComponent>();
            _wallGlideComponent = (WallGlideComponent)_modComponent.GetModBySystem(this).modComponent;
            _baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            _animation = owner.GetControllerComponent<AnimationComponentsComposer>();
            wallEdgeClimbComponent = owner.GetControllerComponent<WallEdgeClimbComponent>();
            owner.OnUpdate += Update;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            if (!CanWallGlide() || wallEdgeClimbComponent.EdgeStuckProcess != null)
            {
                wasLocked = false;
                _animation.SetRequest("WallGlide", false);
                return;
            }

            wasLocked = true;
            _animation.SetRequest("WallGlide", true);
            Vector2 vel = _baseFields.rb.linearVelocity;
            vel.y = Mathf.Max(vel.y, -2.4f);
            _baseFields.rb.linearVelocity = vel;
        }

        public override void OnDisable()
        {
            base.OnDisable();
            wasLocked = false;
            _animation?.SetRequest("WallGlide", false);
        }

        public bool CanWallGlide() =>  Physics2D.Raycast(_colorPositioningComponent.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint(), transform.right, _wallGlideComponent.rayDist, _wallGlideComponent.wallLayer);

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