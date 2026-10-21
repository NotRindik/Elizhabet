using System;
using Controllers;
using UnityEngine;

namespace Systems
{
    internal class StepClimbSystem : BaseSystem, IDisposable
    {
        private StepClimbComponent _stepClimbComponent;
        private ControllersBaseFields _baseFields;
        private GroundingComponent _groundC;
        private MoveComponent moveC;
        private DashComponent dashC;
        private WallRunComponent wllRunC;

        private Rigidbody2D Rb => _baseFields.rb;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _stepClimbComponent = owner.GetControllerComponent<StepClimbComponent>();
            _baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            _groundC = owner.GetControllerComponent<GroundingComponent>();
            moveC = owner.GetControllerComponent<MoveComponent>();
            dashC = owner.GetControllerComponent<DashComponent>();
            wllRunC = owner.GetControllerComponent<WallRunComponent>();
            owner.OnFixedUpdate += Update;
        }

        public override void OnUpdate()
        {
            if (wllRunC != null && wllRunC.wallRunProcess != null)
                return;

            bool isDashing = dashC != null && dashC.isDash;
            
            if (!_groundC.isGround && !isDashing)
                return;

            float facing = transform.FacingSign();

            bool movesForward = isDashing || (moveC != null && moveC.direction.x * facing > 0.01f);
            if (!movesForward)
                return;

            if (Rb.linearVelocity.y > 0.1f)
                return;

            Vector2 normal = _groundC.groundNormal;
            if (normal.y <= 0.01f)
                return;

            if (Vector2.Angle(normal, Vector2.up) > _stepClimbComponent.maxWalkableAngle)
                return;

            float distance = _stepClimbComponent.stepCheckDistance;
            float maxHeight = _stepClimbComponent.maxStepHeight;

            Vector2 groundOrigin = _groundC.origin;
            float slope = -normal.x / normal.y;
            float expectedY = groundOrigin.y + slope * distance * facing;

            Vector2 downOrigin = new Vector2(
                groundOrigin.x + facing * distance,
                expectedY + maxHeight);

            RaycastHit2D down = Physics2D.Raycast(
                downOrigin,
                Vector2.down,
                maxHeight * 2f,
                _stepClimbComponent.groundLayer);

            Debug.DrawRay(downOrigin, Vector2.down * maxHeight * 2f, Color.cyan);

            if (down.collider == null || down.fraction <= 0f)
                return;

            if (Vector2.Angle(down.normal, Vector2.up) > _stepClimbComponent.maxWalkableAngle)
                return;

            float extra = down.point.y - expectedY;

            if (extra < _stepClimbComponent.minStepHeight || extra > maxHeight)
                return;

            Vector2 offsetFromFeet = Rb.position - groundOrigin;
            Vector2 targetPosition = down.point + offsetFromFeet;

            if (targetPosition.y <= Rb.position.y)
                return;

            Rb.position = targetPosition;
        }

        public void Dispose()
        {
            owner.OnFixedUpdate -= Update;
        }
    }

    [System.Serializable]
    public class StepClimbComponent : IComponent
    {
        public float maxStepHeight = 0.3f;
        public float minStepHeight = 0.03f;
        public float stepCheckDistance = 0.4f;
        public float maxWalkableAngle = 50f;
        public float hightOffset;
        public LayerMask groundLayer;
    }
}