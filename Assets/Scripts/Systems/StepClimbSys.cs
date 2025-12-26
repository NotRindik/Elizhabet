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
                    // 3. Проверяем, есть ли свободное место над ступенькой
                    Vector2 stepCheckPos = origin + Vector2.up * _stepClimbComponent.maxStepHeight;
                    RaycastHit2D hitAbove = Physics2D.Raycast(stepCheckPos, direction, _stepClimbComponent.stepCheckDistance, _stepClimbComponent.groundLayer);

                    // 4. Если сверху свободно — поднимаем игрока только на разницу высот
                    if (hitAbove.collider == null)
                    {
                        float stepHeight = hitFront.point.y - _groundC.origin.y + 0.2f;
                        float stepDist = hitFront.point.x - origin.x;
                        if (stepHeight > 0)
                        {
                            Rb.position += new Vector2(stepDist, Mathf.Min(stepHeight, _stepClimbComponent.maxStepHeight));
                            break;
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
