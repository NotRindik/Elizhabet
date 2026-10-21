using System;
using Controllers;
using UnityEngine;

namespace Systems
{
    public class GroundGravitySystem : BaseSystem, IDisposable
    {
        private ControllersBaseFields baseFields;
        private GroundingComponent groundingComponent;

        public override void Initialize(AbstractEntity owner)
        {
            base.Initialize(owner);

            baseFields = owner.GetControllerComponent<ControllersBaseFields>();
            groundingComponent = owner.GetControllerComponent<GroundingComponent>();

            owner.OnFixedUpdate += Update;
        }

        public override void OnUpdate()
        {
            if (groundingComponent == null ||
                !groundingComponent.IsReallyGrounded)
                return;

            Rigidbody2D rb = baseFields.rb;

            if (rb.bodyType != RigidbodyType2D.Dynamic)
                return;

            Vector2 normal = groundingComponent.groundNormal.normalized;
            
            if (normal.y <= 0.01f)
                return;

            Vector2 gravity = Physics2D.gravity * rb.gravityScale;
            
            Vector2 tangentGravity = gravity - Vector2.Dot(gravity, normal) * normal;
            
            rb.AddForce(-tangentGravity * rb.mass);
        }

        public void Dispose()
        {
            owner.OnFixedUpdate -= Update;
        }
    }
}