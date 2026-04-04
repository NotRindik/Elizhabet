using Controllers;
using System;
using UnityEngine;

namespace Systems
{
    internal unsafe class StepClimbSystem : BaseSystem, IDisposable
    {
        private StepClimbComponent _stepClimbComponent;
        private ControllersBaseFields _baseFields;
        private GroundingComponent _groundC;
        private DashComponent dashC;
        private WallRunComponent wllRunC;

        private Rigidbody2D Rb => _baseFields.rb;

        public void Dispose()
        {
            owner.OnUpdate -= Update;
        }

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            _stepClimbComponent = owner.GetControllerComponent<StepClimbComponent>();
            _baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            _groundC = owner.GetControllerComponent<GroundingComponent>();
            dashC = owner.GetControllerComponent<DashComponent>();
            wllRunC = owner.GetControllerComponent<WallRunComponent>();
            owner.OnUpdate += Update;
        }

        public override void OnUpdate()
        {

            if (!_groundC.isGround && !dashC.isDash || wllRunC.wallRunProcess != null)
                return;
            Vector2 origin = (Vector2)transform.position + (Vector2)transform.up * -_stepClimbComponent.hightOffset; // немного над нижней частью коллайдера
            Vector2 direction = Vector2.right * Mathf.Sign(transform.localScale.x); // направление вперед
            var startY = origin.y;
            int steps = 10;
            for (int i = 0; i < steps; i++)
            {
                RaycastHit2D hitFront = Physics2D.Raycast(origin, direction, _stepClimbComponent.stepCheckDistance, _stepClimbComponent.groundLayer);
                Debug.DrawRay(origin, direction * _stepClimbComponent.stepCheckDistance, Color.magenta);

                if (hitFront.collider != null)
                {
                    Vector2 downOrigin = origin + direction * _stepClimbComponent.stepCheckDistance + Vector2.up * _stepClimbComponent.maxStepHeight;
                    float downCheckHeight = downOrigin.y - origin.y;
                    RaycastHit2D hitDown = Physics2D.Raycast(
                        downOrigin,
                        Vector2.down,
                        downCheckHeight,
                        _stepClimbComponent.groundLayer
                    );

                    Debug.DrawRay(downOrigin, Vector2.down * downCheckHeight, Color.cyan);

                    if (hitDown.collider != null)
                    {
                        float targetY = hitDown.point.y;
                        if (targetY < _groundC.origin.y || (downOrigin.y - targetY) < 0.05f)
                            return;


                        float diff = transform.position.y - _groundC.origin.y;

                        if (Mathf.Abs(diff) > 0.001f)
                        {
                            Rb.position = new Vector2(hitDown.point.x, diff + targetY);
                        }
                    }   
                }
                var t = i/steps;
                origin.y = Mathf.Lerp(startY, _groundC.origin.y,t);
            }

            // Debug
            Debug.DrawRay(origin + Vector2.up * _stepClimbComponent.maxStepHeight, direction * _stepClimbComponent.stepCheckDistance, Color.green);
        }

    }

    [System.Serializable]
    public class StepClimbComponent : IComponent
    {
        public float maxStepHeight, stepCheckDistance,hightOffset;
        public LayerMask groundLayer;
    }
}
