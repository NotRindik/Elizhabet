using Controllers;
using UnityEngine;

namespace Systems
{
    public class MoveSystem : BaseSystem
    {
        private MoveComponent moveComponent;

        public override void Initialize(Controller owner)
        {
            base.Initialize(owner);
            moveComponent = owner.GetControllerComponent<MoveComponent>();
        }
        public override void Update()
        {
            float targetSpeed = moveComponent.direction.x * moveComponent.speed;
            float speedDif = targetSpeed - owner.baseFields.rb.linearVelocityX;
            float accelRate = Mathf.Abs(targetSpeed) > 0.01f ? moveComponent.acceleration : moveComponent.decceleration;
            float movement = Mathf.Pow(Mathf.Abs(speedDif) * accelRate, moveComponent.velPower) * Mathf.Sign(speedDif);
            owner.baseFields.rb.AddForce(movement * Vector2.right);
            Friction();
        }
        void Friction()
        {
            if (Mathf.Abs(moveComponent.direction.x) < 0.01f)
            {
                float amount = Mathf.Min(Mathf.Abs(owner.baseFields.rb.linearVelocityX), Mathf.Abs(moveComponent.frictionAmount));

                amount *= Mathf.Sign(owner.baseFields.rb.linearVelocityX);

                owner.baseFields.rb.AddForce(Vector2.right * -amount, ForceMode2D.Impulse);
            }
        }
    }
    [System.Serializable]
    public class MoveComponent : IComponent
    {
        internal Vector2 direction;
        public float speed;
        public float frictionAmount;
        public float acceleration;
        public float decceleration;
        public float velPower;
    }
}
