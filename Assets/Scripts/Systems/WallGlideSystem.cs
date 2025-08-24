using Controllers;
using UnityEngine;

namespace Systems
{
    public class WallGlideSystem : BaseSystem
    {
        private ColorPositioningComponent _colorPositioningComponent;
        private WallGlideComponent _wallGlideComponent;
        private ControllersBaseFields _baseFields;
        private AnimationComponent _animationComponent;
        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            _colorPositioningComponent = owner.GetControllerComponent<ColorPositioningComponent>();
            _wallGlideComponent = owner.GetControllerComponent<WallGlideComponent>();
            _baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            _animationComponent = owner.GetControllerComponent<AnimationComponent>();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            Vector2 vel = _baseFields.rb.linearVelocity;
            vel.y = Mathf.Max(vel.y, -3f);
            _baseFields.rb.linearVelocity = vel;
        }

        public bool CanWallGlide() =>  Physics2D.Raycast(_colorPositioningComponent.pointsGroup[ColorPosNameConst.TAZ].FirstActivePoint(), owner.transform.right* owner.transform.localScale.x, _wallGlideComponent.rayDist, _wallGlideComponent.wallLayer);

    }
    [System.Serializable]
    public class WallGlideComponent : IComponent
    {
        public float rayDist = 1;
        public LayerMask wallLayer;
    }
}