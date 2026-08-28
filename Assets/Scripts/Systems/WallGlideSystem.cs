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
        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _colorPositioningComponent = owner.GetControllerComponent<ColorPositioningComponent>();
            _wallGlideComponent = (WallGlideComponent)_modComponent.GetModBySystem(this).modComponent;
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
                    // Раньше тут было UnlockParts("RightPivot", "RightPivot") — опечатка,
                    // LeftPivot никогда не разлочивался и оставался залочен навсегда после
                    // первого вызова WallGlide. ClearLayer снимает claim со ВСЕХ частей,
                    // которые сейчас держит Action, так что такой класс опечаток отсюда
                    // просто исчезает — нет списка имён, в котором можно ошибиться.
                    _animationComponent.ClearLayer("Action");
                    wasLocked = false;
                }
                return;
            }

            _animationComponent.PlayState("Action", "WallGlide");
            wasLocked = true;
            Vector2 vel = _baseFields.rb.linearVelocity;
            vel.y = Mathf.Max(vel.y, -2.4f);
            _baseFields.rb.linearVelocity = vel;
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