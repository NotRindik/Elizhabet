using System;
using Controllers;
using UnityEngine;

namespace Systems
{
    public class MoveSystem : BaseSystem
    {
        private MoveComponent moveComponent;
        private ControllersBaseFields baseFields;
        private GroundingComponent groundingComponent;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            this.owner = (EntityController)owner;
            moveComponent = owner.GetControllerComponent<MoveComponent>();
            baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            
            groundingComponent = owner.GetControllerComponent<GroundingComponent>();
            
            if(moveComponent.autoUpdate)
                this.owner.OnFixedUpdate += Update;
        }
        
        public override void OnUpdate()
        {
            var rb = baseFields.rb;

            Vector2 surfaceDir = Vector2.right;

            if (groundingComponent != null &&
                groundingComponent.IsReallyGrounded)
            {
                Vector2 normal = groundingComponent.groundNormal;

                if (normal.y > 0.01f)
                {
                    surfaceDir = Vector2.Perpendicular(normal).normalized;

                    if (surfaceDir.x < 0f)
                        surfaceDir = -surfaceDir;
                }
            }

            float currentSpeed = Vector2.Dot(rb.linearVelocity, surfaceDir);

            float targetSpeed =
                moveComponent.direction.x *
                moveComponent.speed *
                moveComponent.speedMultiplierDynamic;
            
            if (groundingComponent != null &&
                groundingComponent.IsReallyGrounded &&
                surfaceDir.x > 0.01f)
            {
                targetSpeed /= surfaceDir.x;
            }

            float speedDif = targetSpeed - currentSpeed;

            float accelRate = Mathf.Abs(targetSpeed) > 0.01f
                ? moveComponent.acceleration
                : moveComponent.decceleration;

            float movement =
                Mathf.Pow(
                    Mathf.Abs(speedDif) * accelRate,
                    moveComponent.velPower
                ) * Mathf.Sign(speedDif);

            rb.AddForce(surfaceDir * movement);
        }
        
    }
    [System.Serializable]
    public class MoveComponent : IComponent
    {
        public Vector2 direction;
        public float speed;
        public float speedMultiplierDynamic;
        public float frictionAmount;
        public float acceleration;
        public float decceleration;
        public float velPower;
        public bool autoUpdate = false;
    }


    public class SimpleMoveSystem : BaseSystem,IDisposable
    {
        private ControllersBaseFields baseFields;

        private SimpleMoveComponent moveC;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);
            baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            moveC = owner.GetControllerComponent<SimpleMoveComponent>();

            owner.OnFixedUpdate += Update;
        }

        public override void OnUpdate()
        {
            var rb = baseFields.rb;
            
            if(moveC.direction.x == 0 || moveC.speedMultiplier == 0)
                return;

            if (rb.bodyType == RigidbodyType2D.Dynamic)
            {
                rb.position += Vector2.right * (moveC.direction.x * (moveC.speed * moveC.speedMultiplier * Time.fixedDeltaTime));
                return;
            }
            
            rb.MovePosition(rb.position + moveC.direction * (moveC.speed * moveC.speedMultiplier * Time.fixedDeltaTime));
        }
        public void Dispose()
        {
            owner.OnFixedUpdate -= Update;
        }
    }

    [System.Serializable]
    public class SimpleMoveComponent : IComponent
    {
        public Vector2 direction;
        public float speed,speedMultiplier;
    }
}
